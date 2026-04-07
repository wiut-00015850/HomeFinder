using System;
using System.Collections.Generic;

namespace HomeFinder.Models.Reports;

public sealed class ApartmentPriceAnalyticsReportVm
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }

    public string? SelectedDistrict { get; set; }
    public List<string> Districts { get; set; } = new();

    public int? Rooms { get; set; }
    public string? SelectedApartmentType { get; set; }

    public string Granularity { get; set; } = "daily"; // daily | weekly | monthly

    public List<AverageByDistrictRow> AverageByDistrict { get; set; } = new();
    public List<PriceTrendRow> PriceTrend { get; set; } = new();

    public sealed class AverageByDistrictRow
    {
        public string District { get; set; } = "";
        public decimal AveragePrice { get; set; }
        public int ApartmentsCount { get; set; }
    }

    public sealed class PriceTrendRow
    {
        public string Label { get; set; } = "";
        public decimal AveragePrice { get; set; }
        public int ViewsCount { get; set; }
    }
}

