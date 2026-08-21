namespace Code7App.Models;

public class AppConfig
{
    public CsvSetting PatientRegister { get; set; } = new();
    public CsvSetting OrderItem { get; set; } = new();
    public CsvSetting OrderSet { get; set; } = new();
    public OrderConfig OrderSettings { get; set; } = new OrderConfig();
}

public class CsvSetting
{
    public string DefaultPath { get; set; } = string.Empty;
    public string FlagColumn { get; set; } = string.Empty;
    public string DropdownColumn { get; set; } = string.Empty;
    public string DropdownList { get; set; } = string.Empty;
    public string CalculateColumn { get; set; } = string.Empty;
    public string FormDisplayColumns { get; set; } = string.Empty;
    public string DepartmentList { get; set; } = "OPD (ผู้ป่วยนอก), IPD (ผู้ป่วยใน), ER (ฉุกเฉิน), OR (ห้องผ่าตัด)"; 
    public string DoctorList { get; set; } = "ระบุแพทย์ทีหลัง";


}

public class OrderConfig
{
    public string DefaultPath { get; set; } = string.Empty;
}