using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BarDbManagmentSystem
{
    public  class ExceptionResult
    {
        public string ErrorMessage { get; set; }
        public System.Collections.IEnumerable ReferenceData { get; set; }
        public string IdFieldName {  get; set; }
        public bool NeedsAdjustion => ReferenceData != null;
    }
}
