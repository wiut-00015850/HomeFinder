using System;
using System.Collections.Generic;

namespace HomeFinder.Models.Reports
{
    public class MostActiveTenantsReportVm
    {
        public int Top { get; set; }
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }

        public List<Row> Items { get; set; } = new List<Row>();

        public class Row
        {
            public int UserId { get; set; }
            public string Login { get; set; } = string.Empty;
            public string TenantName { get; set; } = string.Empty;
            public int LoginCount { get; set; }
            public DateTime? LastLoginTime { get; set; }
            public int AppointmentCount { get; set; }
        }
    }
}

