using System.Text.RegularExpressions;
using Code7App.Models;

namespace Code7App.Services;

public partial class CsvReaderService : ICsvReaderService
{
    // ใช้ Source Generator ของ .NET 9 เพื่อประสิทธิภาพสูงสุดในการ Compile Regex
    [GeneratedRegex(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))")]
    private partial Regex CsvSplitRegex();

    public async Task<CsvDataResult> ReadCsvAsync(string filePath)
    {
        var result = new CsvDataResult();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return result;

        // ใช้ FileShare.ReadWrite เพื่อป้องกัน Error File is locked หากมีโปรแกรมอื่นเปิดไฟล์นี้อยู่
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        string? headerLine = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(headerLine)) return result;

        // อ่าน Header
        result.Columns = headerLine.Split(',').Select(c => c.Trim('"')).ToList();

        // อ่าน Data Rows แบบบรรทัดต่อบรรทัดเพื่อประหยัด Memory
        var csvParser = CsvSplitRegex();

        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var values = csvParser.Split(line).Select(v => v.Trim('"')).ToArray();
            var rowDict = new Dictionary<string, string>();

            for (int i = 0; i < result.Columns.Count; i++)
            {
                var columnName = result.Columns[i];
                rowDict[columnName] = i < values.Length ? values[i] : string.Empty;
            }

            result.Rows.Add(rowDict);
        }

        return result;
    }
}