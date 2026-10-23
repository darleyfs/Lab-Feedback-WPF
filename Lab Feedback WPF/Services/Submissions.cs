using Lab_Feedback_WPF.Models;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lab_Feedback_WPF.Services
{
    /// <summary>What <see cref="Submissions.Open"/> found for one submission.</summary>
    /// <param name="StudentFiles">Source files the student wrote; starter files are left out.</param>
    /// <param name="PrimaryFile">The student file to show first, or null if there are no student files.</param>
    /// <param name="Results">The autograder's results.json, or null if there isn't one.</param>
    /// <param name="BuildLog">The build log, or null if there isn't one.</param>
    /// <param name="Problems">Messages for the grader, e.g. a missing build log.</param>
    public record OpenedSubmission(
        IReadOnlyList<string> StudentFiles,
        string? PrimaryFile,
        LabResults? Results,
        IReadOnlyList<BuildLogEntry>? BuildLog,
        IReadOnlyList<string> Problems);

    /// <summary>
    /// Everything that knows how a student's submissions sit on disk: zips, "submission_*"
    /// attempt folders, Lab and Practical folder names, starter files, results.json and the build log.
    /// </summary>
    public static class Submissions
    {
        private static readonly HashSet<string> StarterFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            "DONOTUSEANYTHINGINTHISFILE.h", "Source.h", "Helper.cpp", "Helper.h",
            "Source.cpp", "Test.cpp", "Test.h", "Tester.cpp", "Tester.h",
            "Utility.cpp", "Utility.h", "UI.h", "ShopUtils.cpp", "ShopUtils.h",
            "LabUI.h", "resource.h", "LLMChecker.h", "ProgressBar.h", "Result.h",
            "Results.h", "ResultsLib.h", "LabTestUtils.h", "Console.h", "Console.cpp"
        };

        // ─── Prepare ──────────────────────────────────────────────────────────

        /// <summary>
        /// Unzips the student's uploads, keeps only the latest attempt of each assignment, and
        /// returns the assignments found, ordered by name. Changes the folder on disk.
        /// <paramref name="createProgress"/> is only called when there is something to unzip,
        /// because creating the progress dialog shows it.
        /// </summary>
        public static async Task<List<Assignment>> PrepareAsync(string studentFolder, Func<IExtractionProgress>? createProgress)
        {
            if (!Directory.Exists(studentFolder)) return new List<Assignment>();

            if (Directory.GetFiles(studentFolder, "*.zip", SearchOption.AllDirectories).Length > 0)
            {
                var progress = createProgress?.Invoke();
                var attempts = AttemptFolders(studentFolder).OrderBy(d => d).ToList();
                foreach (var folder in attempts.Count > 0 ? attempts : new List<string> { studentFolder })
                    await ExtractZipsAsync(folder, progress);

                if (progress?.IsVisible == true)
                    progress.Close();
            }

            if (AttemptFolders(studentFolder).Any())
                KeepLatestAttempts(studentFolder);

            return FindAssignments(studentFolder);
        }

        // ─── Open ─────────────────────────────────────────────────────────────

        public static OpenedSubmission Open(Assignment assignment)
        {
            var problems = new List<string>();
            var name = assignment.Name ?? string.Empty;
            var folder = assignment.Folder;

            if (folder == null || !Directory.Exists(folder))
            {
                problems.Add("There may be a problem with the folder structure.");
                return new OpenedSubmission(Array.Empty<string>(), null, null, null, problems);
            }

            var starterFiles = new HashSet<string>(StarterFiles, StringComparer.OrdinalIgnoreCase);
            if (assignment.Kind == AssignmentKind.Lab)
            {
                // The Lab's own test runner, e.g. "Lab 3A.cpp" or "Lab3A.cpp".
                starterFiles.Add(name + ".cpp");
                starterFiles.Add(name.Replace(" ", "") + ".cpp");
            }

            var studentFiles = new[] { "*.h", "*.cpp", "*.hpp" }
                .SelectMany(pattern => Directory.GetFiles(folder, pattern, SearchOption.AllDirectories))
                .Where(f => !starterFiles.Contains(Path.GetFileName(f)))
                .ToList();

            var primaryFile = PrimaryFile(assignment, studentFiles);
            if (primaryFile == null)
                problems.Add("There may be a problem with the folder structure.");

            var results = LoadResults(folder, problems);

            var buildLogPath = FindFile(folder, "hdkvkt.txt");
            var buildLog = buildLogPath == null ? null : ParseBuildLog(buildLogPath);
            if (buildLog == null)
                problems.Add($"Build logs not found for {name}.");

            return new OpenedSubmission(studentFiles, primaryFile, results, buildLog, problems);
        }

        private static string? PrimaryFile(Assignment assignment, List<string> studentFiles)
        {
            string? Named(string fileName) =>
                studentFiles.FirstOrDefault(f => Path.GetFileName(f).Equals(fileName, StringComparison.OrdinalIgnoreCase));

            var name = assignment.Name ?? string.Empty;
            var preferred = assignment.Kind == AssignmentKind.Lab
                ? Named("Submission.cpp") ?? Named("StudentWork.h")
                : name.StartsWith("Midterm") ? Named("HighScore_Table.cpp")
                : name.StartsWith("Final") ? Named("RPG_Shop.cpp")
                : Named(name + ".cpp");

            return preferred ?? studentFiles.FirstOrDefault();
        }

        private static LabResults? LoadResults(string folder, List<string> problems)
        {
            var path = FindFile(folder, "results.json");
            if (path == null) return null;

            try
            {
                return JsonSerializer.Deserialize<LabResults>(File.ReadAllText(path));
            }
            catch (JsonException)
            {
                problems.Add("The autograder's results.json could not be read.");
                return null;
            }
        }

        private static List<BuildLogEntry> ParseBuildLog(string path) =>
            File.ReadAllLines(path)
                .Select(line => line.Split(" - "))
                .Where(parts => parts.Length == 2 && int.TryParse(parts[1], out _))
                .Select(parts => new BuildLogEntry(parts[0], int.Parse(parts[1])))
                .ToList();

        private static string? FindFile(string folder, string fileName) =>
            Directory.GetFiles(folder, fileName, SearchOption.AllDirectories).FirstOrDefault();

        // ─── Assignment discovery ─────────────────────────────────────────────

        private static List<Assignment> FindAssignments(string folder)
        {
            var assignments = new List<Assignment>();
            SearchForAssignments(folder, assignments);

            // The same assignment can appear more than once; keep the newest copy.
            return assignments
                .GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(a => AttemptDate(a.Folder)).First())
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void SearchForAssignments(string folder, List<Assignment> assignments)
        {
            foreach (var entry in Directory.GetDirectories(folder))
            {
                var name = Path.GetFileName(entry);

                if (name.StartsWith(".") || name == "bin" || name == "obj")
                    continue;

                if (IsAssignmentFolderName(name))
                    assignments.Add(new Assignment(name, entry));
                else
                    SearchForAssignments(entry, assignments);
            }
        }

        private static bool IsAssignmentFolderName(string name) =>
            name.StartsWith("Lab ") || name.Contains("Practical");

        // ─── Attempts ("submission_<date>" folders) ───────────────────────────

        private static IEnumerable<string> AttemptFolders(string studentFolder) =>
            Directory.GetDirectories(studentFolder)
                .Where(d => Path.GetFileName(d).StartsWith("submission_", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Moves the newest copy of each assignment out of the attempt folders into the student
        /// folder, then deletes the attempt folders.
        /// </summary>
        private static void KeepLatestAttempts(string studentFolder)
        {
            var attempts = AttemptFolders(studentFolder)
                .Select(attempt => (Date: AttemptDate(attempt), Folder: attempt, Assignments: Directory
                    .GetDirectories(attempt, "*", SearchOption.AllDirectories)
                    .Where(d =>
                    {
                        var name = Path.GetFileName(d);
                        return IsAssignmentFolderName(name)
                            && !d.Split(Path.DirectorySeparatorChar).Any(p => p.StartsWith("."))
                            && !name.EndsWith(".tlog");
                    })
                    .ToList()))
                .ToList();

            var latestPerAssignment = attempts
                .SelectMany(a => a.Assignments.Select(folder => (a.Date, Folder: folder)))
                .GroupBy(x => Path.GetFileName(x.Folder), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.Date).First().Folder)
                .ToList();

            foreach (var assignmentFolder in latestPerAssignment)
            {
                var name = Path.GetFileName(assignmentFolder);
                var destination = Path.Combine(studentFolder, name);

                try
                {
                    if (Directory.Exists(destination))
                        Directory.Delete(destination, recursive: true);

                    Directory.Move(assignmentFolder, destination);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error moving {name}: {ex.Message}");
                }
            }

            foreach (var attempt in attempts)
            {
                try
                {
                    Directory.Delete(attempt.Folder, recursive: true);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error deleting {Path.GetFileName(attempt.Folder)}: {ex.Message}");
                }
            }
        }

        private static DateTime AttemptDate(string? folderPath)
        {
            if (folderPath == null) return DateTime.MinValue;

            foreach (var part in folderPath.Split(Path.DirectorySeparatorChar))
            {
                if (!part.StartsWith("submission_", StringComparison.OrdinalIgnoreCase))
                    continue;

                var match = Regex.Match(part, @"(\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2})");
                if (match.Success &&
                    DateTime.TryParseExact(match.Value, "yyyy-MM-dd_HH-mm-ss", null,
                        System.Globalization.DateTimeStyles.None, out var date))
                {
                    return date;
                }
            }

            return DateTime.MinValue;
        }

        // ─── Zips ─────────────────────────────────────────────────────────────

        /// <summary>Extracts and then deletes each zip directly in <paramref name="folder"/>.</summary>
        private static async Task ExtractZipsAsync(string folder, IExtractionProgress? progress)
        {
            var zipFiles = Directory.GetFiles(folder, "*.zip");
            if (zipFiles.Length == 0) return;

            var operationId = Guid.NewGuid().ToString();
            var token = progress?.AddOperation(operationId, Path.GetFileName(folder), zipFiles.Length);

            try
            {
                for (var i = 0; i < zipFiles.Length; i++)
                {
                    // None means the progress dialog was closed: stop quietly.
                    if (token?.CancellationToken == CancellationToken.None) return;
                    if (token?.CancellationToken.IsCancellationRequested == true) return;

                    var fileName = Path.GetFileName(zipFiles[i]);
                    progress?.UpdateOperation(operationId, i + 1, $"Extracting: {fileName}");

                    try
                    {
                        await ExtractZipAsync(zipFiles[i], folder, token?.CancellationToken ?? CancellationToken.None);
                        File.Delete(zipFiles[i]);
                        progress?.UpdateOperation(operationId, i + 1, $"Done: {fileName}");
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        progress?.UpdateOperation(operationId, i + 1, $"Error: {ex.Message}");
                        await Task.Delay(1000);
                    }
                }

                progress?.CompleteOperation(operationId);
            }
            catch (Exception ex)
            {
                progress?.FailOperation(operationId, ex.Message);
            }
        }

        private static Task ExtractZipAsync(string zipFile, string folder, CancellationToken cancellationToken) =>
            Task.Run(() =>
            {
                var root = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
                using var archive = ZipFile.OpenRead(zipFile);

                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Zips come from students: skip entries that would land outside the folder ("zip slip").
                    var destination = Path.GetFullPath(Path.Combine(folder, entry.FullName));
                    if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        if (entry.Name == "")
                        {
                            Directory.CreateDirectory(destination);
                        }
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                            entry.ExtractToFile(destination, overwrite: true);
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip files we can't write.
                    }
                }
            }, cancellationToken);
    }
}
