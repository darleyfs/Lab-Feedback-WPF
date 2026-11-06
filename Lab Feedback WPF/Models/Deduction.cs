namespace Lab_Feedback_WPF.Models
{
    public class Deduction
    {
        public string Label { get; init; }
        public float Points { get; init; }
        public bool IsAutoZero { get; init; }
        /// <summary>For a deduction that makes the submission ungradeable: the HTML message to the student, with a {firstName} placeholder.</summary>
        public string? ResubmitRemarks { get; init; }
        public bool IsResubmit => ResubmitRemarks != null;
        public bool IsApplied { get; set; }

        public Deduction(string label, float points, bool isAutoZero = false, string? resubmitRemarks = null)
        {
            Label = label;
            Points = points;
            IsAutoZero = isAutoZero;
            ResubmitRemarks = resubmitRemarks;
        }
    }
}