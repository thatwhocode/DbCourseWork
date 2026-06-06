using System;
using System.Collections.Generic;

namespace BarDbManagmentSystem.Models;

public partial class Recipe
{
    public int RecipeId { get; set; }

    public int ItemId { get; set; }

    public int IngredientId { get; set; }

    public decimal Quantity { get; set; }

    public virtual Ingredient Ingredient { get; set; } = null!;

    public virtual MenuItem Item { get; set; } = null!;
}
