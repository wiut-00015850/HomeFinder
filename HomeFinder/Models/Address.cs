using System;
using System.Collections.Generic;

namespace HomeFinder.Models;

public partial class Address
{
    public int AddressId { get; set; }

    public int? ApartmentId { get; set; }

    public string? StreetAddress { get; set; }

    public string? BuildingNumber { get; set; }

    public string? ApartmentNumber { get; set; }

    public string? District { get; set; }

    public string? City { get; set; }

    public string? Region { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public virtual Apartment? Apartment { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
