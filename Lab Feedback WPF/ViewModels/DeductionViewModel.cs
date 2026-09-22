using Lab_Feedback_WPF.Models;

namespace Lab_Feedback_WPF.ViewModels
{
    public class DeductionViewModel : ViewModelBase
    {
        private bool _isApplied;
        private bool _isEnabled = true;

        public Deduction Model { get; }
        public string Label => Model.Label;
        public float Points => Model.Points;
        public bool IsAutoZero => Model.IsAutoZero;
        public bool IsBadSubmission => Model.IsBadSubmission;

        public bool IsApplied
        {
            get => _isApplied;
            set
            {
                if (SetField(ref _isApplied, value))
                {
                    Model.IsApplied = value;
                    OnPropertyChanged(nameof(PointsText));
                }
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetField(ref _isEnabled, value);
        }

        public string PointsText => IsAutoZero
            ? (IsApplied ? "AUTO ZERO" : "Auto Zero")
            : $"-{Points:F0} pts";

        public DeductionViewModel(Deduction model) => Model = model;
    }
}
