using Lab_Feedback_WPF.Models;
using System.IO;
using System.Text;

namespace Lab_Feedback_WPF.Services
{
    /// <summary>The violations found in a submission's student files, by file path.</summary>
    public class ViolationReport
    {
        public IReadOnlyDictionary<string, IReadOnlyList<Violation>> ByFile { get; }

        public ViolationReport(IReadOnlyDictionary<string, IReadOnlyList<Violation>> byFile) => ByFile = byFile;

        public static ViolationReport Empty { get; } = new(new Dictionary<string, IReadOnlyList<Violation>>());

        public int Total => ByFile.Values.Sum(v => v.Count);

        public IReadOnlyList<int> LinesIn(string file) =>
            ByFile.TryGetValue(file, out var violations)
                ? violations.Select(v => v.LineNumber).ToList()
                : Array.Empty<int>();

        /// <summary>Plain-text list for the status bar tooltip; empty when there are none.</summary>
        public string Summary
        {
            get
            {
                if (Total == 0) return string.Empty;

                var sb = new StringBuilder();
                sb.AppendLine($"Found {Total} violation(s):\n");
                foreach (var (file, violations) in ByFile.Where(f => f.Value.Count > 0))
                {
                    sb.AppendLine($"── {Path.GetFileName(file)}");
                    foreach (var v in violations)
                        sb.AppendLine($"  Line {v.LineNumber}: '{v.MatchedText}'  {v.Context}");
                    sb.AppendLine();
                }
                return sb.ToString().TrimEnd();
            }
        }
    }

    /// <summary>
    /// Finds banned terms (language features PG1 doesn't allow) in student code. Comments are
    /// ignored. Violations are evidence for the grader; they don't change the grade.
    /// </summary>
    public static class ViolationCheck
    {
        private static readonly ViolationsMatcher Matcher = new(new List<string>
        {
            "&", "const", "static_cast", "reinterpret_cast", "dynamic_cast",
            "resize()", "ignore()", "clear()", "auto", "try", "size_t", "goto",
            "catch", "\\0", "(...)", "var", "continue", "iterator"
        });

        /// <param name="files">Each student file's path and its code.</param>
        public static ViolationReport Check(IEnumerable<KeyValuePair<string, string>> files) =>
            new(files.ToDictionary(
                f => f.Key,
                f => (IReadOnlyList<Violation>)Matcher.GetViolations(f.Value)));
    }
}
