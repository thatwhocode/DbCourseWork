using System;
using System.Collections.Generic;

namespace BarDbManagmentSystem.Models;

public partial class Order
{
    public int OrderId { get; set; }

    public int? StaffId { get; set; }

    public DateTime OrderDate { get; set; }

    public int TableNumber { get; set; }

    public bool IsCompleted { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual Staff? Staff { get; set; }
}
