using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Code7App.Models
{
    public class CsvDataResult
    {
        public List<string> Columns { get; set; } = [];
        public List<Dictionary<string, string>> Rows { get; set; } = [];
    }
}
