using Lab_Feedback_WPF.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace Lab_Feedback_WPF.ViewModels
{
    /// <summary>Binds the grading panel to a <see cref="FeedbackSheet"/>, which owns the grade rules.</summary>
    public class GradingViewModel : ViewModelBase
    {
        private readonly FeedbackSheet _sheet = new();
        private string _studentName = string.Empty;
        private string _remarks = string.Empty;
        private string _labTitle = string.Empty;
        private string _totalScore = string.Empty;
        private string _totalPossible = string.Empty;
        private SolidColorBrush _totalScoreColor = ScoreBrushes.For(ScoreBand.Full);
        private bool _isPlaceholderVisible = true;
        private bool _hasResults;
        private string _lastAutoRemarks = string.Empty;
        private Student? _student;

        public ObservableCollection<DeductionViewModel> Deductions { get; } = new();
        public ObservableCollection<TestResultViewModel> TestResults { get; } = new();

        public string StudentName
        {
            get => _studentName;
            set => SetField(ref _studentName, value);
        }

        public string Remarks
        {
            get => _remarks;
            set => SetField(ref _remarks, value);
        }

        public string LabTitle
        {
            get => _labTitle;
            set => SetField(ref _labTitle, value);
        }

        public string TotalScore
        {
            get => _totalScore;
            set => SetField(ref _totalScore, value);
        }

        public string TotalPossible
        {
            get => _totalPossible;
            set => SetField(ref _totalPossible, value);
        }

        public SolidColorBrush TotalScoreColor
        {
            get => _totalScoreColor;
            set => SetField(ref _totalScoreColor, value);
        }

        public bool IsPlaceholderVisible
        {
            get => _isPlaceholderVisible;
            set
            {
                if (SetField(ref _isPlaceholderVisible, value))
                    OnPropertyChanged(nameof(IsContentVisible));
            }
        }

        public bool IsContentVisible => !_isPlaceholderVisible;

        public bool HasResults
        {
            get => _hasResults;
            set
            {
                if (SetField(ref _hasResults, value))
                    OnPropertyChanged(nameof(NoResultsVisible));
            }
        }

        public bool NoResultsVisible => !_hasResults;

        public bool ControlsEnabled => !_sheet.IsBadSubmission;

        public System.Windows.Input.ICommand CopyFeedbackCommand { get; }

        public event EventHandler? FeedbackCopied;

        public GradingViewModel()
        {
            CopyFeedbackCommand = new RelayCommand(
                CopyFeedback,
                () => _sheet.IsLoaded || _sheet.IsBadSubmission);

            foreach (var model in _sheet.Deductions)
            {
                var vm = new DeductionViewModel(model);
                vm.Toggled += (_, _) => OnDeductionToggled(vm);
                Deductions.Add(vm);
            }
        }

        // ─── Public API ───────────────────────────────────────────────────────

        public void LoadResults(LabResults? results, Student? student = null)
        {
            _student = student;
            _lastAutoRemarks = string.Empty;
            _sheet.Load(results);
            ResetDeductions();

            StudentName = student?.FirstName ?? string.Empty;
            IsPlaceholderVisible = false;
            TestResults.Clear();

            if (results == null || results.Results.Count == 0)
            {
                LabTitle = string.Empty;
                TotalScore = string.Empty;
                TotalPossible = string.Empty;
                HasResults = false;
                SetRemarks(DefaultRemarks());
                return;
            }

            HasResults = true;
            LabTitle = results.Name;

            foreach (var test in results.Results)
            {
                var vm = new TestResultViewModel(test, results.LevelPercents);
                vm.ScoreChanged += (_, _) => UpdateTotalScore();
                TestResults.Add(vm);
            }

            SetRemarks(DefaultRemarks());
            UpdateTotalScore();
        }

        public void Clear()
        {
            _student = null;
            _lastAutoRemarks = string.Empty;
            _sheet.Clear();
            ResetDeductions();

            LabTitle = string.Empty;
            TotalScore = string.Empty;
            TotalPossible = string.Empty;
            StudentName = string.Empty;
            Remarks = string.Empty;
            HasResults = false;
            IsPlaceholderVisible = true;
            TestResults.Clear();
        }

        public string GenerateFeedbackHtml() => _sheet.ToHtml(StudentName, Remarks);

        // ─── Deductions ───────────────────────────────────────────────────────

        private void ResetDeductions()
        {
            foreach (var d in Deductions)
            {
                d.Refresh();
                d.IsEnabled = true;
            }
            OnPropertyChanged(nameof(ControlsEnabled));
        }

        private void OnDeductionToggled(DeductionViewModel deduction)
        {
            if (deduction.IsBadSubmission)
            {
                // A bad submission can't be graded, so everything but its own checkbox locks.
                foreach (var d in Deductions)
                    d.IsEnabled = d.IsBadSubmission || !_sheet.IsBadSubmission;
                OnPropertyChanged(nameof(ControlsEnabled));
            }

            // Ticking or unticking an auto-zero always replaces the remarks.
            if (deduction.IsAutoZero)
                SetRemarks(DefaultRemarks());

            UpdateTotalScore();
        }

        // ─── Score and remarks ────────────────────────────────────────────────

        private void UpdateTotalScore()
        {
            if (!_sheet.IsLoaded) return;

            TotalScore = $"{_sheet.FinalGrade:F2}";
            TotalPossible = $" / {_sheet.Possible:F2}";
            TotalScoreColor = ScoreBrushes.For(FeedbackSheet.Band(_sheet.FinalGrade, _sheet.Possible));

            // Keep the remarks in step with the score until the grader edits them.
            var isStillAuto = Remarks == _lastAutoRemarks || string.IsNullOrEmpty(Remarks);
            if (isStillAuto)
                SetRemarks(DefaultRemarks());
        }

        private string DefaultRemarks() => _sheet.DefaultRemarks(_student?.FirstName ?? "Student");

        private void SetRemarks(string remarks)
        {
            Remarks = remarks;
            _lastAutoRemarks = remarks;
        }

        // ─── Clipboard ────────────────────────────────────────────────────────

        private void CopyFeedback()
        {
            Clipboard.SetText(GenerateFeedbackHtml());
            FeedbackCopied?.Invoke(this, EventArgs.Empty);
        }
    }
}
