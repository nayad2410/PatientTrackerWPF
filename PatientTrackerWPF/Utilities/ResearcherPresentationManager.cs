using PatientTrackerWPF.Constants;
using PatientTrackerWPF.Helper;

using PatientTrackerWPF.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PatientTrackerWPF.Utilities
{
    public static class ResearcherPresentationManager
    {
        public static bool IsResearcherPresentationReady(User user)
        {
            return RoleHelper.IsResearcher(user) && user.IsActive;
        }

        public static string GetResearcherPresentationInfo()
        {
            return "Researcher Account - Presentation Ready:\n\n" +
                   "✅ Perfect for demonstrations\n" +
                   "✅ All viewing and reporting features\n" +
                   "✅ Professional presentation interface\n" +
                   "✅ Read-only data protection\n\n" +
                   "🎯 Use your researcher credentials for presentations";
        }

        public static string GetPresentationCapabilities()
        {
            return "🎯 PRESENTATION CAPABILITIES:\n\n" +
                   "✅ View all patient data and trends\n" +
                   "✅ Generate professional reports\n" +
                   "✅ Export data for analysis\n" +
                   "✅ View clinical metrics and outcomes\n" +
                   "✅ Demonstrate chart functionality\n" +
                   "✅ Show remission tracking\n" +
                   "✅ Display treatment notes\n\n" +
                   "🔒 Data is protected from accidental changes\n" +
                   "Perfect for showcasing analytical features!";
        }

        public static void ShowResearcherTips()
        {
            MessageBox.Show(
                "🎯 RESEARCHER PRESENTATION TIPS:\n\n" +
                "1. Use 'Generate Professional Report' to show clinical outputs\n" +
                "2. Try 'Calculate Metrics' to demonstrate outcome analysis\n" +
                "3. Use 'Export to CSV' to show data export capabilities\n" +
                "4. Click on patient charts to show interactive features\n" +
                "5. Double-click data grid entries to show data details\n" +
                "6. Use 'View All Remissions' for population analysis\n\n" +
                "Your researcher account showcases all analytical features perfectly!",
                "Presentation Tips",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
