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
        private bool _rescheduleFileFound;
        private string _instructor = string.Empty;
        private string _copyStatus = string.Empty;

        public ObservableCollection<GradeRecord> Records { get; } = new();
        public ObservableCollection<SectionCheckStatus> SectionChecks { get; } = new();

        public int Total { get => _total; private set => SetField(ref _total, value); }
        public int Failing { get => _failing; private set => SetField(ref _failing, value); }
        public int Passing { get => _passing; private set => SetField(ref _passing, value); }
        public int TBD { get => _tbd; private set => SetField(ref _tbd, value); }
        public int Inactive { get => _inactive; private set => SetField(ref _inactive, value); }
        public int AlreadyRescheduled { get => _alreadyRescheduled; private set => SetField(ref _alreadyRescheduled, value); }
        public int NeedRescheduling { get => _needRescheduling; private set => SetField(ref _needRescheduling, value); }

        /// <summary>Whether at least one class-wide reschedule CSV was found at the root folder.</summary>
        public bool RescheduleFileFound { get => _rescheduleFileFound; private set => SetField(ref _rescheduleFileFound, value); }

        /// <summary>Instructor name written into the Instructor column of copied reschedule rows.</summary>
        public string Instructor { get => _instructor; set => SetField(ref _instructor, value); }

        /// <summary>Feedback from the last copy action (e.g. "Copied 3 rows").</summary>
        public string CopyStatus { get => _copyStatus; private set => SetField(ref _copyStatus, value); }

        public ICommand CopySelectedCommand { get; }
        public ICommand CopyNeedRescheduleCommand { get; }

        public GradeOverviewViewModel()
        {
            CopySelectedCommand = new RelayCommand<IList>(CopySelected, selected => selected?.Count > 0);
            CopyNeedRescheduleCommand = new RelayCommand(CopyNeedReschedule, () => NeedRescheduling > 0);
        }

        public void Load(string rootPath, IEnumerable<string> sectionFolderNames)
        {
            Records.Clear();
            SectionChecks.Clear();

            var (rescheduledIds, rescheduleFound) = GradebookService.LoadRescheduleList(rootPath);
            RescheduleFileFound = rescheduleFound;

            foreach (var sectionName in sectionFolderNames)
            {
                var sectionFolder = Path.Combine(rootPath, sectionName);
                var (records, gradebookFound) = GradebookService.LoadSection(sectionFolder, sectionName, rescheduledIds);

                foreach (var record in records)
                    Records.Add(record);

                SectionChecks.Add(new SectionCheckStatus(sectionName, gradebookFound));
            }

            UpdateSummary();
        }

        public void Clear()
        {
            Records.Clear();
            SectionChecks.Clear();
            RescheduleFileFound = false;
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
            CopyStatus = string.Empty;
            CommandManager.InvalidateRequerySuggested();
        }

        private void CopySelected(IList? selected)
        {
            if (selected == null || selected.Count == 0) return;

            CopyRows(selected.Cast<GradeRecord>().ToList());
        }

        private void CopyNeedReschedule() => CopyRows(Records.Where(r => r.NeedsReschedule).ToList());

        private void CopyRows(List<GradeRecord> records)
        {
            if (records.Count == 0)
            {
                CopyStatus = "Nothing to copy";
                return;
            }

            var text = string.Join("\n", records.Select(r => r.ToRescheduleRow(Instructor)));

            // Retries cover the clipboard being briefly locked by another app.
            for (var attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    Clipboard.SetDataObject(text, true);
                    CopyStatus = records.Count == 1 ? "Copied 1 row" : $"Copied {records.Count} rows";
                    return;
                }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                    Thread.Sleep(50);
                }
            }

            CopyStatus = "Clipboard busy — try again";
        }
    }
}
