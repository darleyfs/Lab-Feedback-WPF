namespace Lab_Feedback_WPF.Models
{
    /// <summary>
    /// Whether a section's gradebook CSV was found on disk. (The reschedule CSV is a single
    /// class-wide file, not per-section, so it isn't tracked here — see
    /// GradeOverviewViewModel.RescheduleFileFound.)
    /// </summary>
    public class SectionCheckStatus
    {
        public string Section { get; }
        public bool GradebookFound { get; }

        public SectionCheckStatus(string section, bool gradebookFound)
        {
            Section = section;
            GradebookFound = gradebookFound;
        }
    }
}
