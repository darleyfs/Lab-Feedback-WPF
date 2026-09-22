namespace Lab_Feedback_WPF.Models
{
    /// <summary>
    /// Whether a section's gradebook and reschedule CSVs were found on disk.
    /// </summary>
    public class SectionCheckStatus
    {
        public string Section { get; }
        public bool GradebookFound { get; }
        public bool RescheduleFound { get; }

        public SectionCheckStatus(string section, bool gradebookFound, bool rescheduleFound)
        {
            Section = section;
            GradebookFound = gradebookFound;
            RescheduleFound = rescheduleFound;
        }
    }
}
