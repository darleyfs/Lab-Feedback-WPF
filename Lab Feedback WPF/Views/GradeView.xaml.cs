using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Lab_Feedback_WPF.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Lab_Feedback_WPF.Views
{
    public partial class GradingView : UserControl
    {
        private IHighlightingDefinition? _htmlHighlighting;
        private ICSharpCode.AvalonEdit.TextEditor? _remarksEditor;
        private GradingViewModel? _vm;

        public GradingView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is GradingViewModel oldVm)
            {
                oldVm.PropertyChanged -= OnVmPropertyChanged;
                oldVm.FeedbackCopied -= OnFeedbackCopied;
            }

            _vm = e.NewValue as GradingViewModel;

            if (_vm == null) return;

            _vm.PropertyChanged += OnVmPropertyChanged;
            _vm.FeedbackCopied += OnFeedbackCopied;

            // Create the remarks editor once and insert it
            if (_remarksEditor == null)
            {
                _remarksEditor = CreateHtmlEditor();
                _remarksEditor.Document.Changed += (_, _) =>
                {
                    if (_vm != null && _vm.Remarks != _remarksEditor.Text)
                        _vm.Remarks = _remarksEditor.Text;
                };
                remarksExpander.Content = _remarksEditor;
            }

            _remarksEditor.Text = _vm.Remarks;
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GradingViewModel.Remarks) && _remarksEditor != null)
            {
                if (_remarksEditor.Text != _vm!.Remarks)
                    _remarksEditor.Text = _vm.Remarks;
            }
        }

        private void OnFeedbackCopied(object? sender, EventArgs e)
        {
            copyFeedbackButton.Content = "Copied!";
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (_, _) =>
            {
                copyFeedbackButton.Content = "Copy Feedback";
                timer.Stop();
            };
            timer.Start();
        }

        private ICSharpCode.AvalonEdit.TextEditor CreateHtmlEditor(string initialText = "")
        {
            if (_htmlHighlighting == null)
            {
                var uri = new Uri("pack://application:,,,/Resources/Html.xshd");
                using var stream = Application.GetResourceStream(uri)?.Stream;
                if (stream != null)
                {
                    using var reader = new System.Xml.XmlTextReader(stream);
                    _htmlHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }

            var editor = new ICSharpCode.AvalonEdit.TextEditor
            {
                Text = initialText,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(26, 26, 26)),
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(248, 248, 242)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(63, 63, 70)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4),
                MinHeight = 80,
                ShowLineNumbers = false,
                SyntaxHighlighting = _htmlHighlighting,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                WordWrap = true,
                Margin = new Thickness(0, 4, 0, 0)
            };

            editor.TextArea.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(26, 26, 26));

            return editor;
        }
    }
}
