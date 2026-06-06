using System;
using System.Collections.Generic;

namespace BarDbManagmentSystem.Models;

public partial class BarTable
{
    public int TableId { get; set; }

    public string? Name { get; set; }

    public int? Capacity { get; set; }

    public int HallId { get; set; }

    public virtual Hall Hall { get; set; } = null!;
}
