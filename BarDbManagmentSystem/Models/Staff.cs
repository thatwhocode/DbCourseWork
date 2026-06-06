using System;
using System.Collections.Generic;

namespace BarDbManagmentSystem.Models;

public partial class Staff
{
    public int StaffId { get; set; }

    public string FullName { get; set; } = null!;

    public string Position { get; set; } = null!;

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Shift> Shifts { get; set; } = new List<Shift>();

    public virtual ICollection<StaffLanguage> StaffLanguages { get; set; } = new List<StaffLanguage>();

    public virtual ICollection<StaffSpeciality> StaffSpecialities { get; set; } = new List<StaffSpeciality>();

    public virtual ICollection<Hall> Halls { get; set; } = new List<Hall>();

    public virtual ICollection<Shift> ShiftsNavigation { get; set; } = new List<Shift>();
}
