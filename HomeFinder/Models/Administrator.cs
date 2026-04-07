using System;
using System.Collections.Generic;

namespace HomeFinder.Models;

public partial class Administrator
{
    public int AdministratorId { get; set; }

    public string? Login { get; set; }

    public string? Password { get; set; }
}
