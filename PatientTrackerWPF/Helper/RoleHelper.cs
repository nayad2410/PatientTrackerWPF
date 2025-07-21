using PatientTrackerWPF.Constants;
using PatientTrackerWPF.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PatientTrackerWPF.Helper
{
    public static class RoleHelper
    {
        public static bool IsAdmin(User? user) => user?.Role == UserRoles.Admin;
        public static bool IsDoctor(User? user) => user?.Role == UserRoles.Doctor;
        public static bool IsTechnician(User? user) => user?.Role == UserRoles.Technician;
        public static bool IsResearcher(User? user) => user?.Role == UserRoles.Researcher;
        public static bool IsTest(User? user) => user?.Role == UserRoles.Test;

        public static bool CanManageUsers(User? user) => IsAdmin(user) || IsDoctor(user);
        public static bool CanAddData(User? user) => IsAdmin(user) || IsDoctor(user) || IsTechnician(user);
        public static bool CanEditData(User? user) => IsAdmin(user) || IsDoctor(user) || IsTechnician(user);
        public static bool CanDeleteData(User? user) => IsAdmin(user) || IsDoctor(user) || IsTechnician(user);
        public static bool CanExportData(User? user) => IsAdmin(user) || IsDoctor(user) || IsResearcher(user);
        public static bool CanGenerateReports(User? user) => IsAdmin(user) || IsDoctor(user) || IsTechnician(user) || IsResearcher(user);

        public static string GetPermissionSummary(User? user)
        {
            if (user == null) return "No permissions";

            var permissions = new List<string>();
            if (CanManageUsers(user)) permissions.Add("Manage Users");
            if (CanAddData(user)) permissions.Add("Add Data");
            if (CanEditData(user)) permissions.Add("Edit Data");
            if (CanDeleteData(user)) permissions.Add("Delete Data");
            if (CanExportData(user)) permissions.Add("Export Data");
            if (CanGenerateReports(user)) permissions.Add("Generate Reports");

            return permissions.Any() ? string.Join(", ", permissions) : "View Only";
        }
    }
}

