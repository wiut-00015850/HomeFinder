using System;
using System.Collections.Generic;

namespace HomeFinder.Models;

public partial class Appointment
{
    public int AppointmentId { get; set; }

    // tenant who created the appointment
    public int? UserId { get; set; }

    public int? ApartmentId { get; set; }

    public int? AddressId { get; set; }

    public DateTime? DateTime { get; set; }

    public virtual User? User { get; set; }

    public virtual Address? Address { get; set; }

    public virtual Apartment? Apartment { get; set; }
}
