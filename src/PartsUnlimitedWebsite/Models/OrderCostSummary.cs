// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PartsUnlimited.ViewModels
{
    public class OrderCostSummary
    {
        public decimal CartSubTotal { get; set; }
        public decimal CartShipping { get; set; }
        public decimal CartTax { get; set; }
        public decimal CartTotal { get; set; }
    }
}