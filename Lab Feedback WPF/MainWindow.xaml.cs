using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Lab_Feedback_WPF.Models;
using Lab_Feedback_WPF.Services;
using Lab_Feedback_WPF.Views;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;
//using WpfButton = System.Windows.Controls.Button;
//using WpfTextBox = System.Windows.Controls.TextBox;
//using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfButton = Wpf.Ui.Controls.Button;
using WpfTextBox = Wpf.Ui.Controls.TextBox;
using WpfTextBlock = Wpf.Ui.Controls.TextBlock;


namespace Lab_Feedback_WPF
{
    public partial class MainWindow : FluentWindow
    {
        private readonly GradingView _gradingView = new();

        private WpfButton? _selectedTabButton;
        private ExtractionProgressDialog? _progressDialog;
        private ViolationHighlighter _violationHighlighter = null!;
        private ToggleButton? _activeTab;
        private double _sidePanelWidth = 500;


        public MainWindow()
        {
            InitializeComponent();
            LoadMonokaiTheme();
        }

        // ─── Theme ────────────────────────────────────────────────────────────

        private void LoadMonokaiTheme()
        {
            _violationHighlighter = new ViolationHighlighter(codeEditor);

            var uri = new Uri("pack://application:,,,/Resources/Monokai.xshd");
            using var stream = Application.GetResourceStream(uri)?.Stream;
            if (stream == null) return;

            using var reader = new System.Xml.XmlTextReader(stream);
            codeEditor.SyntaxHighlighting = HighlightingLoader
                .Load(reader, HighlightingManager.Instance);

            codeEditor
                .TextArea
                .TextView
                .BackgroundRenderers
                .Add(_violationHighlighter);
        }

        // ─── Folder / Student Loading ─────────────────────────────────────────

        private void OpenFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Debug.WriteLine("=== OpenFolderMenuItem_Click fired ===");

            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            var result = dialog.ShowDialog();

            // Debug.WriteLine($"Dialog result: {result}");

            if (result != System.Windows.Forms.DialogResult.OK) return;

            // Debug.WriteLine($"Selected path: {dialog.SelectedPath}");

            var students = Student.GetStudentsFromFolders(dialog.SelectedPath);

            // Debug.WriteLine($"Students found: {students?.Count() ?? 0}");

            listBoxStudents.Items.Clear();
            foreach (var student in students)
            {
                Debug.WriteLine($"Adding student: {student.FullName} | Folder: {student.Folder}");
                listBoxStudents.Items.Add(student);
            }

            // Debug.WriteLine($"ListBox item count after population: {listBoxStudents.Items.Count}");
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // ─── Student Selection ────────────────────────────────────────────────

        private async void ListBoxStudents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listBoxStudents.SelectedItem is not Student student) return;

            var path = student.Folder;
            if (path == null) return;

            var hasZipFiles = Directory.Exists(path) &&
                              Directory.GetFiles(path, "*.zip", SearchOption.AllDirectories).Length > 0;

            if (hasZipFiles)
            {
                var progressDialog = GetOrCreateProgressDialog();

                var submissionFolders = Directory.GetDirectories(path)
                    .Where(d => Path.GetFileName(d)
                        .StartsWith("submission_", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(d => d)
                    .ToList();

                if (submissionFolders.Any())
                {
                    foreach (var submissionFolder in submissionFolders)
                        await ZipFileHandler.ExtractZipFilesInFolderWithProgressAsync(
                            submissionFolder, progressDialog);
                }
                else
                {
                    await ZipFileHandler.ExtractZipFilesInFolderWithProgressAsync(
                        path, progressDialog);
                }

                if (_progressDialog?.IsVisible == true)
                    _progressDialog.Close();
            }

            // Always consolidate if submission folders exist regardless of whether zips were extracted
            var hasSubmissionFolders = Directory.Exists(path) &&
                                       Directory.GetDirectories(path)
                                           .Any(d => Path.GetFileName(d)
                                               .StartsWith("submission_", StringComparison.OrdinalIgnoreCase));

            if (hasSubmissionFolders)
            {
                Debug.WriteLine("=== Running ConsolidateSubmissions ===");
                Assignment.ConsolidateSubmissions(path);
            }

            var assignments = Assignment.FindLabOrPracticalSubfolders(path)
                .Where(a => !a.Folder!
                    .Split(Path.DirectorySeparatorChar)
                    .Any(segment => segment == ".vs"));

            listBoxAssignments.Items.Clear();
            foreach (var assignment in assignments)
                listBoxAssignments.Items.Add(assignment);
        }

        private ExtractionProgressDialog GetOrCreateProgressDialog()
        {
            if (_progressDialog == null || !_progressDialog.IsVisible)
            {
                _progressDialog = new ExtractionProgressDialog
                {
                    Owner = this
                };

                _progressDialog.Closed += (s, e) =>
                {
                    this.Activate();
                    this.Focus();
                };

                _progressDialog.Show();
            }
            return _progressDialog;
        }

        // ─── Assignment Selection ─────────────────────────────────────────────

        private void ListBoxAssignments_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ClearFileTabs();

            if (listBoxAssignments.SelectedItem is not Assignment selectedAssignment) return;

            var name = selectedAssignment.Name;
            var path = selectedAssignment.Folder;
            if (name == null || path == null) return;

            string? filePath = null;

            var exclusions = new List<string>
            {
                "DONOTUSEANYTHINGINTHISFILE.h", "Source.h", "Helper.cpp", "Helper.h",
                "Source.cpp", "Test.cpp", "Test.h", "Tester.cpp", "Tester.h",
                "Utility.cpp", "Utility.h", "UI.h", "ShopUtils.cpp", "ShopUtils.h",
                "LabUI.h", "resource.h", "LLMChecker.h", "ProgressBar.h", "Result.h",
                "Results.h", "ResultsLib.h", "LabTestUtils.h", "Console.h", "Console.cpp"
            };

            if (name.StartsWith("Lab ") || name.StartsWith("Midterm") || name.StartsWith("Final"))
            {
                PopulateFileTabs(name, path, exclusions);

                if (name.StartsWith("Lab "))
                {
                    if (FileHandler.SearchFile(path, "StudentWork.h") != "")
                        filePath = FileHandler.SearchFile(path, "StudentWork.h");

                    if (FileHandler.SearchFile(path, "Submission.cpp") != "")
                        filePath = FileHandler.SearchFile(path, "Submission.cpp");
                }

                if (name.StartsWith("Midterm"))
                    filePath = FileHandler.SearchFile(path, "HighScore_Table.cpp");

                if (name.StartsWith("Final"))
                    filePath = FileHandler.SearchFile(path, "RPG_Shop.cpp");
            }

            if (string.IsNullOrEmpty(path)) return;

            if (!string.IsNullOrEmpty(filePath))
            {
                codeEditor.Text = File.ReadAllText(filePath);
                SetEmptyState(false);
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "There may be a problem with the folder structure.",
                    "Error opening file path.");
                SetEmptyState(true);
            }

            // Load JSON results and populate grade sheet form
            var labResults = FileHandler.LoadLabResults(path);
            var selectedStudent = listBoxStudents.SelectedItem as Student;
            _gradingView.LoadResults(labResults ?? new LabResults(), selectedStudent);

            UpdateViolationsStatus();
        }

        // ─── File Tabs ────────────────────────────────────────────────────────

        private void ClearFileTabs()
        {
            panelFileTabs.Children.Clear();
            _selectedTabButton = null;
            codeEditor.Text = string.Empty;
            _violationHighlighter.Clear();
            _gradingView.Clear();
            SetEmptyState(true);
        }

        private void PopulateFileTabs(string name, string path, List<string> exclusions)
        {
            var resultsPath = FileHandler.SearchFile(path, "hdkvkt.txt");

            if (name.StartsWith("Lab"))
            {
                exclusions.Add(name + ".cpp");
                exclusions.Add(name.Replace(" ", "") + ".cpp");
                SetResultStatusLabels(resultsPath, name);
            }

            var filePaths = FileHandler.SearchHeaderFiles(path, exclusions);
            var first = true;

            foreach (var fp in filePaths)
            {
                var btn = CreateFileTab(fp, first);
                panelFileTabs.Children.Add(btn);

                if (first)
                {
                    SelectTab(btn);
                    first = false;
                }
            }

            // Count violations across all loaded files
            UpdateViolationsStatus();
        }

        private void SetEmptyState(bool isEmpty)
        {
            emptyStateOverlay.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        private WpfButton CreateFileTab(string filePath, bool first = false)
        {
            var btn = new WpfButton
            {
                Content = Path.GetFileName(filePath),
                Tag = filePath,
                Margin = new Thickness(2, 0, 2, 0),
                Background = first
                    ? new SolidColorBrush(Color.FromArgb(40, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(255, 39, 40, 34))
            };

            btn.Click += FileTab_Click;
            return btn;
        }

        private void FileTab_Click(object sender, RoutedEventArgs e)
            => SelectTab((WpfButton)sender);

        private void SelectTab(WpfButton btn)
        {
            if (_selectedTabButton != null)
                _selectedTabButton.Background =
                    new SolidColorBrush(Color.FromArgb(255, 39, 40, 34));

            _selectedTabButton = btn;
            btn.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));

            var path = (string)btn.Tag!;
            codeEditor.Text = File.ReadAllText(path);
            SetEmptyState(false);
            UpdateViolationsStatus();
        }

        // ─── Status Bar ───────────────────────────────────────────────────────

        private void UpdateViolationsStatus()
        {
            var violationTerms = new List<string>
            {
                "&", "const", "static_cast", "reinterpret_cast", "dynamic_cast",
                "resize()", "ignore()", "clear()", "auto", "try", "size_t", "goto",
                "catch", "\0", "(...)", "var", "continue", "iterator"
            };

            var matcher = new ViolationsMatcher(violationTerms);

            // Collect text from all loaded tabs, not just the visible one
            var allViolations = new List<Violation>();
            foreach (WpfButton tab in panelFileTabs.Children)
            {
                var path = (string)tab.Tag!;
                if (!File.Exists(path)) continue;

                var text = File.ReadAllText(path);
                var violations = matcher.GetViolations(text);

                // Tag each violation with its source file for the tooltip
                foreach (var v in violations)
                    allViolations.Add(new Violation(
                        v.LineNumber,
                        v.MatchedText,
                        v.Context,
                        $"{Path.GetFileName(path)} — {v.LineContent}"));
            }

            // Highlight violations only in the currently visible file
            var visibleViolations = allViolations
                .Where(v => v.LineContent.Contains(
                    _selectedTabButton != null
                        ? Path.GetFileName((string)_selectedTabButton.Tag!)
                        : string.Empty))
                .ToList();

            // Actually re-run for visible file to get correct line numbers
            if (_selectedTabButton != null)
            {
                var visiblePath = (string)_selectedTabButton.Tag!;
                var visibleText = File.ReadAllText(visiblePath);
                var visibleFileViolations = matcher.GetViolations(visibleText);
                _violationHighlighter.SetViolationLines(
                    visibleFileViolations.Select(v => v.LineNumber));
            }
            else
            {
                _violationHighlighter.Clear();
            }

            statusViolations.Text = allViolations.Count.ToString();

            if (allViolations.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Found {allViolations.Count} violation(s):\n");

                // Group by file for readability
                var byFile = allViolations
                    .GroupBy(v => v.LineContent.Split('—')[0].Trim());

                foreach (var group in byFile)
                {
                    sb.AppendLine($"── {group.Key}");
                    foreach (var v in group)
                        sb.AppendLine($"  Line {v.LineNumber}: '{v.MatchedText}'  {v.Context}");
                    sb.AppendLine();
                }

                statusViolations.ToolTip = new ToolTip
                {
                    Content = new WpfTextBlock
                    {
                        Text = sb.ToString().TrimEnd(),
                        FontFamily = new FontFamily("Consolas"),
                        MaxWidth = 600,
                        TextWrapping = TextWrapping.Wrap
                    },
                    MaxWidth = 620
                };
            }
            else
            {
                statusViolations.ToolTip = null;
            }

            statusViolations.Foreground = allViolations.Count switch
            {
                > 3 => Brushes.Red,
                > 0 => Brushes.Orange,
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"))
            };
        }

        // TODO: Remove?
        private List<int> GetViolationLineNumbers(List<string> violations)
        {
            var lines = new HashSet<int>();
            var docLines = codeEditor.Text.Split('\n');

            for (int i = 0; i < docLines.Length; i++)
            {
                var line = docLines[i];
                if (violations.Any(v => line.Contains(v)))
                    lines.Add(i + 1); // AvalonEdit lines are 1-indexed
            }

            return lines.ToList();
        }

        private void SetResultStatusLabels(string resultsPath, string projectName)
        {
            if (resultsPath.Length > 0)
            {
                var results = FileHandler.ParseFile(resultsPath);
                statusBuilds.Text = results.Count.ToString();
                statusScore.Text = results.Count > 0 ? results[^1].Number.ToString() : "N/A";
            }
            else
            {
                System.Windows.MessageBox.Show(
                    $"Build logs not found for {projectName}.");
                statusBuilds.Text = "N/A";
                statusScore.Text = "N/A";
                statusViolations.Text = "0";
            }
        }

        // ─── Context Menu (Students ListBox) ──────────────────────────────────

        private void OpenInFileExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (listBoxStudents.SelectedItem is not Student student) return;

            if (!string.IsNullOrEmpty(student.Folder) && Directory.Exists(student.Folder))
                Process.Start("explorer.exe", student.Folder);
            else
                System.Windows.MessageBox.Show(
                    "Invalid file path or file does not exist.", "Error");
        }

        private void CopyStudentNameAndNumber_Click(object sender, RoutedEventArgs e)
        {
            if (listBoxStudents.SelectedItem is not Student student) return;
            Clipboard.SetText($"{student.FirstName} {student.LastName}\t{student.IdNumber}");
        }

        private void ListBoxItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item)
            {
                item.IsSelected = true;
                e.Handled = false;
            }
        }

        // ─── Context Menu (Assignments ListBox) ──────────────────────────────────

        private void OpenAssignmentInFileExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (listBoxAssignments.SelectedItem is not Assignment assignment) return;

            if (!string.IsNullOrEmpty(assignment.Folder) && Directory.Exists(assignment.Folder))
                Process.Start("explorer.exe", assignment.Folder);
            else
                System.Windows.MessageBox.Show(
                    "Invalid folder path or folder does not exist.", "Error");
        }


        // ─── Open in New Window ───────────────────────────────────────────────

        private void OpenNewWindowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // new FormCodeView(codeEditor.Text, "test.cpp").Show();
        }

        private void OpenSidePanel()
        {
            sidePanelSplitter.Visibility = Visibility.Visible;
            sidePanelColumn.Width = new GridLength(_sidePanelWidth);
        }

        private void CloseSidePanel()
        {
            // Save current width before closing
            _sidePanelWidth = sidePanelColumn.Width.Value > 0
                ? sidePanelColumn.Width.Value
                : 260;

            sidePanelSplitter.Visibility = Visibility.Collapsed;
            sidePanelColumn.Width = new GridLength(0);
        }

        //private void ButtonOpenGrading_Click(object sender, RoutedEventArgs e)
        //{
        //    if (tabGrading.IsChecked == true)
        //    {
        //        tabGrading.IsChecked = false;
        //    }
        //    else
        //    {
        //        tabGrading.IsChecked = true;
        //    }
        //}

        private void ButtonOpenGrading_Click(object sender, RoutedEventArgs e)
        {
            if (sidePanelColumn.Width.Value > 0)
            {
                _sidePanelWidth = sidePanelColumn.Width.Value;
                sidePanelSplitter.Visibility = Visibility.Collapsed;
                sidePanelColumn.Width = new GridLength(0);
                sidePanelPresenter.Content = null;
            }
            else
            {
                sidePanelPresenter.Content = _gradingView;
                sidePanelSplitter.Visibility = Visibility.Visible;
                sidePanelColumn.Width = new GridLength(_sidePanelWidth);
            }
        }
    }
}