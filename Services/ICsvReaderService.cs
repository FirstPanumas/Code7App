using Code7App.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Code7App.Services
{
    public interface ICsvReaderService
    {
        Task<CsvDataResult> ReadCsvAsync(string filePath);
    }
}
