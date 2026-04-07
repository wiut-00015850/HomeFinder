using System;
using System.Collections.Generic;

namespace HomeFinder.Models;

public partial class Payment
{
    public long ClickTransId { get; set; }

    public int? UserId { get; set; }

    public int? ServiceId { get; set; }

    public long? ClickPaydocId { get; set; }

    public string? MerchantTransId { get; set; }

    public decimal? Amount { get; set; }

    public int? Action { get; set; }

    public int? Error { get; set; }

    public string? ErrorNote { get; set; }

    public string? SignTime { get; set; }

    public string? SignString { get; set; }

    public virtual User? User { get; set; }
}
