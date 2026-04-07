namespace HomeFinder.Models.Reports;

public sealed class MostViewedDistrictsReportVm
{
    public int Top { get; set; }
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public List<Row> Items { get; set; } = new();

    public sealed class Row
    {
        public string District { get; set; } = "";
        public int TotalViews { get; set; }
        public int ApartmentsCount { get; set; }
    }
}
