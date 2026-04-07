using System;
using System.Collections.Generic;

namespace HomeFinder.Models;

public partial class User
{
    public int UserId { get; set; }

    public bool? IsTenant { get; set; }

    public bool? IsLandlord { get; set; }

    public string? Login { get; set; }

    public string? Password { get; set; }

    public string? Pinfl { get; set; }

    public string? PassportSeries { get; set; }

    public string? PassportNumber { get; set; }

    public DateOnly? BirthDate { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? MiddleName { get; set; }

    public string? PhoneNumber { get; set; }

    public virtual ICollection<Apartment> Apartments { get; set; } = new List<Apartment>();

    public virtual ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<ReviewApartment> ReviewApartments { get; set; } = new List<ReviewApartment>();

    public virtual ICollection<UserLoginLog> UserLoginLogs { get; set; } = new List<UserLoginLog>();
}
