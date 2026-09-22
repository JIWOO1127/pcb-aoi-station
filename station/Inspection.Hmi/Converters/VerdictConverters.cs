using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Inspection.Contracts;

namespace Inspection.Hmi.Converters;

/// <summary>판정 → 배경색</summary>
public class VerdictToBrushConverter : IValueConverter
{
    private static readonly Brush PassBrush = Frozen(0x2E, 0x7D, 0x32);    // 초록
    private static readonly Brush FailBrush = Frozen(0xC6, 0x28, 0x28);    // 빨강
    private static readonly Brush InvalidBrush = Frozen(0xEF, 0x6C, 0x00); // 주황
    private static readonly Brush IdleBrush = Frozen(0x61, 0x61, 0x61);    // 회색

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        Verdict.Pass => PassBrush,
        Verdict.Fail => FailBrush,
        Verdict.Invalid => InvalidBrush,
        _ => IdleBrush
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

/// <summary>판정 → 글자 (색만으로 구분하지 않기 위해)</summary>
public class VerdictToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        Verdict.Pass => "PASS",
        Verdict.Fail => "FAIL",
        Verdict.Invalid => "검사 불가",
        _ => "대기"
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>불량 종류 → 짧은 한글 (목록 표시용)</summary>
public class DefectTypeToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        DefectType.Missing => "누락",
        DefectType.Offset => "위치",
        DefectType.WrongPart => "오부품",
        DefectType.Extra => "추가",
        DefectType.WireColorSwap => "전선 색",
        _ => "정상"
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
