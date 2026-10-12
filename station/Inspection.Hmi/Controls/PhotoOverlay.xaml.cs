using System.Windows.Controls;

namespace Inspection.Hmi.Controls;

/// <summary>실제 사진 위 불량 표시. 데이터는 MainViewModel의 OverlayImage·OverlayParts·ModelBoxes.</summary>
public partial class PhotoOverlay : UserControl
{
    public PhotoOverlay()
    {
        InitializeComponent();
    }
}
