using Lab_Feedback_WPF.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace Lab_Feedback_WPF.Views
{
    public partial class ExtractionProgressDialog : FluentWindow, IExtractionProgress
    {
        private readonly Dictionary<string, ExtractionOperation> _operations = new();
        private readonly CancellationTokenSource _globalCts = new();

        public ExtractionProgressDialog()
        {
            InitializeComponent();
        }

        // ─── IExtractionProgress ──────────────────────────────────────────────

        public ExtractionOperationToken AddOperation(string id, string title, int totalFiles)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token);
            var operation = new ExtractionOperation(id, title, totalFiles, cts);

            Dispatcher.Invoke(() =>
            {
                _operations[id] = operation;
                progressItemsPanel.Children.Add(CreateOperationCard(operation));
                UpdateHeader();
            });

            return new ExtractionOperationToken(cts.Token);
        }

        public void UpdateOperation(string id, int current, string fileName)
        {
            Dispatcher.Invoke(() =>
            {
                if (!_operations.TryGetValue(id, out var op)) return;
                op.Current = current;
                op.CurrentFile = fileName;
                op.ProgressBar.Value = current;
                op.FileCountLabel.Text = $"{current} of {op.TotalFiles}";
                op.StatusLabel.Text = fileName;
            });
        }

        public void CompleteOperation(string id)
        {
            Dispatcher.Invoke(() =>
            {
                if (!_operations.TryGetValue(id, out var op)) return;
                op.StatusLabel.Text = "Complete";
                op.ProgressBar.Value = op.TotalFiles;
                op.CancelButton.IsEnabled = false;
                op.CancelButton.Content = "Done";
                op.IsComplete = true;
                op.Cts.Dispose();
                UpdateHeader();

                if (_operations.Values.All(o => o.IsComplete || o.IsCancelled))
                    Close();
            });
        }

        public void FailOperation(string id, string message)
        {
            Dispatcher.Invoke(() =>
            {
                if (!_operations.TryGetValue(id, out var op)) return;
                op.StatusLabel.Text = $"Error: {message}";
                op.StatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(249, 38, 114));
                op.CancelButton.IsEnabled = false;
                op.IsComplete = true;
                op.Cts.Dispose();
                UpdateHeader();
            });
        }

        // ─── UI Building ──────────────────────────────────────────────────────

        private UIElement CreateOperationCard(ExtractionOperation op)
        {
            var card = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var stack = new StackPanel();
            card.Child = stack;

            var titleRow = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };

            var cancelBtn = new Button
            {
                Content = "✕",
                Width = 20,
                Height = 20,
                Padding = new Thickness(0),
                FontSize = 10,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            cancelBtn.Click += (s, e) =>
            {
                op.Cts.Cancel();
                op.IsCancelled = true;
                cancelBtn.IsEnabled = false;
                cancelBtn.Content = "—";
                op.StatusLabel.Text = "Cancelled";
                op.StatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150));
                op.Cts.Dispose();
                UpdateHeader();

                if (_operations.Values.All(o => o.IsComplete || o.IsCancelled))
                    Close();
            };
            DockPanel.SetDock(cancelBtn, Dock.Right);
            titleRow.Children.Add(cancelBtn);
            op.CancelButton = cancelBtn;

            titleRow.Children.Add(new TextBlock
            {
                Text = op.Title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            stack.Children.Add(titleRow);

            var progressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = op.TotalFiles,
                Value = 0,
                Height = 4,
                Margin = new Thickness(0, 0, 0, 4)
            };
            op.ProgressBar = progressBar;
            stack.Children.Add(progressBar);

            var fileCountLabel = new TextBlock
            {
                Text = $"0 of {op.TotalFiles}",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                Margin = new Thickness(0, 0, 0, 2)
            };
            op.FileCountLabel = fileCountLabel;
            stack.Children.Add(fileCountLabel);

            var statusLabel = new TextBlock
            {
                Text = "Preparing...",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            op.StatusLabel = statusLabel;
            stack.Children.Add(statusLabel);

            return card;
        }

        private void UpdateHeader()
        {
            var active = _operations.Values.Count(o => !o.IsComplete && !o.IsCancelled);
            activeCountLabel.Text = active switch
            {
                0 => "All extractions complete",
                1 => "1 Active Extraction",
                _ => $"{active} Active Extractions"
            };
        }

        // ─── Buttons ──────────────────────────────────────────────────────────

        private void CancelAllButton_Click(object sender, RoutedEventArgs e)
        {
            _globalCts.Cancel();
            Close();
        }

        private void ExtractionProgressDialog_Closing(
            object sender, System.ComponentModel.CancelEventArgs e)
        {
            var hasActiveOperations = _operations.Values.Any(o => !o.IsComplete && !o.IsCancelled);
            if (hasActiveOperations)
            {
                e.Cancel = true;
            }
            else if (!_globalCts.IsCancellationRequested)
            {
                _globalCts.Cancel();
                _globalCts.Dispose();
            }
        }
    }

    // ─── Operation State ──────────────────────────────────────────────────────

    public class ExtractionOperation
    {
        public string Id { get; }
        public string Title { get; }
        public int TotalFiles { get; }
        public int Current { get; set; }
        public string CurrentFile { get; set; } = string.Empty;
        public bool IsComplete { get; set; }
        public bool IsCancelled { get; set; }
        public CancellationTokenSource Cts { get; }

        public ProgressBar ProgressBar { get; set; } = null!;
        public Wpf.Ui.Controls.TextBlock FileCountLabel { get; set; } = null!;
        public Wpf.Ui.Controls.TextBlock StatusLabel { get; set; } = null!;
        public Wpf.Ui.Controls.Button CancelButton { get; set; } = null!;

        public ExtractionOperation(string id, string title, int totalFiles, CancellationTokenSource cts)
        {
            Id = id;
            Title = title;
            TotalFiles = totalFiles;
            Cts = cts;
        }
    }
}
