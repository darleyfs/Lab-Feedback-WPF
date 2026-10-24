namespace Lab_Feedback_WPF.Models
{
    /// <summary>One build in a submission's build log: when it ran and the score it got.</summary>
    public record BuildLogEntry(string Timestamp, int Number);
}
