using Lab_Feedback_WPF.Models;

namespace Lab_Feedback_WPF.ViewModels
{
    public class DeductionViewModel : ViewModelBase
    {
        private bool _isEnabled = true;

        public Deduction Model { get; }
        public string Label => Model.Label;
        public float Points => Model.Points;
        public bool IsAutoZero => Model.IsAutoZero;
        public bool IsBadSubmission => Model.IsBadSubmission;

        public bool IsApplied
        {
            get => Model.IsApplied;
            set
            {
                if (Model.IsApplied == value) return;
                Model.IsApplied = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PointsText));
                Toggled?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Raised when the grader ticks or unticks this deduction (not on Refresh).</summary>
        public event EventHandler? Toggled;

        /// <summary>Tells the UI the model was reset outside this view model (a new submission loaded).</summary>
        public void Refresh()
        {
            OnPropertyChanged(nameof(IsApplied));
            OnPropertyChanged(nameof(PointsText));
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
