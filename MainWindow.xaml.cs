using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace BudgetSuite
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            // 마우스 클릭 시 텍스트박스 포커스 해제 (LostFocus 바인딩 커밋용)
            PreviewMouseDown += MainWindow_PreviewMouseDown;
            PreviewKeyDown += MainWindow_PreviewKeyDown;

            // 창이 포커스를 얻을 때 모바일 등 외부 변경사항 즉시 동기화
            Activated += (s, e) => _viewModel.CheckForRemoteUpdates();
        }

        private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not System.Windows.Controls.TextBox)
            {
                Keyboard.ClearFocus();
            }
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 텍스트박스에서 Enter 키 누르면 포커스 이동하여 바인딩 즉시 갱신
            if (e.Key == Key.Enter && e.OriginalSource is System.Windows.Controls.TextBox)
            {
                Keyboard.ClearFocus();
            }
            // 표에서 항목 선택 후 Delete 키를 눌렀을 때도 선택 항목 삭제 즉시 실행
            else if (e.Key == Key.Delete && e.OriginalSource is not System.Windows.Controls.TextBox)
            {
                if (_viewModel.SelectedTabIndex == 0 && _viewModel.SelectedTodayExpense != null)
                {
                    _viewModel.DeleteTodayExpenseCommand.Execute(null);
                    e.Handled = true;
                }
                else if (_viewModel.SelectedTabIndex == 2 && _viewModel.SelectedHistoryExpense != null)
                {
                    _viewModel.DeleteHistoryExpenseCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _viewModel.SaveData();
        }
    }
}
