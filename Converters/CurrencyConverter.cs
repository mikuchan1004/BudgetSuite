using System;
using System.Globalization;
using System.Windows.Data;

namespace BudgetSuite.Converters
{
    /// <summary>
    /// 숫자형(decimal/int) 금액 데이터와 천 단위 콤마(,)가 포함된 문자열 UI 간의 양방향 변환기
    /// </summary>
    public class CurrencyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decValue)
            {
                return decValue.ToString("N0");
            }
            if (value is int intValue)
            {
                return intValue.ToString("N0");
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string strValue)
            {
                string cleaned = strValue.Replace(",", "").Trim();
                if (decimal.TryParse(cleaned, NumberStyles.Any, culture, out decimal result))
                {
                    if (targetType == typeof(int))
                        return (int)result;
                    return result;
                }
            }
            if (targetType == typeof(int))
                return 0;
            return 0m;
        }
    }
}
