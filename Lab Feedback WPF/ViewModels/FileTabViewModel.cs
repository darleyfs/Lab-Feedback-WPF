using System.IO;

namespace Lab_Feedback_WPF.ViewModels
{
    public class FileTabViewModel : ViewModelBase
    {
        private bool _isSelected;

        public string FilePath { get; }
        public string FileName => Path.GetFileName(FilePath);

        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }

        public FileTabViewModel(string filePath) => FilePath = filePath;
    }
}
