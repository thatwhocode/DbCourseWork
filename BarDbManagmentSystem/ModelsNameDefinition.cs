using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BarDbManagmentSystem.Models
{
    public partial class Staff : IDbEntityDisplay { public string DisplayName => "Співробітник"; }
    public partial class Shift : IDbEntityDisplay { public string DisplayName => "Зміна персоналу"; }

    internal class ModelsNameDefinition
    {
    }
}
