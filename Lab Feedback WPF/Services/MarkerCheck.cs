using Lab_Feedback_WPF.Models;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lab_Feedback_WPF.Services
{
    /// <summary>
    /// The section tags planted in this term's starters, read from the staff repo's markers.json.
    /// Tokens live outside this (public) repo so students can't look them up.
    /// </summary>
    public record MarkerSet(string Prefix, string Label, IReadOnlyDictionary<string, string> Assignments)
    {
        /// <summary>Environment variable holding the path to markers.json.</summary>
        public const string PathVariable = "LAB_FEEDBACK_MARKERS";

        private record Json(string Prefix, string Label, Dictionary<string, string> Markers);

        public static MarkerSet Parse(string json)
        {
            var j = JsonSerializer.Deserialize<Json>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("markers.json is empty.");
            if (string.IsNullOrEmpty(j.Prefix) || string.IsNullOrEmpty(j.Label) || j.Markers == null)
                throw new InvalidDataException("markers.json needs prefix, label and markers.");
            return new MarkerSet(j.Prefix, j.Label, j.Markers);
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
    /// Finds markers: section tags or assisted-by labels an AI agent left in student code because
    /// the starter's instructions asked it to. Unlike violations, comments are searched, since the
    /// label is a comment. A marker is a reason to talk to the student, not proof; it never reaches
    /// the feedback.
    /// </summary>
    public static class MarkerCheck
    {
        public static ViolationReport Check(IEnumerable<KeyValuePair<string, string>> files, MarkerSet set)
        {
            // Any prefix + 3 chars, so a token from another assignment or an older term still shows.
            var pattern = new Regex($"{Regex.Escape(set.Prefix)}[a-z0-9]{{3}}|{Regex.Escape(set.Label)}");
            return new(files.ToDictionary(
                f => f.Key,
                f => (IReadOnlyList<Violation>)Find(f.Value, pattern, set)), "marker");
        }

        private static List<Violation> Find(string code, Regex pattern, MarkerSet set)
        {
            var found = new List<Violation>();
            var lines = code.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var match = pattern.Match(lines[i]);
                if (!match.Success) continue;

                var token = pattern.Matches(lines[i]).Select(m => m.Value).FirstOrDefault(v => v.StartsWith(set.Prefix));
                var context = token == null ? "label without a token"
                    : set.Assignments.TryGetValue(token, out var assignment) ? $"token for {assignment}"
                    : "unknown token";
                found.Add(new Violation(i + 1, token ?? match.Value, context, lines[i].Trim()));
            }
            return found;
        }
    }
}
