using System;
using System.Collections.Generic;

namespace BarDbManagmentSystem.Models;

public partial class Hall
{
    public int HallId { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<BarTable> BarTables { get; set; } = new List<BarTable>();

    public virtual ICollection<Shift> Shifts { get; set; } = new List<Shift>();

    public virtual ICollection<Staff> Staff { get; set; } = new List<Staff>();
}
