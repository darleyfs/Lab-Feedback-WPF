using Lab_Feedback_WPF.Models;
using System.IO;

namespace Lab_Feedback_WPF.Services
{
    /// <summary>
    /// Loads reschedule-relevant grade data by parsing each section's gradebook CSV and the
    /// single class-wide reschedule CSV (found at the root folder, shared across all sections).
    /// </summary>
    public static class GradebookService
    {
        private static readonly string[] RescheduleIdColumns =
        {
            "Student ID#", "Student ID", "ID", "StudentID", "id", "student_id"
        };

        /// <summary>
        /// Finds and parses the single class-wide reschedule CSV directly in <paramref name="rootFolder"/>.
        /// </summary>
        public static (HashSet<string> RescheduledIds, bool Found) LoadRescheduleList(string rootFolder)
        {
            if (!Directory.Exists(rootFolder))
                return (new HashSet<string>(), false);

            var rescheduleFile = Directory.GetFiles(rootFolder, "*.csv", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(f => Path.GetFileName(f).Contains("reschedul", StringComparison.OrdinalIgnoreCase));

            if (rescheduleFile == null)
                return (new HashSet<string>(), false);

            return (LoadRescheduledIds(rescheduleFile), true);
        }

        /// <summary>
        /// Parses a section's gradebook CSV(s), cross-referencing against the already-loaded
        /// class-wide <paramref name="rescheduledIds"/>.
        /// </summary>
        public static (List<GradeRecord> Records, bool GradebookFound) LoadSection(
            string sectionFolder, string sectionName, HashSet<string> rescheduledIds)
        {
            var records = new List<GradeRecord>();
            if (!Directory.Exists(sectionFolder)) return (records, false);

            var gradebookFiles = Directory.GetFiles(sectionFolder, "*.csv", SearchOption.TopDirectoryOnly)
                .Where(f => Path.GetFileName(f).Contains("gradebook", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var gradebookFile in gradebookFiles)
                records.AddRange(ParseGradebook(gradebookFile, sectionName, rescheduledIds));

            return (records, gradebookFiles.Count > 0);
        }

        private static HashSet<string> LoadRescheduledIds(string filePath)
        {
            var ids = new HashSet<string>();

            var (_, rows) = CsvUtils.ParseWithHeader(filePath,
                headerDetector: fields => fields.Contains("Student Name") && fields.Any(f => f.Contains("Student ID")));

            foreach (var row in rows)
            {
                foreach (var col in RescheduleIdColumns)
                {
                    if (row.TryGetValue(col, out var value) && !string.IsNullOrWhiteSpace(value))
                    {
                        ids.Add(GradeRecord.NormalizeId(value));
                        break;
                    }
                }
            }

            return ids;
        }

        private static IEnumerable<GradeRecord> ParseGradebook(
            string filePath, string sectionName, HashSet<string> rescheduledIds)
        {
            var (headers, rows) = CsvUtils.ParseWithHeader(filePath);
            if (headers.Length == 0) yield break;

            var idColumn = headers[0];

            foreach (var row in rows)
            {
                var name = row.TryGetValue("Activity", out var activity) ? activity.Trim() : string.Empty;
                if (string.IsNullOrEmpty(name) || name is "Unknown" or "Weight") continue;

                var rawId = row.TryGetValue(idColumn, out var idValue) ? idValue : string.Empty;
                if (!long.TryParse(rawId.Trim(), out _)) continue;

                var id = GradeRecord.NormalizeId(rawId);
                var actualGrade = SafeParseDouble(row.GetValueOrDefault("Actual Grade"));
                var bestPossible = SafeParseDouble(row.GetValueOrDefault("Best Possible"));
                var status = GradeRecord.CalculateStatus(row.GetValueOrDefault("Status"), actualGrade, bestPossible);

                yield return new GradeRecord(
                    sectionName, name, id, status, actualGrade, bestPossible,
                    rescheduledIds.Contains(id));
            }
        }

        private static double SafeParseDouble(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "-") return 0.0;
            return double.TryParse(value, out var result) ? result : 0.0;
        }
    }
}
