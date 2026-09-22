using System.Collections;
using System.Globalization;
using System.Windows.Data;
using Inspection.Contracts;

namespace Inspection.Hmi.Converters;

/// <summary>홀 목록(List&lt;Hole&gt;)을 "A0, A2" 같은 문자열로 바꿔 표에 표시.</summary>
public class HoleListConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IEnumerable holes and not string)
            return string.Join(", ", holes.Cast<object>().Select(h => h?.ToString()));
        return "-";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}