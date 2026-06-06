using System;
using System.Collections.Generic;

namespace BarDbManagmentSystem.Models;

public partial class StaffSpeciality
{
    public int StaffId { get; set; }

    public string Specialization { get; set; } = null!;

    public virtual Staff Staff { get; set; } = null!;
}
