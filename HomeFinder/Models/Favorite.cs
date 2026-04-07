using System;
using System.Collections.Generic;

namespace HomeFinder.Models;

public partial class Favorite
{
    public int FavoriteId { get; set; }

    public int? UserId { get; set; }

    public int? ApartmentId { get; set; }

    public virtual Apartment? Apartment { get; set; }

    public virtual User? User { get; set; }
}
