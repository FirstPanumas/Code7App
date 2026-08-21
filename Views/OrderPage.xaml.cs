using Code7App.Messages;
using Code7App.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using System.Diagnostics;
using System.IO;

namespace Code7App.Views;
public partial class OrderPage : ContentPage, IQueryAttributable
{
    public OrderPage(OrderViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // รอรับคำสั่งสร้าง PDF จาก ViewModel
        WeakReferenceMessenger.Default.Register<PrintHtmlMessage>(this, (r, m) =>
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    // 1. นำข้อมูล HTML ใส่เข้าไปใน WebView ที่ซ่อนอยู่
                    PrintHelperWebView.Source = new HtmlWebViewSource { Html = m.HtmlContent };

                    // 2. หน่วงเวลาให้ WebView2 สร้าง DOM และจัดรูปแบบ CSS ให้เสร็จสมบูรณ์
                    await Task.Delay(1000);

#if WINDOWS
                    if (PrintHelperWebView.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 webView2)
                    {
                        // บังคับ Initialize ให้แน่ใจว่า CoreWebView2 พร้อมทำงาน
                        await webView2.EnsureCoreWebView2Async();

                        string fileName = $"Receipt_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                        string filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

                        // 3. สร้าง PrintSettings สำหรับกระดาษ A4 แนวตั้ง
                        var printSettings = webView2.CoreWebView2.Environment.CreatePrintSettings();
                        
                        // 🌟 ตั้งค่าเป็นแนวตั้ง (Portrait)
                        printSettings.Orientation = Microsoft.Web.WebView2.Core.CoreWebView2PrintOrientation.Portrait;
                        
                        // 🌟 กำหนดขนาดกระดาษ A4 (กว้าง 8.27 นิ้ว x สูง 11.69 นิ้ว)
                        printSettings.PageWidth = 8.27;
                        printSettings.PageHeight = 11.69;
                        
                        printSettings.ShouldPrintBackgrounds = true;

                        // 4. สั่งแปลง HTML เป็น PDF
                        bool isSuccess = await webView2.CoreWebView2.PrintToPdfAsync(filePath, printSettings);

                        if (isSuccess)
                        {
                            OpenPdfWithChrome(filePath);
                        }
                        else
                        {
                            await DisplayAlert("Error", "เกิดข้อผิดพลาดในการสร้างไฟล์ PDF", "OK");
                        }
                    }
#endif
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Print System Error", ex.Message, "OK");
                }
            });
        });
    }

    // ฟังก์ชันรับพารามิเตอร์ตอนเปลี่ยนหน้า
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is OrderViewModel vm)
        {
            Dictionary<string, string>? patientData = null;
            string? csvPath = null;

            if (query.TryGetValue("PatientData", out var pData) || query.TryGetValue("patientData", out pData))
            {
                patientData = pData as Dictionary<string, string>;
            }

            if (query.TryGetValue("CsvPath", out var cPath) || query.TryGetValue("csvPath", out cPath))
            {
                csvPath = cPath?.ToString();
            }

            vm.InitializeOrderData(patientData, csvPath);
        }
    }

    // ฟังก์ชันจัดการเปิด PDF ด้วย Chrome เหมือนใน MainPage
    private void OpenPdfWithChrome(string filePath)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "chrome",
                Arguments = $"\"{filePath}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
    }
}