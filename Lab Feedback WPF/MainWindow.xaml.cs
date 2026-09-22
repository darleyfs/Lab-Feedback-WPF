using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Lab_Feedback_WPF.Services;
using Lab_Feedback_WPF.ViewModels;
using Lab_Feedback_WPF.Views;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace Lab_Feedback_WPF
{
    public partial class MainWindow : FluentWindow
    {
        private readonly MainWindowViewModel _vm;
        private readonly GradingView _gradingView;
        private ViolationHighlighter _violationHighlighter = null!;
        private double _sidePanelWidth = 500;

        public MainWindow()
        {
            InitializeComponent();

            _vm = new MainWindowViewModel(CreateProgressDialog);
            DataContext = _vm;

            _gradingView = new GradingView { DataContext = _vm.GradingVM };

            LoadMonokaiTheme();
            WireViewModelEvents();
        }

        // ─── AvalonEdit Setup ─────────────────────────────────────────────────

        private void LoadMonokaiTheme()
        {
            _violationHighlighter = new ViolationHighlighter(codeEditor);

            var uri = new Uri("pack://application:,,,/Resources/Monokai.xshd");
            using var stream = Application.GetResourceStream(uri)?.Stream;
            if (stream == null) return;

            using var reader = new System.Xml.XmlTextReader(stream);
            codeEditor.SyntaxHighlighting = HighlightingLoader
                .Load(reader, HighlightingManager.Instance);

            codeEditor.TextArea.TextView.BackgroundRenderers.Add(_violationHighlighter);
        }

        // ─── ViewModel Event Wiring ───────────────────────────────────────────

        private void WireViewModelEvents()
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MainWindowViewModel.SelectedFileContent):
                    codeEditor.Text = _vm.SelectedFileContent;
                    break;

                case nameof(MainWindowViewModel.ViolationLines):
                    _violationHighlighter.SetViolationLines(_vm.ViolationLines);
                    break;

                case nameof(MainWindowViewModel.IsSidePanelOpen):
                    UpdateSidePanelLayout();
                    break;
            }
        }

        private void UpdateSidePanelLayout()
        {
            if (_vm.IsSidePanelOpen)
            {
                sidePanelPresenter.Content = _gradingView;
                sidePanelSplitter.Visibility = Visibility.Visible;
                sidePanelColumn.Width = new System.Windows.GridLength(_sidePanelWidth);
            }
            else
            {
                if (sidePanelColumn.Width.Value > 0)
                    _sidePanelWidth = sidePanelColumn.Width.Value;

                sidePanelSplitter.Visibility = Visibility.Collapsed;
                sidePanelColumn.Width = new System.Windows.GridLength(0);
                sidePanelPresenter.Content = null;
            }
        }

        // ─── Progress Dialog Factory ──────────────────────────────────────────

        private IExtractionProgress CreateProgressDialog()
        {
            var dialog = new ExtractionProgressDialog { Owner = this };
            dialog.Closed += (_, _) => { Activate(); Focus(); };
            dialog.Show();
            return dialog;
        }

        // ─── Right-click Selection ────────────────────────────────────────────

        private void ListBoxItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.ListBoxItem item)
            {
                item.IsSelected = true;
                e.Handled = false;
            }
        }
    }
}
