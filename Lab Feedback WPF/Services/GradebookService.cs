using Lab_Feedback_WPF.Models;
using System.IO;

namespace Lab_Feedback_WPF.Services
{
    /// <summary>
    /// Loads reschedule-relevant grade data for a section by parsing the gradebook and
    /// reschedule CSVs found inside that section's folder.
    /// </summary>
    public static class GradebookService
    {
        private static readonly string[] RescheduleIdColumns =
        {
            "Student ID#", "Student ID", "ID", "StudentID", "id", "student_id"
        };

        public static (List<GradeRecord> Records, SectionCheckStatus Status) LoadSection(string sectionFolder, string sectionName)
        {
            var records = new List<GradeRecord>();

            if (!Directory.Exists(sectionFolder))
                return (records, new SectionCheckStatus(sectionName, gradebookFound: false, rescheduleFound: false));

            var files = Directory.GetFiles(sectionFolder, "*.csv", SearchOption.TopDirectoryOnly);

            var gradebookFiles = files.Where(f => Path.GetFileName(f).Contains("gradebook", StringComparison.OrdinalIgnoreCase)).ToList();
            var rescheduleFile = files.FirstOrDefault(f => Path.GetFileName(f).Contains("reschedul", StringComparison.OrdinalIgnoreCase));

            var rescheduledIds = rescheduleFile != null
                ? LoadRescheduledIds(rescheduleFile)
                : new HashSet<string>();

            foreach (var gradebookFile in gradebookFiles)
                records.AddRange(ParseGradebook(gradebookFile, sectionName, rescheduledIds));

            var status = new SectionCheckStatus(sectionName, gradebookFiles.Count > 0, rescheduleFile != null);
            return (records, status);
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
