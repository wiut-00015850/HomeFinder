using System;
using System.Collections.Generic;

namespace HomeFinder.Models.Reports;

public sealed class ApartmentInteractivityReportVm
{
    public int Top { get; set; }
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }

    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public decimal PriceBucketSize { get; set; }

    public List<Row> Items { get; set; } = new();

    public List<GroupRow> ByPriceRange { get; set; } = new();
    public List<GroupRow> ByDistrict { get; set; } = new();
    public List<GroupRow> ByRooms { get; set; } = new();

    public sealed class Row
    {
        public int ApartmentId { get; set; }
        public int Views { get; set; }
        public int Inquiries { get; set; } // appointments in period
        public int FavoritesTotal { get; set; } // all-time (no timestamp in table)
        public double ConversionRate { get; set; } // inquiries / views

        public decimal? Price { get; set; }
        public int? Rooms { get; set; }
        public string? District { get; set; }
        public string? City { get; set; }
        public string? StreetAddress { get; set; }
        public string? BuildingNumber { get; set; }
        public string? PhotoPath { get; set; }

        public string? DetailsUrl { get; set; }
    }

    public sealed class GroupRow
    {
        public string Key { get; set; } = "";
        public int ApartmentsCount { get; set; }
        public int Views { get; set; }
        public int Inquiries { get; set; }
        public double ConversionRate { get; set; }
    }
}

