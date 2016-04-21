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

            var order = new Order
            {
                Name = user.Name,
                Email = user.Email,
                Username = user.UserName
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

                HandleDonation(formCollection, order);

                //Add the Order
                _db.Orders.Add(order);

                //Process the order
                var cart = ShoppingCart.GetCart(_db, HttpContext);
                cart.CreateOrder(order);

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

        private static async void HandleDonation(Microsoft.AspNet.Http.IFormCollection formCollection, Order order)
        {
            string donationAmountStr = "DonationAmount";
            var donationFieldStr = formCollection["DonationAmount"].FirstOrDefault();
            if (string.Equals(donationFieldStr, donationAmountStr,
                StringComparison.OrdinalIgnoreCase) != false || !String.IsNullOrEmpty(donationFieldStr))
            {
                // Donation field is populated
                try
                {
                    // Validate type
                    float donationAmount = float.Parse(donationFieldStr);

                    // Validate range
                    if (donationAmount > 0.0f && donationAmount < 10000.0f)
                    {
                        var retailer = "PartsUnlimited";
                        var donation = new
                        {
                            sourceRetailer = retailer,
                            customerId = "bobtaylor@hotmail.com",
                            orderId = $"{retailer}_{order.OrderId}",
                            currency = "GBP",
                            dateTime = DateTime.Now
                        };

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
