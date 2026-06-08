using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WannaDoWidget
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DateFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dt)
            {
                return dt.ToString("yyyy/MM/dd HH:mm");
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StateToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WannaDoState state)
            {
                return state switch
                {
                    WannaDoState.Aborted => new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 120, 80, 80)), // soft dark red
                    WannaDoState.Expired => new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 120, 100, 40)), // soft dark orange/yellow
                    _ => new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 60, 60, 60))
                };
            }
            return new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 60, 60, 60));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StateTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WannaDoState state)
            {
                return state switch
                {
                    WannaDoState.Todo => "In Progress",
                    WannaDoState.Completed => "Completed",
                    WannaDoState.Aborted => "Aborted",
                    WannaDoState.Expired => "Expired",
                    _ => string.Empty
                };
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
