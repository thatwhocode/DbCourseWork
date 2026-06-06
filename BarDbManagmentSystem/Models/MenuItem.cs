using System;
using System.Collections.Generic;

namespace BarDbManagmentSystem.Models;

public partial class MenuItem
{
    public int ItemId { get; set; }

    public string Name { get; set; } = null!;

    public decimal Price { get; set; }

    public int? CategoryId { get; set; }

    public virtual Category? Category { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
}
