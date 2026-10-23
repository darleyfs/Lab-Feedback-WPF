using Lab_Feedback_WPF.Models;

namespace Lab_Feedback_WPF.ViewModels
{
    public class RequirementViewModel : ViewModelBase
    {
        public Requirement Model { get; }
        public string Label => Model.Label;
        public string Note => Model.Note;
        public bool ShowNote => !Model.Met && !string.IsNullOrWhiteSpace(Model.Note);

        public bool IsMet
        {
            get => Model.Met;
            set
            {
                if (Model.Met == value) return;
                Model.Met = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowNote));
                MetChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? MetChanged;

        public RequirementViewModel(Requirement model) => Model = model;
    }
}
