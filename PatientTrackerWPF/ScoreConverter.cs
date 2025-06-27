using System;
using System.Globalization;
using System.Windows.Data;

namespace PatientTrackerWPF
{
    public class ScoreConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int score)
            {
                return score == -1 ? "—" : score.ToString();
            }
            return "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                if (str == "—" || string.IsNullOrWhiteSpace(str))
                    return -1;

                if (int.TryParse(str, out int result))
                    return result;
            }
            return -1;
        }
    }
}