using System;
using System.Collections.Generic;

namespace BarDbManagmentSystem.Models;

public partial class StaffLanguage
{
    public int StaffId { get; set; }

    public string Languages { get; set; } = null!;

    public virtual Staff Staff { get; set; } = null!;
}
