namespace HomeFinder.Models.Reports
{
    public class MostViewedApartmentsReportVm
    {
        public int Top { get; set; }

        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }

        public string? SelectedDistrict { get; set; }
        public List<string> Districts { get; set; } = new();

        public List<Row> Items { get; set; } = new();

        public class Row
        {
            public int ApartmentId { get; set; }
            public int Views { get; set; }
            public decimal? Price { get; set; }
            public string? District { get; set; }
            public string? City { get; set; }
            public string? StreetAddress { get; set; }
            public string? BuildingNumber { get; set; }
            public string? PhotoPath { get; set; }
        }
    }
}