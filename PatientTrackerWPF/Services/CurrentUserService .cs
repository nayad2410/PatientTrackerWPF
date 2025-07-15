using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PatientTrackerWPF.Models;

namespace PatientTrackerWPF.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        public User? CurrentUser { get; private set; }

        public void SetCurrentUser(User user)
        {
            CurrentUser = user;
        }

        public void ClearCurrentUser()
        {
            CurrentUser = null;
        }
    }

}
