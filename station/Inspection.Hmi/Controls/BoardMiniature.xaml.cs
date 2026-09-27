using System.Windows.Controls;

namespace Inspection.Hmi.Controls;

/// <summary>
/// 보드 미니어처. 데이터는 MainViewModel의 BoardCells·BoardParts에서 온다
/// (DataContext는 부모에게서 그대로 물려받는다).
/// </summary>
public partial class BoardMiniature : UserControl
{
    public BoardMiniature()
    {
        InitializeComponent();
    }
}
