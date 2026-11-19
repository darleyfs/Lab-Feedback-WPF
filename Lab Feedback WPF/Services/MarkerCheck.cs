using Lab_Feedback_WPF.Models;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lab_Feedback_WPF.Services
{
    /// <summary>
    /// The section codes planted in this term's starters (suffix to assignment), read from the staff
    /// repo's markers.json. Codes live outside this (public) repo so students can't look them up.
    /// </summary>
    public record MarkerSet(IReadOnlyDictionary<string, string> Assignments)
    {
        /// <summary>Environment variable holding the path to markers.json.</summary>
        public const string PathVariable = "LAB_FEEDBACK_MARKERS";

        private record Json(Dictionary<string, string>? Markers);

        public static MarkerSet Parse(string json)
        {
            var j = JsonSerializer.Deserialize<Json>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (j?.Markers == null || j.Markers.Count == 0 || j.Markers.Keys.Any(k => !Regex.IsMatch(k, "^[a-z0-9]+$")))
                throw new InvalidDataException("markers.json needs a non-empty \"markers\" map of lowercase section codes.");
            return new MarkerSet(j.Markers);
        }

        /// <summary>The configured marker set, null when none is configured, or the load error.</summary>
        public static (MarkerSet? Set, string? Error) LoadConfigured()
        {
            var path = Environment.GetEnvironmentVariable(PathVariable);
            if (string.IsNullOrEmpty(path)) return (null, null);
            try { return (Parse(File.ReadAllText(path)), null); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
            {
                return (null, $"Couldn't read {path}: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Finds markers: identifiers ending in a section code (e.g. <c>days_sq</c>), which the starter's
    /// instructions ask AI agents to use for local variables. Comments are skipped, because the
    /// starter's own notice spells the code out. A marker is a reason to talk to the student, not
    /// proof; it never reaches the feedback.
    /// </summary>
    public static class MarkerCheck
    {
        public static ViolationReport Check(IEnumerable<KeyValuePair<string, string>> files, MarkerSet set)
        {
            var pattern = new Regex($@"\b[A-Za-z]\w*_({string.Join("|", set.Assignments.Keys)})\b");
            return new(files.ToDictionary(
                f => f.Key,
                f => (IReadOnlyList<Violation>)Find(f.Value, pattern, set)), "marker");
        }

        private static List<Violation> Find(string code, Regex pattern, MarkerSet set)
        {
            var found = new List<Violation>();
            var original = code.Split('\n');
            var searchable = ViolationsMatcher.StripComments(code).Split('\n');
            for (int i = 0; i < searchable.Length; i++)
            {
                var match = pattern.Match(searchable[i]);
                if (match.Success)
                    found.Add(new Violation(i + 1, match.Value, $"section code for {set.Assignments[match.Groups[1].Value]}", original[i].Trim()));
            }
            return found;
        }
    }
}
