using System;
using System.Collections.Generic;

namespace BarDbManagmentSystem.Models;

public partial class Shift
{
    public int ShiftId { get; set; }

    public int StaffId { get; set; }

    public DateTime WorkDate { get; set; }

    public string? ShiftType { get; set; }

    public virtual Staff Staff { get; set; } = null!;

    public virtual ICollection<Hall> Halls { get; set; } = new List<Hall>();

    public virtual ICollection<Staff> StaffNavigation { get; set; } = new List<Staff>();
}
