using PatientTrackerWPF.Models;
using System;

namespace PatientTrackerWPF.Services
{
    public interface ICurrentUserService
    {
        User? CurrentUser { get; }
        void SetCurrentUser(User user);
        void ClearCurrentUser();
    }


}