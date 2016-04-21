using PartsUnlimited.Models;
using PartsUnlimited.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PartsUnlimited
{
    public class CostSummaryHelper
    {
        public static OrderCostSummary CalculateCostSummary(ShoppingCart cart)
        {
            var items = cart.GetCartItems();
            var itemsCount = items.Count;
            var subTotal = items.Sum(x => x.Count * x.Product.Price);
            var shipping = itemsCount * (decimal)5.00;
            var tax = (subTotal + shipping) * (decimal)0.05;
            var total = subTotal + shipping + tax;

            var costSummary = new OrderCostSummary
            {
                CartSubTotal = Math.Round(subTotal, 2),
                CartShipping = Math.Round(shipping, 2),
                CartTax = Math.Round(tax, 2),
                CartTotal = Math.Round(total, 2)
            };

            return costSummary;
        }

    }
}
