﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.Antiforgery;
using Microsoft.AspNet.Mvc;
using Microsoft.Extensions.Primitives;
using PartsUnlimited.Models;
using PartsUnlimited.Telemetry;
using PartsUnlimited.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PartsUnlimited.Controllers
{
    public class ShoppingCartController : Controller
    {
        private readonly IPartsUnlimitedContext _db;
        private readonly ITelemetryProvider _telemetry;
        private readonly IAntiforgery _antiforgery;

        public ShoppingCartController(IPartsUnlimitedContext context, ITelemetryProvider telemetryProvider, IAntiforgery antiforgery)
        {
            _db = context;
            _telemetry = telemetryProvider;
            _antiforgery = antiforgery;
        }

        //
        // GET: /ShoppingCart/

        public IActionResult Index()
        {
            var cart = ShoppingCart.GetCart(_db, HttpContext);
            var costSummary = CostSummaryHelper.CalculateCostSummary(cart);
  
            // Set up our ViewModel
            var viewModel = new ShoppingCartViewModel
            {
                CartItems = cart.GetCartItems(),
                CartCount = cart.GetCartItems().Count,
                OrderCostSummary = costSummary
            };

            // Track cart review event with measurements
            _telemetry.TrackTrace("Cart/Server/Index");

            // Return the view
            return View(viewModel);
        }

       
        //
        // GET: /ShoppingCart/AddToCart/5

        public async Task<IActionResult> AddToCart(int id)
        {
            // Retrieve the product from the database
            var addedProduct = _db.Products
                .Single(product => product.ProductId == id);

            // Start timer for save process telemetry
            var startTime = System.DateTime.Now;

            // Add it to the shopping cart
            var cart = ShoppingCart.GetCart(_db, HttpContext);

            cart.AddToCart(addedProduct);

            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            // Trace add process
            var measurements = new Dictionary<string, double>()
            {
                {"ElapsedMilliseconds", System.DateTime.Now.Subtract(startTime).TotalMilliseconds }
            };
            _telemetry.TrackEvent("Cart/Server/Add", null, measurements);

            // Go back to the main store page for more shopping
            return RedirectToAction("Index");
        }

        //
        // AJAX: /ShoppingCart/RemoveFromCart/5
        [HttpPost]
        public async Task<IActionResult> RemoveFromCart(int id)
        {
            var cookieToken = string.Empty;
            var formToken = string.Empty;
            StringValues tokenHeaders;
            string[] tokens = null;

            if (HttpContext.Request.Headers.TryGetValue("RequestVerificationToken", out tokenHeaders))
            {
                tokens = tokenHeaders.First().Split(':');
                if (tokens != null && tokens.Length == 2)
                {
                    cookieToken = tokens[0];
                    formToken = tokens[1];
                }
            }


            _antiforgery.ValidateTokens(HttpContext, new AntiforgeryTokenSet(formToken, cookieToken));

            // Start timer for save process telemetry
            var startTime = System.DateTime.Now;

            // Retrieve the current user's shopping cart
            var cart = ShoppingCart.GetCart(_db, HttpContext);

            // Get the name of the product to display confirmation
            // TODO [EF] Turn into one query once query of related data is enabled
            int productId = _db.CartItems.Single(item => item.CartItemId == id).ProductId;
            string productName = _db.Products.Single(a => a.ProductId == productId).Title;

            // Remove from cart
            int itemCount = cart.RemoveFromCart(id);

            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            string removed = (itemCount > 0) ? " 1 copy of " : string.Empty;

            // Trace remove process
            var measurements = new Dictionary<string, double>()
            {
                {"ElapsedMilliseconds", System.DateTime.Now.Subtract(startTime).TotalMilliseconds }
            };
            _telemetry.TrackEvent("Cart/Server/Remove", null, measurements);

            // Display the confirmation message
            var costSummary = CostSummaryHelper.CalculateCostSummary(cart);

            var results = new ShoppingCartRemoveViewModel
            {
                Message = removed + productName +
                    " has been removed from your shopping cart.",
                CartSubTotal = costSummary.CartSubTotal.ToString("C"),
                CartShipping = costSummary.CartShipping.ToString("C"),
                CartTax = costSummary.CartTax.ToString("C"),
                CartTotal = costSummary.CartSubTotal.ToString("C"),
                CartCount = cart.GetCount(),
                ItemCount = cart.GetCount(),
                DeleteId = id
            };

            return Json(results);
        }

        // NOTE - This is a dubious hack to simplify a hackfest. Do NOT use this in a live environment or kittens will be harmed
        [HttpGet]
        public async Task<ActionResult> AutoPopulateCart()
        {
            // Retrieve the product from the database
            var productsToAdd = _db.Products
                .Take(3);

            // Start timer for save process telemetry
            var startTime = System.DateTime.Now;

            // Add it to the shopping cart
            var cart = ShoppingCart.GetCart(_db, HttpContext);

            foreach (var product in productsToAdd)
            {
                cart.AddToCart(product);
            }

            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            return RedirectToAction(actionName: "Index", controllerName: "Home");
        }
    }
}