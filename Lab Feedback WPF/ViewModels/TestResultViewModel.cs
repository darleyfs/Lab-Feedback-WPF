using Lab_Feedback_WPF.Models;
using System.Windows.Media;

namespace Lab_Feedback_WPF.ViewModels
{
    public class TestResultViewModel : ViewModelBase
    {
        private float _gradedPoints;
        private string _comments;

        public TestResult Model { get; }
        public string Name => Model.Name;
        public float PointsTotal => Model.PointsTotal;
        public string PointsTotalText => $" / {PointsTotal:F2}";
        public string DisplayDescription => string.IsNullOrWhiteSpace(Model.Description)
            ? "No description provided." : Model.Description;
        public bool HasDescription => !string.IsNullOrWhiteSpace(Model.Description);

        public float GradedPoints
        {
            get => _gradedPoints;
            set
            {
                var clamped = Math.Clamp(value, 0f, PointsTotal);
                if (SetField(ref _gradedPoints, clamped))
                {
                    Model.GradedPoints = clamped;
                    OnPropertyChanged(nameof(IndicatorColor));
                    OnPropertyChanged(nameof(PointsText));
                    ScoreChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string PointsText => _gradedPoints.ToString("F2");

        public string Comments
        {
            get => _comments;
            set
            {
                if (SetField(ref _comments, value))
                    Model.Comments = value;
            }
        }

        public bool HasComments => !string.IsNullOrEmpty(_comments);

        public SolidColorBrush IndicatorColor => ScoreToBrush(_gradedPoints, PointsTotal);

        public event EventHandler? ScoreChanged;

        public TestResultViewModel(TestResult model)
        {
            Model = model;
            _gradedPoints = model.GradedPoints;
            _comments = model.Comments;
        }

        private static SolidColorBrush ScoreToBrush(float graded, float total)
        {
            if (total <= 0) return new SolidColorBrush(Color.FromRgb(100, 100, 100));
            return ((graded / total) * 100f) switch
            {
                >= 100f => new SolidColorBrush(Color.FromRgb(166, 226, 46)),
                >= 70f => new SolidColorBrush(Color.FromRgb(253, 151, 31)),
                _ => new SolidColorBrush(Color.FromRgb(249, 38, 114))
            };
        }
    }
}
