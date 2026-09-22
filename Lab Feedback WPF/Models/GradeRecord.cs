namespace Lab_Feedback_WPF.Models
{
    public enum GradeStatus
    {
        Failing,
        Passing,
        TBD,
        Inactive
    }

    /// <summary>
    /// A single gradebook row for one student in one section, cross-referenced against the
    /// reschedule list. Independent of whether that student has a submission folder.
    /// </summary>
    public class GradeRecord
    {
        public string Section { get; }
        public string Name { get; }
        public string Id { get; }
        public GradeStatus Status { get; }
        public double ActualGrade { get; }
        public double BestPossible { get; }
        public bool AlreadyRescheduled { get; }

        public GradeRecord(string section, string name, string id, GradeStatus status,
            double actualGrade, double bestPossible, bool alreadyRescheduled)
        {
            Section = section;
            Name = name;
            Id = id;
            Status = status;
            ActualGrade = actualGrade;
            BestPossible = bestPossible;
            AlreadyRescheduled = alreadyRescheduled;
        }

        public bool NeedsReschedule => Status == GradeStatus.Failing && !AlreadyRescheduled;

        public string RescheduleStatusText
        {
            get
            {
                if (AlreadyRescheduled) return "On Reschedule List";
                return Status switch
                {
                    GradeStatus.Failing => "NOT on List",
                    GradeStatus.TBD => "Pending",
                    _ => "Not Needed"
                };
            }
        }

        /// <summary>Status classification, ported from the reschedule tool's grading logic.</summary>
        public static GradeStatus CalculateStatus(string? rawStatus, double actualGrade, double bestPossible)
        {
            var status = (rawStatus ?? "ACTIVE").Trim().ToUpperInvariant();
            if (status != "ACTIVE") return GradeStatus.Inactive;
            if (bestPossible < 59.5) return GradeStatus.Failing;
            if (actualGrade > 59.5) return GradeStatus.Passing;
            return GradeStatus.TBD;
        }

        /// <summary>Normalizes a student ID by stripping leading zeros, for cross-file matching.</summary>
        public static string NormalizeId(string? id)
        {
            var trimmed = (id ?? string.Empty).Trim();
            return long.TryParse(trimmed, out var n) ? n.ToString() : trimmed;
        }
    }
}
