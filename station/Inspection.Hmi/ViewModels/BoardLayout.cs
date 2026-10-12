using Inspection.Contracts;

namespace Inspection.Hmi.ViewModels;

/// <summary>
/// 핀홀 좌표(A0~U13)를 미니어처 화면의 픽셀 위치로 바꾼다.
///
/// 브레드보드는 열이 등간격이 아니다. 전원 레일(0·1, 12·13)과 단자부(2~6, 7~11)
/// 사이, 그리고 가운데 홈(6↔7) 자리에 여백이 있다.
///
/// v13: 여백을 실제 보드 사진에서 잰 값으로 맞췄다 (구역 사이 ≈ 홀 간격의 1.8배).
/// 그래서 미니어처 모양이 실제 검사 영역처럼 거의 정사각형(가로/세로 ≈ 0.97)이 되고,
/// 나중에 배경을 실제 정렬 사진으로 바꿔도 표시 위치가 그대로 맞는다.
/// </summary>
public static class BoardLayout
{
    public const double CellSize = 18;   // 홀 간격 (픽셀)
    public const double HoleSize = 7;    // 홀 점 지름
    public const double Gap = 32;        // 구역 사이 추가 여백 (실측: 홀 간격 × 1.8)
    public const double MarginX = 22;    // 좌우 바깥 여백 (레일 선 자리)
    public const double MarginY = 14;    // 위아래 바깥 여백

    public const int Rows = 21;          // A~U (보드 번호 10~30)
    public const int Cols = 14;          // 0~13 (레일2 + A~E + F~J + 레일2)

    /// <summary>열 앞에 들어가는 누적 여백. 0·1 | 2~6 | 7~11 | 12·13</summary>
    private static double GapBefore(int col) => col switch
    {
        >= 12 => Gap * 3,
        >= 7 => Gap * 2,
        >= 2 => Gap * 1,
        _ => 0
    };

    public static double X(int col) => MarginX + col * CellSize + GapBefore(col);
    public static double Y(char row) => MarginY + (row - 'A') * CellSize;

    public static double TotalWidth => MarginX * 2 + Cols * CellSize + Gap * 3;   // 392
    public static double TotalHeight => MarginY * 2 + Rows * CellSize;             // 406

    /// <summary>실제로 존재하는 홀 전체 (전원 레일에는 E·K·Q 행이 없다).</summary>
    public static IEnumerable<Hole> AllHoles()
    {
        for (char row = 'A'; row <= 'U'; row++)
            for (int col = 0; col < Cols; col++)
            {
                var hole = new Hole(row, col);
                if (hole.IsValid()) yield return hole;
            }
    }
}
