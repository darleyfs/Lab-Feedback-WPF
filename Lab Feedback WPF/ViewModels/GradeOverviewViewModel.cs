using Lab_Feedback_WPF.Models;
using Lab_Feedback_WPF.Services;
using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Lab_Feedback_WPF.ViewModels
{
    public class GradeOverviewViewModel : ViewModelBase
    {
        private int _total;
        private int _failing;
        private int _passing;
        private int _tbd;
        private int _inactive;
        private int _alreadyRescheduled;
        private int _needRescheduling;
        private string _missingGradebookNote = string.Empty;

        public ObservableCollection<GradeRecord> Records { get; } = new();

        public int Total { get => _total; private set => SetField(ref _total, value); }
        public int Failing { get => _failing; private set => SetField(ref _failing, value); }
        public int Passing { get => _passing; private set => SetField(ref _passing, value); }
        public int TBD { get => _tbd; private set => SetField(ref _tbd, value); }
        public int Inactive { get => _inactive; private set => SetField(ref _inactive, value); }
        public int AlreadyRescheduled { get => _alreadyRescheduled; private set => SetField(ref _alreadyRescheduled, value); }
        public int NeedRescheduling { get => _needRescheduling; private set => SetField(ref _needRescheduling, value); }

        public string MissingGradebookNote { get => _missingGradebookNote; private set => SetField(ref _missingGradebookNote, value); }

        public ICommand CopySelectedCommand { get; }
        public ICommand CopyNeedRescheduleCommand { get; }

        public GradeOverviewViewModel()
        {
            CopySelectedCommand = new RelayCommand<IList>(CopySelected);
            CopyNeedRescheduleCommand = new RelayCommand(CopyNeedReschedule);
        }

        public void Load(string rootPath, IEnumerable<string> sectionFolderNames)
        {
            Records.Clear();

            var missingSections = new List<string>();

            foreach (var sectionName in sectionFolderNames)
            {
                var sectionFolder = Path.Combine(rootPath, sectionName);
                var records = GradebookService.LoadSection(sectionFolder, sectionName);

                if (records.Count == 0)
                    missingSections.Add(sectionName);

                foreach (var record in records)
                    Records.Add(record);
            }

            MissingGradebookNote = missingSections.Count > 0
                ? $"No gradebook found for section(s): {string.Join(", ", missingSections)}"
                : string.Empty;

            UpdateSummary();
        }

        public void Clear()
        {
            Records.Clear();
            MissingGradebookNote = string.Empty;
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            Total = Records.Count;
            Failing = Records.Count(r => r.Status == GradeStatus.Failing);
            Passing = Records.Count(r => r.Status == GradeStatus.Passing);
            TBD = Records.Count(r => r.Status == GradeStatus.TBD);
            Inactive = Records.Count(r => r.Status == GradeStatus.Inactive);
            AlreadyRescheduled = Records.Count(r => r.AlreadyRescheduled);
            NeedRescheduling = Records.Count(r => r.NeedsReschedule);
        }

        private static void CopySelected(IList? selected)
        {
            if (selected == null || selected.Count == 0) return;

            var lines = selected.Cast<GradeRecord>().Select(r => $"{r.Name}\t{r.Id}");
            Clipboard.SetText(string.Join("\n", lines));
        }

        private void CopyNeedReschedule()
        {
            var lines = Records.Where(r => r.NeedsReschedule).Select(r => $"{r.Name}\t{r.Id}");
            var text = string.Join("\n", lines);

            if (!string.IsNullOrEmpty(text))
                Clipboard.SetText(text);
        }
    }
}
