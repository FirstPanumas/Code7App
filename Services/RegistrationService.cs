using System.Numerics;
using System.Text.Json;

namespace Code7App.Services;

public class RegistrationService : IRegistrationService
{
    private readonly string _filePath;

    public RegistrationService()
    {
        // กำหนด Path เก็บไฟล์ RegisteredPatients.json ในพื้นที่ Local ของแอป
        _filePath = Path.Combine(FileSystem.AppDataDirectory, "RegisteredPatients.json");
    }

    public async Task<bool> SaveRegistrationAsync(Dictionary<string, string> patientData, DateTime date, TimeSpan time, string department, string doctor, string dx, string allergy)
    {
        {
            try
            {
                var record = new
                {
                    Id = Guid.NewGuid().ToString(),
                    RegisteredAt = DateTime.Now,
                    AppointmentDate = date.ToString("yyyy-MM-dd"),
                    AppointmentTime = time.ToString(@"hh\:mm"),
                    Department = department,
                    Doctor = doctor, // เพิ่มแพทย์
                    Dx = dx,         // เพิ่ม Dx
                    Allergy = allergy,
                    PatientDetails = patientData
                };

                List<object> existingRecords = []; // ใช้ Collection expression (C# 12+)

                // ตรวจสอบและอ่านไฟล์เดิมด้วย Stream เพื่อประสิทธิภาพ (ลด Memory Allocation)
                if (File.Exists(_filePath))
                {
                    using var readStream = File.OpenRead(_filePath);
                    if (readStream.Length > 0)
                    {
                        existingRecords = await JsonSerializer.DeserializeAsync<List<object>>(readStream) ?? [];
                    }
                }

                existingRecords.Add(record);

                // บันทึกไฟล์ทับด้วย Stream
                using var writeStream = File.Create(_filePath);
                var options = new JsonSerializerOptions { WriteIndented = true };
                await JsonSerializer.SerializeAsync(writeStream, existingRecords, options);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RegistrationService Error: {ex.Message}");
                return false;
            }
        }
    }
}