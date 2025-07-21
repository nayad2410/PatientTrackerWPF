using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PatientTrackerWPF.Models;

namespace PatientTrackerWPF.Constants
{
  public static class UserRoles
    {
        public const string Admin = "Admin";
        public const string Doctor = "Doctor";
        public const string Technician = "Technician";
        public const string Researcher = "Researcher";
        public const string User = "User";
        public const string Test = "Test";

        // Helper method to validate roles
        public static bool IsValidRole(string role)
        {
            return role switch
            {
                Admin or Doctor or Technician or Researcher or User or Test => true,
                _ => false
            };
        }

        // Get all available roles for dropdowns
        public static string[] GetAllRoles()
        {
            return new[] { Admin, Doctor, Technician, Researcher, User, Test };
        }

        // Get roles that can be assigned by current user
        public static string[] GetAssignableRoles(string currentUserRole)
        {
            return currentUserRole switch
            {
                Admin => new[] { Admin, Doctor, Technician, Researcher, User, Test },
                Doctor => new[] { Technician, Researcher, User },
                _ => new string[0] // Others can't create users
            };
        }

        // Get role descriptions
        public static string GetRoleDescription(string role)
        {
            return role switch
            {
                Admin => "System Administrator - Full Access",
                Doctor => "Doctor - Patient Management & Clinical Reports",
                Technician => "Technician - Data Entry & Basic Reports",
                Researcher => "Researcher - Read-Only Access & Analytics (Presentation Ready)",
                User => "User - Limited Access",
                Test => "Test Account - Demo Mode",
                _ => "Unknown Role"
            };
        }
    }
}
