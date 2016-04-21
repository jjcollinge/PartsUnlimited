// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.Authorization;
using Microsoft.AspNet.Mvc;
using Microsoft.Data.Entity;
using Newtonsoft.Json;
using PartsUnlimited.Models;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace PartsUnlimited.Controllers
{
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly IPartsUnlimitedContext _db;
        private const decimal MAX_DONATION = 10000.0M;
        private const decimal MIN_DONATION = 0.1M;

        public CheckoutController(IPartsUnlimitedContext context)
        {
            _db = context;
        }

        private const string PromoCode = "FREE";

        //
        // GET: /Checkout/

        public async Task<IActionResult> AddressAndPayment()
        {
            var id = User.GetUserId();
            var user = await _db.Users.FirstOrDefaultAsync(o => o.Id == id);

            var cart = ShoppingCart.GetCart(_db, HttpContext);
            var costSummary = CostSummaryHelper.CalculateCostSummary(cart);

            var order = new Order
            {
                Name = user.Name,
                Email = user.Email,
                Username = user.UserName,
                Total = costSummary.CartTotal
            };

            return View(order);
        }

        //
        // POST: /Checkout/AddressAndPayment

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddressAndPayment(Order order)
        {
            var formCollection = await HttpContext.Request.ReadFormAsync();

            try
            {
                // Hack to accept orders without promo code for hackathon

                //if (string.Equals(formCollection["PromoCode"].FirstOrDefault(), PromoCode,
                //    StringComparison.OrdinalIgnoreCase) == false)
                //{
                //    return View(order);
                //}
                //else
                //{

                order.Username = HttpContext.User.GetUserName();
                order.OrderDate = DateTime.Now;

                //Add the Order
                _db.Orders.Add(order);

                //Process the order
                var cart = ShoppingCart.GetCart(_db, HttpContext);
                cart.CreateOrder(order);

                //Handle donations
                if (order.Donated)
                    HandleDonation(order);

                // Save all changes
                await _db.SaveChangesAsync(HttpContext.RequestAborted);

                return RedirectToAction("Complete",
                    new { id = order.OrderId });
                //}
            }
            catch
            {
                //Invalid - redisplay with errors
                return View(order);
            }
        }

        private static async void HandleDonation(Order order)
        {
            try
            {
                //TODO: Fix rounding errors by increasing precision to 3 dp
                var donationAmount = Math.Ceiling(order.Total) - order.Total;

                // Validate range
                if (donationAmount < MAX_DONATION)
                {
                    if (donationAmount < MIN_DONATION)
                        donationAmount = MIN_DONATION;

                    var retailer = "PartsUnlimited";
                    var donation = new
                    {
                        sourceRetailer = retailer,
                        customerId = order.Email,
                        orderId = $"{retailer}_{order.OrderId}",
                        currency = order.Total.ToString("C"),
                        dateTime = DateTime.Now
                    };

                    // Move to config
                    var baseUrl = "http://*";
                    var endpoint = "api/donation";

                    // Send donation notification to external web api
                    using (var client = new HttpClient())
                    {
                        client.BaseAddress = new Uri(baseUrl);
                        client.DefaultRequestHeaders.Accept.Clear();
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                        var response = await client.PostAsync($"{baseUrl}/{endpoint}",
                            new StringContent(
                                JsonConvert.SerializeObject(donation)));
                        if (response.IsSuccessStatusCode)
                        {
                            // Do something with the response
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Swallow any invalid field inputs
                Console.WriteLine(ex.Message);
            }
        }


        //
        // GET: /Checkout/Complete

        public IActionResult Complete(int id)
        {
            // Validate customer owns this order
            Order order = _db.Orders.FirstOrDefault(
                o => o.OrderId == id &&
                o.Username == HttpContext.User.GetUserName());

            if (order != null)
            {
                return View(order);
            }
            else
            {
                return View("Error");
            }
        }
    }
}
