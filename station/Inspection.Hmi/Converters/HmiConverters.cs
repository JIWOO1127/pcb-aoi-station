using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Inspection.Contracts;
using Inspection.Hmi.ViewModels;

namespace Inspection.Hmi.Converters;

/// <summary>화면 상태 → 배너 배경색. 색의 의미를 화면 전체에서 일관되게 유지한다.</summary>
public class StateToBrushConverter : IValueConverter
{
    private static readonly Brush Neutral = Freeze("#3A3A3A");   // 대기·검사중
    private static readonly Brush Pass = Freeze("#2E7D32");      // 양품
    private static readonly Brush Fail = Freeze("#C62828");      // 불량
    private static readonly Brush Caution = Freeze("#EF6C00");   // 판정 불가·이상

    private static Brush Freeze(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    public object Convert(object? value, Type t, object? p, CultureInfo c) => value switch
    {
        ScreenState.Pass => Pass,
        ScreenState.Fail => Fail,
        ScreenState.Invalid or ScreenState.Error => Caution,
        _ => Neutral
    };

    public object ConvertBack(object? value, Type t, object? p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>불량 종류 → 짧은 한글 (목록 표시용).</summary>
public class DefectTypeToTextConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
        => MainViewModel.DescribeDefect(value as DefectType?);

    public object ConvertBack(object? value, Type t, object? p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>모델 클래스 이름(resistance470)을 작업자가 읽는 말(저항 470Ω)로.</summary>
public class ClassNameConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
        => MainViewModel.Korean(value as string);

    public object ConvertBack(object? value, Type t, object? p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>홀 목록을 "A0, A2" 문자열로.</summary>
public class HoleListConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        if (value is IEnumerable holes and not string)
        {
            var list = holes.Cast<object>().Select(h => h?.ToString()).ToList();
            return list.Count == 0 ? "-" : string.Join(", ", list);
        }
        return "-";
    }

    public object ConvertBack(object? value, Type t, object? p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>true면 보이고 false면 숨긴다. parameter="invert"면 반대.</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? parameter, CultureInfo c)
    {
        bool b = value is bool v && v;
        if (parameter as string == "invert") b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type t, object? p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>문자열이 비어 있으면 숨긴다.</summary>
public class EmptyStringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type t, object? p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>상태 점 색: null 회색(모름) / true 초록(정상) / false 빨강(이상).</summary>
public class StatusToBrushConverter : IValueConverter
{
    private static readonly Brush Unknown = Make("#757575");
    private static readonly Brush Ok = Make("#43A047");
    private static readonly Brush Bad = Make("#E53935");

    private static Brush Make(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    public object Convert(object? value, Type t, object? p, CultureInfo c) => value switch
    {
        true => Ok,
        false => Bad,
        _ => Unknown
    };

    public object ConvertBack(object? value, Type t, object? p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>
/// 부품 종류가 parameter와 같으면 보인다. parameter는 "red_wire|black_wire"처럼 여러 개 가능.
/// 미니어처에서 종류별 아이콘 하나만 보이게 할 때 쓴다.
/// </summary>
public class ClassIsConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? parameter, CultureInfo c)
    {
        var cls = value as string ?? "";
        var options = (parameter as string ?? "").Split('|');
        return options.Contains(cls) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type t, object? p, CultureInfo c)
        => throw new NotSupportedException();
}
