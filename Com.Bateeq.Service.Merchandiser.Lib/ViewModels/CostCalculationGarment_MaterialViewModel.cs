﻿using Com.Bateeq.Service.Merchandiser.Lib.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Com.Bateeq.Service.Merchandiser.Lib.ViewModels
{
    public class CostCalculationGarment_MaterialViewModel : BasicViewModel
    {
        public string Code { get; set; }
        public int? PO_SerialNumber { get; set; }
        public string PO { get; set; }
        public CategoryViewModel Category { get; set; }
        public MaterialViewModel Material { get; set; }
        public string Description { get; set; }
        public double? Quantity { get; set; }
        public UOMViewModel UOMQuantity { get; set; }
        public double? Price { get; set; }
        public UOMViewModel UOMPrice { get; set; }
        public double? Conversion { get; set; }
        public double Total { get; set; }
        public bool isFabricCM { get; set; }
        public double? CM_Price { get; set; }
        public double? ShippingFeePortion { get; set; }
        public double TotalShippingFee { get; set; }
        public double BudgetQuantity { get; set; }
        public string Information { get; set; }
    }
}
