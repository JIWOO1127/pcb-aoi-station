using System.Windows;
using Inspection.Hmi.ViewModels;

namespace Inspection.Hmi;

public partial class MainWindow : Window
{
    // ViewModel은 DI가 넣어준다. 코드 비하인드에는 로직을 두지 않는다.
    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
