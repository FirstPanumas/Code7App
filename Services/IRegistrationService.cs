using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Code7App.Services
{
    public interface IRegistrationService
    {
        Task<bool> SaveRegistrationAsync(Dictionary<string, string> patientData, DateTime date, TimeSpan time, string department, string doctor, string dx, string allergy);
    }
}
