﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fix_My_Chrome
{
    // Credit: http://stackoverflow.com/questions/23046565/wpf-radial-progressbar-meter-i-e-battery-meter
    public class ProgressToAngleConverter : System.Windows.Data.IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double progress = (double)values[0];
            System.Windows.Controls.ProgressBar bar = values[1] as System.Windows.Controls.ProgressBar;

            return 359.999 * (progress / (bar.Maximum - bar.Minimum));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
