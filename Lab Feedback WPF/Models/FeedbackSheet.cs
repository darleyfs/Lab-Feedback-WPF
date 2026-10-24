using System.Text;

namespace Lab_Feedback_WPF.Models
{
    /// <summary>How close a score is to full marks; drives every score colour and check mark.</summary>
    public enum ScoreBand
    {
        Full,      // 100%
        Close,     // 70% or more
        Low,       // under 70%
        Unscored   // nothing to earn
    }

    /// <summary>
    /// One submission's grading: its rubric rows or tests, the course's deductions, the final
    /// grade, the default remarks, and the feedback HTML. Created once and reused: Load() resets
    /// it for each submission.
    /// </summary>
    public class FeedbackSheet
    {
        private LabResults? _results;

        public IReadOnlyList<Deduction> Deductions { get; } = new[]
        {
            new Deduction("Method has multiple returns",        10f),
            new Deduction("Method lacks descriptive comments",  10f),
            new Deduction("Violates external resource policy",   0f, isAutoZero: true),
            new Deduction("Bad submission",                      0f, isAutoZero: true, isBadSubmission: true)
        };

        /// <summary>False until Load() is given results (even empty ones).</summary>
        public bool IsLoaded => _results != null;

        public IReadOnlyList<TestResult> Rows => _results?.Results ?? (IReadOnlyList<TestResult>)Array.Empty<TestResult>();

        /// <summary>Starts a new submission: points come from the autograder and no deductions are ticked.</summary>
        public void Load(LabResults? results)
        {
            _results = results;

            foreach (var row in Rows)
                row.GradedPoints = row.PointsReceived;

            foreach (var deduction in Deductions)
                deduction.IsApplied = false;
        }

        public void Clear() => Load(null);

        // ─── Grade ────────────────────────────────────────────────────────────

        public bool IsAutoZero => Deductions.Any(d => d.IsApplied && d.IsAutoZero);
        public bool IsBadSubmission => Deductions.Any(d => d.IsApplied && d.IsBadSubmission);

        public float Possible => Rows.Sum(r => r.PointsTotal);

        public float FinalGrade
        {
            get
            {
                if (IsAutoZero) return 0f;

                var deducted = Deductions.Where(d => d.IsApplied && !d.IsAutoZero).Sum(d => d.Points);
                return Math.Max(0f, Rows.Sum(r => r.GradedPoints) - deducted);
            }
        }

        public float Percentage => Possible > 0 ? FinalGrade / Possible * 100f : 0f;

        public static ScoreBand Band(float earned, float total)
        {
            if (total <= 0) return ScoreBand.Unscored;
            return (earned / total * 100f) switch
            {
                >= 100f => ScoreBand.Full,
                >= 70f => ScoreBand.Close,
                _ => ScoreBand.Low
            };
        }

        // ─── Remarks ──────────────────────────────────────────────────────────

        public string DefaultRemarks(string firstName)
        {
            if (IsBadSubmission)
                return
                    $"<p>Hey {firstName},</p>\n\n" +
                    "<p>It looks like the project wasn't submitted correctly. Please send the full project, " +
                    "including the Solution (.sln or .slnx) file and all related folders and files. You might " +
                    "find it helpful to review the videos in the <em>Required Software</em> and <em>How to Complete " +
                    "Labs</em> assignments for more details on submitting your work.</p>\n\n" +
                    "<p>I've given you a 24-hour extension so you can resubmit the assignment. Once you've " +
                    "submitted the project correctly, I'll review your grade again.</p>\n\n" +
                    "<p>Let me know if you have any other questions.</p>\n" +
                    "<p>Doug</p>";

            if (IsAutoZero)
                return
                    "<p>Unfortunately, this assignment contains the const keyword, C++ references, and " +
                    "initializer lists. These are outlined as not being usable within the course within " +
                    "the External Research Use Policy. As a result, you will receive a 0 for this assignment.</p>\n\n" +
                    "<p>Doug</p>";

            return Percentage switch
            {
                >= 100f => "<p>Excellent job on this assignment!</p>",
                >= 80f => "<p>Great job on this assignment!</p>",
                >= 70f => "<p>Good job on this assignment!</p>",
                _ => "<p>Good work on this assignment!</p>"
            };
        }

        // ─── Feedback HTML ────────────────────────────────────────────────────

        /// <summary>The feedback the student receives. A bad submission's feedback is just its remarks.</summary>
        public string ToHtml(string studentName, string remarks)
        {
            if (IsBadSubmission) return remarks;
            if (_results == null) return string.Empty;

            var sb = new StringBuilder();

            sb.AppendLine("<p>");
            sb.AppendLine($"    <span class=\"student\">{studentName}</span><br><br>");
            sb.AppendLine($"    <span class=\"intro\">{remarks}</span>");
            sb.AppendLine("</p>");
            sb.AppendLine();

            sb.AppendLine("<div class=\"feedback\">");
            foreach (var test in Rows)
            {
                var testId = test.Name.ToLower().Replace(" ", "_");
                var passed = Band(test.GradedPoints, test.PointsTotal) is ScoreBand.Full or ScoreBand.Unscored;
                var scoreColor = passed ? "rgb(0, 199, 199)" : "rgb(161, 0, 0)";
                var description = string.IsNullOrWhiteSpace(test.Description)
                    ? test.Name : test.Description;

                sb.AppendLine($"<div class=\"{testId}-output\">");
                sb.AppendLine($"    <strong class=\"output-header\">{test.Name}: " +
                              $"<span style=\"color: {scoreColor};\">" +
                              $"{test.GradedPoints:F1}/{test.PointsTotal:F1}</span></strong>");
                sb.AppendLine("    <ul class=\"output-list\">");
                if (test.HasRequirements)
                    foreach (var requirement in test.Requirements)
                        AppendListItem(sb, $"{testId}-{requirement.Id}", requirement.Label, requirement.Met);
                else
                    AppendListItem(sb, $"{testId}-result", description, passed);
                sb.AppendLine("    </ul>");
                if (!string.IsNullOrWhiteSpace(test.Comments))
                {
                    sb.AppendLine("    <table style=\"margin-bottom: 12px;\">");
                    sb.AppendLine("        <tbody><tr><td>");
                    sb.AppendLine($"            <p>{test.Comments}</p>");
                    sb.AppendLine("        </td></tr></tbody>");
                    sb.AppendLine("    </table>");
                }
                sb.AppendLine("</div>");
            }
            sb.AppendLine("</div>");
            sb.AppendLine();

            var applied = Deductions.Where(d => d.IsApplied).ToList();
            if (applied.Any())
            {
                sb.AppendLine("<ul>");
                foreach (var d in applied)
                    sb.AppendLine(d.IsAutoZero
                        ? $"    <li><strong>-100pts:</strong> {d.Label}</li>"
                        : $"    <li><strong>-{d.Points:F0}pts:</strong> {d.Label}</li>");
                sb.AppendLine("</ul>");
            }

            sb.AppendLine($"<p>Final Grade: <span class=\"grade\">{FinalGrade:F1}</span>/{Possible:F0}</p>");
            return sb.ToString();
        }

        private static void AppendListItem(StringBuilder sb, string cssClass, string text, bool passed)
        {
            var itemColor = passed ? "rgb(74, 145, 57)" : "rgb(151, 151, 151)";
            sb.AppendLine($"        <li class=\"{cssClass}\" " +
                          $"style=\"color: {itemColor}; font-weight: {(passed ? "bold" : "normal")}; margin-top: 4px;\">");
            sb.AppendLine($"            <span class=\"feedback\" style=\"display: inline;\">{text}</span>");
            sb.AppendLine($"            <span style=\"display: {(passed ? "inline" : "none")};\"> ✓</span>");
            sb.AppendLine("        </li>");
        }
    }
}
