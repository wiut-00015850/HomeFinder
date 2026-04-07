using System;
using System.Collections.Generic;

namespace HomeFinder.Models;

public partial class Apartment
{
    public int ApartmentId { get; set; }

    public int? UserId { get; set; }

    public string? Description { get; set; }

    public decimal? Price { get; set; }

    public int? Size { get; set; }

    public int? Rooms { get; set; }

    public virtual ICollection<Address> Addresses { get; set; } = new List<Address>();

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

    public virtual ICollection<Photo> Photos { get; set; } = new List<Photo>();

    public virtual ICollection<ReviewApartment> ReviewApartments { get; set; } = new List<ReviewApartment>();

    public virtual User? User { get; set; }
}
