using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Code7App.Models
{
    public class OrderItemModel
    {
        public string OrderCode { get; set; } = string.Empty;
        public string OrderName { get; set; } = string.Empty;
        public decimal Price { get; set; }

        // เก็บค่าคอลัมน์เป้าหมายสำหรับ Filter
        public string FilterValue { get; set; } = string.Empty;

        // เก็บแค่ String บรรทัดดิบๆ ประหยัด Memory กว่า Dictionary มาก
        public string RawCsvLine { get; set; } = string.Empty;

        public List<PatientDetailModel> DisplayFields { get; set; } = [];
    }
}
