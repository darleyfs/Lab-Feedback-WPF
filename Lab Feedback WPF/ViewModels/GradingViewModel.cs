using Lab_Feedback_WPF.Models;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace Lab_Feedback_WPF.ViewModels
{
    public class GradingViewModel : ViewModelBase
    {
        private string _studentName = string.Empty;
        private string _remarks = string.Empty;
        private string _labTitle = string.Empty;
        private string _totalScore = string.Empty;
        private string _totalPossible = string.Empty;
        private SolidColorBrush _totalScoreColor = new(Color.FromRgb(166, 226, 46));
        private bool _isPlaceholderVisible = true;
        private bool _hasResults;
        private bool _isAutoZero;
        private bool _isBadSubmission;
        private string _lastAutoRemarks = string.Empty;
        private LabResults? _results;
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

        public bool ControlsEnabled => !_isBadSubmission;

        public System.Windows.Input.ICommand CopyFeedbackCommand { get; }

        public event EventHandler? FeedbackCopied;

        public GradingViewModel()
        {
            CopyFeedbackCommand = new RelayCommand(
                CopyFeedback,
                () => _results != null || _isBadSubmission);

            InitDeductions();
        }

        // ─── Public API ───────────────────────────────────────────────────────

        public void LoadResults(LabResults? results, Student? student = null)
        {
            _isBadSubmission = false;
            _isAutoZero = false;
            _results = results;
            _student = student;
            _lastAutoRemarks = string.Empty;

            foreach (var d in Deductions)
                d.IsApplied = false;

            StudentName = student?.FirstName ?? string.Empty;
            IsPlaceholderVisible = false;
            TestResults.Clear();

            if (results == null || results.Results.Count == 0)
            {
                LabTitle = string.Empty;
                TotalScore = string.Empty;
                TotalPossible = string.Empty;
                HasResults = false;

                var pct = CalculatePercentage();
                var defaultRemarks = GetDefaultRemarks(pct);
                Remarks = defaultRemarks;
                _lastAutoRemarks = defaultRemarks;
                return;
            }

            HasResults = true;
            LabTitle = results.Name;

            foreach (var test in results.Results)
                test.GradedPoints = test.PointsReceived;

            foreach (var test in results.Results)
            {
                var vm = new TestResultViewModel(test);
                vm.ScoreChanged += (_, _) => UpdateTotalScore();
                TestResults.Add(vm);
            }

            var percentage = CalculatePercentage();
            Remarks = GetDefaultRemarks(percentage);
            _lastAutoRemarks = Remarks;

            UpdateTotalScore();
        }

        public void Clear()
        {
            _results = null;
            _student = null;
            _isBadSubmission = false;
            _isAutoZero = false;
            _lastAutoRemarks = string.Empty;

            foreach (var d in Deductions)
                d.IsApplied = false;

            LabTitle = string.Empty;
            TotalScore = string.Empty;
            TotalPossible = string.Empty;
            StudentName = string.Empty;
            Remarks = string.Empty;
            HasResults = false;
            IsPlaceholderVisible = true;
            TestResults.Clear();
        }

        // ─── Deduction Setup ──────────────────────────────────────────────────

        private void InitDeductions()
        {
            var models = new[]
            {
                new Deduction("Method has multiple returns",        10f),
                new Deduction("Method lacks descriptive comments",  10f),
                new Deduction("Violates external resource policy",   0f, isAutoZero: true),
                new Deduction("Bad submission",                      0f, isAutoZero: true, isBadSubmission: true)
            };

            foreach (var model in models)
            {
                var vm = new DeductionViewModel(model);
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(DeductionViewModel.IsApplied))
                        OnDeductionChanged(vm);
                };
                Deductions.Add(vm);
            }
        }

        private void OnDeductionChanged(DeductionViewModel dvm)
        {
            if (dvm.IsBadSubmission)
            {
                if (dvm.IsApplied) ApplyBadSubmission();
                else RemoveBadSubmission();
            }
            else if (dvm.IsAutoZero)
            {
                if (dvm.IsApplied) ApplyAutoZero();
                else RemoveAutoZero();
            }
            else
            {
                UpdateTotalScore();
            }
        }

        // ─── Auto-zero / Bad Submission ───────────────────────────────────────

        private void ApplyAutoZero()
        {
            _isAutoZero = true;
            var text =
                "<p>Unfortunately, this assignment contains the const keyword, C++ references, and " +
                "initializer lists. These are outlined as not being usable within the course within " +
                "the External Research Use Policy. As a result, you will receive a 0 for this assignment.</p>\n\n" +
                "<p>Doug</p>";
            Remarks = text;
            _lastAutoRemarks = text;
            UpdateTotalScore();
        }

        private void RemoveAutoZero()
        {
            _isAutoZero = false;
            var pct = CalculatePercentage();
            var remarks = GetDefaultRemarks(pct);
            Remarks = remarks;
            _lastAutoRemarks = remarks;
            UpdateTotalScore();
        }

        private void ApplyBadSubmission()
        {
            _isBadSubmission = true;
            _isAutoZero = true;
            SetDeductionsEnabled(false);
            OnPropertyChanged(nameof(ControlsEnabled));

            var firstName = _student?.FirstName ?? "Student";
            var text =
                $"<p>Hey {firstName},</p>\n\n" +
                "<p>It looks like the project wasn't submitted correctly. Please send the full project, " +
                "including the Solution (.sln or .slnx) file and all related folders and files. You might " +
                "find it helpful to review the videos in the <em>Requiured Software</em> and <em>How to Complete " +
                "Labs</em> assignments for more details on submitting your work.</p>\n\n" +
                "<p>I've given you a 24-hour extension so you can resubmit the assignment. Once you've " +
                "submitted the project correctly, I'll review your grade again.</p>\n\n" +
                "<p>Let me know if you have any other questions.</p>\n" +
                "<p>Doug</p>";
            Remarks = text;
            _lastAutoRemarks = text;
            UpdateTotalScore();
        }

        private void RemoveBadSubmission()
        {
            _isBadSubmission = false;
            _isAutoZero = false;
            SetDeductionsEnabled(true);
            OnPropertyChanged(nameof(ControlsEnabled));

            var pct = CalculatePercentage();
            var remarks = GetDefaultRemarks(pct);
            Remarks = remarks;
            _lastAutoRemarks = remarks;
            UpdateTotalScore();
        }

        private void SetDeductionsEnabled(bool enabled)
        {
            foreach (var d in Deductions)
                d.IsEnabled = d.IsBadSubmission || enabled;
        }

        // ─── Grade Calculation ────────────────────────────────────────────────

        private float CalculateFinalGrade()
        {
            if (_isAutoZero) return 0f;
            if (_results == null) return 0f;

            var raw = _results.Results.Sum(r => r.GradedPoints);
            var deductions = Deductions
                .Where(d => d.IsApplied && !d.IsAutoZero)
                .Sum(d => d.Points);

            return Math.Max(0f, raw - deductions);
        }

        private float CalculatePercentage()
        {
            if (_results == null) return 0f;
            var earned = _results.Results.Sum(r => r.GradedPoints);
            var possible = _results.Results.Sum(r => r.PointsTotal);
            return possible > 0 ? (earned / possible) * 100f : 0f;
        }

        private void UpdateTotalScore()
        {
            if (_results == null) return;

            var possible = _results.Results.Sum(r => r.PointsTotal);
            var earned = CalculateFinalGrade();
            var pct = possible > 0 ? (earned / possible) * 100f : 0f;

            TotalScore = $"{earned:F2}";
            TotalPossible = $" / {possible:F2}";

            TotalScoreColor = _isAutoZero
                ? new SolidColorBrush(Color.FromRgb(249, 38, 114))
                : pct switch
                {
                    >= 100f => new SolidColorBrush(Color.FromRgb(166, 226, 46)),
                    >= 70f => new SolidColorBrush(Color.FromRgb(253, 151, 31)),
                    _ => new SolidColorBrush(Color.FromRgb(249, 38, 114))
                };

            if (_isAutoZero || _isBadSubmission) return;

            var newAutoRemarks = GetDefaultRemarks(pct);
            var isStillAuto = Remarks == _lastAutoRemarks || string.IsNullOrEmpty(Remarks);

            if (isStillAuto && newAutoRemarks != _lastAutoRemarks)
            {
                Remarks = newAutoRemarks;
                _lastAutoRemarks = newAutoRemarks;
            }
        }

        private static string GetDefaultRemarks(float percentage) => percentage switch
        {
            >= 100f => "<p>Excellent job on this assignment!</p>",
            >= 80f  => "<p>Great job on this assignment!</p>",
            >= 70f  => "<p>Good job on this assignment!</p>",
            _       => "<p>Good work on this assignment!</p>"
        };

        // ─── Clipboard ────────────────────────────────────────────────────────

        private void CopyFeedback()
        {
            Clipboard.SetText(GenerateFeedbackHtml());
            FeedbackCopied?.Invoke(this, EventArgs.Empty);
        }

        public string GenerateFeedbackHtml()
        {
            if (_isBadSubmission) return Remarks;
            if (_results == null) return string.Empty;

            var sb = new StringBuilder();
            var possible = _results.Results.Sum(r => r.PointsTotal);
            var finalGrade = CalculateFinalGrade();

            sb.AppendLine("<p>");
            sb.AppendLine($"    <span class=\"student\">{StudentName}</span><br><br>");
            sb.AppendLine($"    <span class=\"intro\">{Remarks}</span>");
            sb.AppendLine("</p>");
            sb.AppendLine();

            sb.AppendLine("<div class=\"feedback\">");
            foreach (var test in _results.Results)
            {
                var testId = test.Name.ToLower().Replace(" ", "_");
                var passed = test.GradedPoints >= test.PointsTotal;
                var scoreColor = passed ? "rgb(0, 199, 199)" : "rgb(161, 0, 0)";
                var itemColor = passed ? "rgb(74, 145, 57)" : "rgb(151, 151, 151)";
                var hasComments = !string.IsNullOrWhiteSpace(test.Comments);
                var description = string.IsNullOrWhiteSpace(test.Description)
                    ? test.Name : test.Description;

                sb.AppendLine($"<div class=\"{testId}-output\">");
                sb.AppendLine($"    <strong class=\"output-header\">{test.Name}: " +
                              $"<span style=\"color: {scoreColor};\">" +
                              $"{test.GradedPoints:F1}/{test.PointsTotal:F1}</span></strong>");
                sb.AppendLine("    <ul class=\"output-list\">");
                sb.AppendLine($"        <li class=\"{testId}-result\" " +
                              $"style=\"color: {itemColor}; font-weight: {(passed ? "bold" : "normal")}; margin-top: 4px;\">");
                sb.AppendLine($"            <span class=\"feedback\" style=\"display: inline;\">{description}</span>");
                sb.AppendLine($"            <span style=\"display: {(passed ? "inline" : "none")};\"> ✓</span>");
                sb.AppendLine("        </li>");
                sb.AppendLine("    </ul>");
                sb.AppendLine($"    <table style=\"display: {(hasComments ? "table" : "none")}; margin-bottom: 12px;\">");
                sb.AppendLine("        <tbody><tr><td>");
                sb.AppendLine(hasComments ? $"            <p>{test.Comments}</p>" : "            <p></p>");
                sb.AppendLine("        </td></tr></tbody>");
                sb.AppendLine("    </table>");
                sb.AppendLine("</div>");
            }
            sb.AppendLine("</div>");
            sb.AppendLine();

            var appliedDeductions = Deductions.Where(d => d.IsApplied).ToList();
            if (appliedDeductions.Any())
            {
                sb.AppendLine("<ul>");
                foreach (var d in appliedDeductions)
                    sb.AppendLine(d.IsAutoZero
                        ? $"    <li><strong>-100pts:</strong> {d.Label}</li>"
                        : $"    <li><strong>-{d.Points:F0}pts:</strong> {d.Label}</li>");
                sb.AppendLine("</ul>");
            }

            sb.AppendLine($"<p>Final Grade: <span class=\"grade\">{finalGrade:F1}</span>/{possible:F0}</p>");
            return sb.ToString();
        }
    }
}
