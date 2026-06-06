using System;
using System.Collections.Generic;

namespace BarDbManagmentSystem.Models;

public partial class BarSystemLog
{
    public int LogId { get; set; }

    public DateTime? LogDate { get; set; }

    public string? AppUser { get; set; }

    public string? ActionDescription { get; set; }
}
