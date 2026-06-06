using System;
using System.Collections.Generic;

namespace BarDbManagmentSystem.Models;

public partial class OrderDetail
{
    public int OrderId { get; set; }

    public int ItemId { get; set; }

    public int Quantity { get; set; }

    public decimal? SalePrice { get; set; }

    public virtual MenuItem Item { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}
