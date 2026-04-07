using System;
using System.Collections.Generic;

namespace HomeFinder.Models;

public partial class Photo
{
    public int PhotoId { get; set; }

    public string? PhotoPath { get; set; }

    public int? ApartmentId { get; set; }

    public virtual Apartment? Apartment { get; set; }
}
