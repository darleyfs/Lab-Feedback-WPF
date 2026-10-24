using Lab_Feedback_WPF.Models;
using Lab_Feedback_WPF.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
                ShowViolations();
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

            GradeOverviewVM.Load(GradebookService.Load(path, students.Select(s => s.Section)));
        }

        private async void LoadStudentAsync(Student? student)
        {
            if (student?.Folder == null) return;

            var assignments = await Submissions.PrepareAsync(student.Folder, _progressFactory);

            Assignments.Clear();
            foreach (var a in assignments)
                Assignments.Add(a);
        }

        // ─── Assignment / File Loading ────────────────────────────────────────

        private void LoadAssignment(Assignment? assignment)
        {
            ClearFileTabs();

            if (assignment?.Name == null || assignment.Folder == null) return;

            var submission = Submissions.Open(assignment);

            foreach (var file in submission.StudentFiles)
                FileTabs.Add(new FileTabViewModel(file));

            StatusBuilds = submission.BuildLog?.Count.ToString() ?? "N/A";
            StatusScore = submission.BuildLog?.LastOrDefault()?.Number.ToString() ?? "N/A";

            foreach (var problem in submission.Problems)
                MessageBox.Show(problem, assignment.Name);

            if (submission.PrimaryFile != null)
            {
                SelectedFileContent = File.ReadAllText(submission.PrimaryFile);
                IsEmptyState = false;

                var matchingTab = FileTabs.FirstOrDefault(t => t.FilePath == submission.PrimaryFile);
                if (matchingTab != null)
                {
                    if (_selectedTab != null) _selectedTab.IsSelected = false;
                    _selectedTab = matchingTab;
                    matchingTab.IsSelected = true;
                    OnPropertyChanged(nameof(SelectedTab));
                }
            }

            GradingVM.LoadResults(submission.Results ?? new LabResults(), _selectedStudent);

            _violations = ViolationCheck.Check(submission.StudentFiles
                .Where(File.Exists)
                .Select(f => KeyValuePair.Create(f, File.ReadAllText(f))));
            ShowViolations();
        }

        private void ClearFileTabs()
        {
            FileTabs.Clear();
            _selectedTab = null;
            SelectedFileContent = string.Empty;
            IsEmptyState = true;
            _violations = ViolationReport.Empty;
            ShowViolations();
            GradingVM.Clear();
        }

        // ─── Violations ───────────────────────────────────────────────────────

        private ViolationReport _violations = ViolationReport.Empty;

        private void ShowViolations()
        {
            ViolationLines = _selectedTab != null ? _violations.LinesIn(_selectedTab.FilePath) : Array.Empty<int>();
            ViolationCount = _violations.Total;
            ViolationTooltip = _violations.Summary;
            ViolationColor = _violations.Total switch
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
