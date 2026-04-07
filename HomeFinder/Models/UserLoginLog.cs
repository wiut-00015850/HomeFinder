using System;
using System.Collections.Generic;

namespace HomeFinder.Models;

public partial class UserLoginLog
{
    public int LogId { get; set; }

    public int? UserId { get; set; }

    public DateTime? LoginTime { get; set; }

    public virtual User? User { get; set; }
}
