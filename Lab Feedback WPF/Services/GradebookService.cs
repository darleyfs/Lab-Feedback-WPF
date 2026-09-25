using Lab_Feedback_WPF.Models;
using System.IO;

namespace Lab_Feedback_WPF.Services
{
    /// <summary>
    /// Loads reschedule-relevant grade data by parsing each section's gradebook CSV and the
    /// class-wide reschedule CSV(s) (found at the root folder, shared across all sections).
    /// </summary>
    public static class GradebookService
    {
        private static readonly string[] RescheduleIdColumns =
        {
            "Student ID#", "Student ID", "ID", "StudentID", "id", "student_id"
        };

        /// <summary>
        /// Finds and parses every class-wide reschedule CSV directly in <paramref name="rootFolder"/>.
        /// A CSV qualifies by its contents (a "Student Name" / "Student ID" header row), not its
        /// file name; IDs from all qualifying files are merged.
        /// </summary>
        public static (HashSet<string> RescheduledIds, bool Found) LoadRescheduleList(string rootFolder)
        {
            var ids = new HashSet<string>();
            if (!Directory.Exists(rootFolder))
                return (ids, false);

            var found = false;
            foreach (var file in Directory.GetFiles(rootFolder, "*.csv", SearchOption.TopDirectoryOnly))
            {
                if (TryLoadRescheduledIds(file, out var fileIds))
                {
                    found = true;
                    ids.UnionWith(fileIds);
                }
            }

            return (ids, found);
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

            var gradebookFiles = GetGradebookFiles(sectionFolder);

            foreach (var gradebookFile in gradebookFiles)
                records.AddRange(ParseGradebook(gradebookFile, sectionName, rescheduledIds));

            return (records, gradebookFiles.Count > 0);
        }

        /// <summary>
        /// Names of the subfolders of <paramref name="rootFolder"/> that contain a gradebook CSV,
        /// so a section shows up in the overview even when it has no student folders.
        /// </summary>
        public static List<string> FindSectionsWithGradebooks(string rootFolder)
        {
            if (!Directory.Exists(rootFolder)) return new List<string>();

            return Directory.GetDirectories(rootFolder)
                .Where(d => GetGradebookFiles(d).Count > 0)
                .Select(Path.GetFileName)
                .OfType<string>()
                .ToList();
        }

        private static List<string> GetGradebookFiles(string folder) =>
            Directory.GetFiles(folder, "*.csv", SearchOption.TopDirectoryOnly)
                .Where(f => Path.GetFileName(f).Contains("gradebook", StringComparison.OrdinalIgnoreCase))
                .ToList();

        private static bool IsRescheduleHeader(string[] fields) =>
            fields.Contains("Student Name") && fields.Any(f => f.Contains("Student ID"));

        /// <summary>
        /// Parses <paramref name="filePath"/> as a reschedule CSV. Returns false if the file has no
        /// reschedule header row or can't be read (e.g. locked by Excel).
        /// </summary>
        private static bool TryLoadRescheduledIds(string filePath, out HashSet<string> ids)
        {
            ids = new HashSet<string>();

            string[] headers;
            List<Dictionary<string, string>> rows;
            try
            {
                (headers, rows) = CsvUtils.ParseWithHeader(filePath, headerDetector: IsRescheduleHeader);
            }
            catch (IOException)
            {
                return false;
            }

            // ParseWithHeader falls back to the first line when no header matches.
            if (!IsRescheduleHeader(headers))
                return false;

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

            return true;
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
