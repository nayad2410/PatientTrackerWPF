using System;
using System.Globalization;
using System.Windows.Data;

namespace PatientTrackerWPF
{
    //my ScoreConverter class is functioning as a WPF value converter that cleans up how nullable or missing score data is displayed and interpreted in the app.


    public class ScoreConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // FIXED: Handle null values and display as "—"
            if (value == null) return "—";

            // Handle nullable integers
            if (value is int nullableInt)
            {
                return nullableInt.ToString();
            }

            // Handle regular integers (legacy -1 values)
            if (value is int regularInt)
            {
                return regularInt == -1 ? "—" : regularInt.ToString();
            }

            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Handle conversion back from string to nullable int
            if (targetType == typeof(int?) || targetType == typeof(int))
            {
                if (value is string str)
                {
                    if (string.IsNullOrWhiteSpace(str) || str == "—")
                        return null;

                    if (int.TryParse(str, out int result))
                        return result;
                }
            }

            return value;
        }
    }
}