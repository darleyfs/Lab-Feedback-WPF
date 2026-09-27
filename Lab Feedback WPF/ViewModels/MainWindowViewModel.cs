using Lab_Feedback_WPF.Models;
using Lab_Feedback_WPF.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Lab_Feedback_WPF.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private Student? _selectedStudent;
        private Assignment? _selectedAssignment;
        private FileTabViewModel? _selectedTab;
        private string _selectedFileContent = string.Empty;
        private bool _isSidePanelOpen;
        private bool _isEmptyState = true;
        private bool _isGradeOverviewVisible;
        private string _statusBuilds = "0";
        private string _statusScore = "0";
        private int _violationCount;
        private string _violationTooltip = string.Empty;
        private Brush _violationColor;
        private IEnumerable<int> _violationLines = Enumerable.Empty<int>();

        private readonly Func<IExtractionProgress>? _progressFactory;

        public ObservableCollection<Student> Students { get; } = new();
        public ObservableCollection<Assignment> Assignments { get; } = new();
        public ObservableCollection<FileTabViewModel> FileTabs { get; } = new();

        public GradingViewModel GradingVM { get; } = new();
        public GradeOverviewViewModel GradeOverviewVM { get; } = new();

        public MainWindowViewModel(Func<IExtractionProgress>? progressFactory = null)
        {
            _progressFactory = progressFactory;
            _violationColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));

            OpenFolderCommand = new RelayCommand(OpenFolder);
            RefreshCommand = new RelayCommand(Refresh, () => _lastOpenedPath != null);
            ExitCommand = new RelayCommand(() => Application.Current.Shutdown());
            OpenStudentInExplorerCommand = new RelayCommand(OpenStudentInExplorer);
            CopyStudentInfoCommand = new RelayCommand(CopyStudentInfo);
            OpenAssignmentInExplorerCommand = new RelayCommand(OpenAssignmentInExplorer);
            ToggleSidePanelCommand = new RelayCommand(() => IsSidePanelOpen = !IsSidePanelOpen);
            SelectTabCommand = new RelayCommand<FileTabViewModel>(tab =>
            {
                if (tab != null) SelectedTab = tab;
            });
        }

        // ─── Properties ───────────────────────────────────────────────────────

        public Student? SelectedStudent
        {
            get => _selectedStudent;
            set
            {
                if (SetField(ref _selectedStudent, value))
                    LoadStudentAsync(value);
            }
        }

        public Assignment? SelectedAssignment
        {
            get => _selectedAssignment;
            set
            {
                if (SetField(ref _selectedAssignment, value))
                    LoadAssignment(value);
            }
        }

        public FileTabViewModel? SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (_selectedTab != null) _selectedTab.IsSelected = false;
                SetField(ref _selectedTab, value);
                if (value != null)
                {
                    value.IsSelected = true;
                    SelectedFileContent = File.ReadAllText(value.FilePath);
                    IsEmptyState = false;
                    IsGradeOverviewVisible = false;
                }
                UpdateViolations();
            }
        }

        public string SelectedFileContent
        {
            get => _selectedFileContent;
            private set => SetField(ref _selectedFileContent, value);
        }

        public bool IsSidePanelOpen
        {
            get => _isSidePanelOpen;
            set => SetField(ref _isSidePanelOpen, value);
        }

        public bool IsEmptyState
        {
            get => _isEmptyState;
            set => SetField(ref _isEmptyState, value);
        }

        public bool IsGradeOverviewVisible
        {
            get => _isGradeOverviewVisible;
            set => SetField(ref _isGradeOverviewVisible, value);
        }

        public string StatusBuilds
        {
            get => _statusBuilds;
            set => SetField(ref _statusBuilds, value);
        }

        public string StatusScore
        {
            get => _statusScore;
            set => SetField(ref _statusScore, value);
        }

        public int ViolationCount
        {
            get => _violationCount;
            set => SetField(ref _violationCount, value);
        }

        public string ViolationTooltip
        {
            get => _violationTooltip;
            set => SetField(ref _violationTooltip, value);
        }

        public Brush ViolationColor
        {
            get => _violationColor;
            set => SetField(ref _violationColor, value);
        }

        public IEnumerable<int> ViolationLines
        {
            get => _violationLines;
            private set => SetField(ref _violationLines, value);
        }

        // ─── Commands ─────────────────────────────────────────────────────────

        public ICommand OpenFolderCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand OpenStudentInExplorerCommand { get; }
        public ICommand CopyStudentInfoCommand { get; }
        public ICommand OpenAssignmentInExplorerCommand { get; }
        public ICommand ToggleSidePanelCommand { get; }
        public ICommand SelectTabCommand { get; }

        // ─── Violation Terms ──────────────────────────────────────────────────

        private static readonly List<string> ViolationTerms = new()
        {
            "&", "const", "static_cast", "reinterpret_cast", "dynamic_cast",
            "resize()", "ignore()", "clear()", "auto", "try", "size_t", "goto",
            "catch", "\0", "(...)", "var", "continue", "iterator"
        };

        private static readonly List<string> FileExclusions = new()
        {
            "DONOTUSEANYTHINGINTHISFILE.h", "Source.h", "Helper.cpp", "Helper.h",
            "Source.cpp", "Test.cpp", "Test.h", "Tester.cpp", "Tester.h",
            "Utility.cpp", "Utility.h", "UI.h", "ShopUtils.cpp", "ShopUtils.h",
            "LabUI.h", "resource.h", "LLMChecker.h", "ProgressBar.h", "Result.h",
            "Results.h", "ResultsLib.h", "LabTestUtils.h", "Console.h", "Console.cpp"
        };

        // ─── Folder / Student Loading ─────────────────────────────────────────

        private string? _lastOpenedPath;

        private void OpenFolder()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            LoadFolder(dialog.SelectedPath);
        }

        private void Refresh()
        {
            if (_lastOpenedPath != null)
                LoadFolder(_lastOpenedPath);
        }

        private void LoadFolder(string path)
        {
            _lastOpenedPath = path;

            var students = Student.GetStudentsFromFolders(path)
                .OrderBy(s => s.Section, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.LastName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Students.Clear();
            foreach (var s in students)
                Students.Add(s);

            // Sections come from student folders and from any subfolder holding a gradebook CSV,
            // so the overview lists every gradebook student even without submission folders.
            var sections = students
                .Select(s => s.Section)
                .Where(s => !string.IsNullOrEmpty(s))
                .Concat(GradebookService.FindSectionsWithGradebooks(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (sections.Count > 0)
                GradeOverviewVM.Load(path, sections);
            else
                GradeOverviewVM.Clear();
        }

        private async void LoadStudentAsync(Student? student)
        {
            if (student?.Folder == null) return;
            var path = student.Folder;

            var hasZipFiles = Directory.Exists(path) &&
                              Directory.GetFiles(path, "*.zip", SearchOption.AllDirectories).Length > 0;

            if (hasZipFiles)
            {
                var reporter = _progressFactory?.Invoke();

                var submissionFolders = Directory.GetDirectories(path)
                    .Where(d => Path.GetFileName(d)
                        .StartsWith("submission_", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(d => d)
                    .ToList();

                if (submissionFolders.Any())
                {
                    foreach (var folder in submissionFolders)
                        await ZipFileHandler.ExtractZipFilesInFolderWithProgressAsync(folder, reporter);
                }
                else
                {
                    await ZipFileHandler.ExtractZipFilesInFolderWithProgressAsync(path, reporter);
                }

                if (reporter?.IsVisible == true)
                    reporter.Close();
            }

            var hasSubmissionFolders = Directory.Exists(path) &&
                Directory.GetDirectories(path)
                    .Any(d => Path.GetFileName(d)
                        .StartsWith("submission_", StringComparison.OrdinalIgnoreCase));

            if (hasSubmissionFolders)
                Assignment.ConsolidateSubmissions(path);

            var assignments = Assignment.FindLabOrPracticalSubfolders(path)
                .Where(a => !a.Folder!.Split(Path.DirectorySeparatorChar).Any(seg => seg == ".vs"));

            Assignments.Clear();
            foreach (var a in assignments)
                Assignments.Add(a);
        }

        // ─── Assignment / File Loading ────────────────────────────────────────

        private void LoadAssignment(Assignment? assignment)
        {
            ClearFileTabs();

            if (assignment?.Name == null || assignment.Folder == null) return;
            var name = assignment.Name;
            var path = assignment.Folder;

            string? primaryFilePath = null;
            var exclusions = new List<string>(FileExclusions);

            if (name.StartsWith("Lab ") || name.StartsWith("Midterm") || name.StartsWith("Final"))
            {
                PopulateFileTabs(name, path, exclusions);

                if (name.StartsWith("Lab "))
                {
                    var swh = FileHandler.SearchFile(path, "StudentWork.h");
                    var sub = FileHandler.SearchFile(path, "Submission.cpp");
                    if (swh != "") primaryFilePath = swh;
                    if (sub != "") primaryFilePath = sub;
                }
                else if (name.StartsWith("Midterm"))
                    primaryFilePath = FileHandler.SearchFile(path, "HighScore_Table.cpp");
                else if (name.StartsWith("Final"))
                    primaryFilePath = FileHandler.SearchFile(path, "RPG_Shop.cpp");
            }

            if (!string.IsNullOrEmpty(primaryFilePath))
            {
                SelectedFileContent = File.ReadAllText(primaryFilePath);
                IsEmptyState = false;

                var matchingTab = FileTabs.FirstOrDefault(t => t.FilePath == primaryFilePath);
                if (matchingTab != null)
                {
                    if (_selectedTab != null) _selectedTab.IsSelected = false;
                    _selectedTab = matchingTab;
                    matchingTab.IsSelected = true;
                    OnPropertyChanged(nameof(SelectedTab));
                }
            }
            else
            {
                MessageBox.Show("There may be a problem with the folder structure.", "Error opening file path.");
                IsEmptyState = true;
            }

            var labResults = FileHandler.LoadLabResults(path);
            GradingVM.LoadResults(labResults ?? new LabResults(), _selectedStudent);
            UpdateViolations();
        }

        private void ClearFileTabs()
        {
            FileTabs.Clear();
            _selectedTab = null;
            SelectedFileContent = string.Empty;
            IsEmptyState = true;
            ViolationLines = Enumerable.Empty<int>();
            GradingVM.Clear();
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

            foreach (var fp in FileHandler.SearchHeaderFiles(path, exclusions))
                FileTabs.Add(new FileTabViewModel(fp));
        }

        private void SetResultStatusLabels(string resultsPath, string projectName)
        {
            if (resultsPath.Length > 0)
            {
                var results = FileHandler.ParseFile(resultsPath);
                StatusBuilds = results.Count.ToString();
                StatusScore = results.Count > 0 ? results[^1].Number.ToString() : "N/A";
            }
            else
            {
                MessageBox.Show($"Build logs not found for {projectName}.");
                StatusBuilds = "N/A";
                StatusScore = "N/A";
                ViolationCount = 0;
            }
        }

        // ─── Violations ───────────────────────────────────────────────────────

        private void UpdateViolations()
        {
            var matcher = new ViolationsMatcher(ViolationTerms);
            var allViolations = new List<Violation>();

            foreach (var tab in FileTabs)
            {
                if (!File.Exists(tab.FilePath)) continue;
                var text = File.ReadAllText(tab.FilePath);
                foreach (var v in matcher.GetViolations(text))
                    allViolations.Add(new Violation(
                        v.LineNumber, v.MatchedText, v.Context,
                        $"{tab.FileName} — {v.LineContent}"));
            }

            if (_selectedTab != null && File.Exists(_selectedTab.FilePath))
            {
                var visibleText = File.ReadAllText(_selectedTab.FilePath);
                ViolationLines = matcher.GetViolations(visibleText).Select(v => v.LineNumber).ToList();
            }
            else
            {
                ViolationLines = Enumerable.Empty<int>();
            }

            ViolationCount = allViolations.Count;

            if (allViolations.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Found {allViolations.Count} violation(s):\n");
                foreach (var group in allViolations.GroupBy(v => v.LineContent.Split('—')[0].Trim()))
                {
                    sb.AppendLine($"── {group.Key}");
                    foreach (var v in group)
                        sb.AppendLine($"  Line {v.LineNumber}: '{v.MatchedText}'  {v.Context}");
                    sb.AppendLine();
                }
                ViolationTooltip = sb.ToString().TrimEnd();
            }
            else
            {
                ViolationTooltip = string.Empty;
            }

            ViolationColor = allViolations.Count switch
            {
                > 3 => Brushes.Red,
                > 0 => Brushes.Orange,
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"))
            };
        }

        // ─── Context Menu Actions ─────────────────────────────────────────────

        private void OpenStudentInExplorer()
        {
            if (_selectedStudent == null) return;
            if (!string.IsNullOrEmpty(_selectedStudent.Folder) && Directory.Exists(_selectedStudent.Folder))
                Process.Start("explorer.exe", _selectedStudent.Folder);
            else
                MessageBox.Show("Invalid file path or file does not exist.", "Error");
        }

        private void CopyStudentInfo()
        {
            if (_selectedStudent == null) return;
            Clipboard.SetText($"{_selectedStudent.FirstName} {_selectedStudent.LastName}\t{_selectedStudent.IdNumber}");
        }

        private void OpenAssignmentInExplorer()
        {
            if (_selectedAssignment == null) return;
            if (!string.IsNullOrEmpty(_selectedAssignment.Folder) && Directory.Exists(_selectedAssignment.Folder))
                Process.Start("explorer.exe", _selectedAssignment.Folder);
            else
                MessageBox.Show("Invalid folder path or folder does not exist.", "Error");
        }
    }
}
