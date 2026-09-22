using System.IO;
using System.Text;

namespace Lab_Feedback_WPF.Services
{
    /// <summary>
    /// Minimal RFC4180-ish CSV reader: quoted fields, embedded commas, doubled-quote escaping.
    /// Does not support embedded newlines inside quoted fields.
    /// </summary>
    public static class CsvUtils
    {
        public static string[] ParseLine(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            fields.Add(current.ToString());
            return fields.ToArray();
        }

        /// <summary>
        /// Parses a CSV file into dictionaries keyed by header column name, along with the
        /// header row itself (in order — useful when a column's name is unknown, e.g. "the
        /// first column"). If <paramref name="headerDetector"/> is provided, lines before the
        /// first line it accepts are skipped (used for files whose real header isn't on line 1).
        /// </summary>
        public static (string[] Headers, List<Dictionary<string, string>> Rows) ParseWithHeader(
            string filePath, Func<string[], bool>? headerDetector = null)
        {
            var lines = File.ReadAllLines(filePath);

            var headerIndex = 0;
            if (headerDetector != null)
            {
                for (var i = 0; i < lines.Length; i++)
                {
                    if (headerDetector(ParseLine(lines[i])))
                    {
                        headerIndex = i;
                        break;
                    }
                }
            }

            var rows = new List<Dictionary<string, string>>();
            if (headerIndex >= lines.Length) return (Array.Empty<string>(), rows);

            var headers = ParseLine(lines[headerIndex]);

            for (var i = headerIndex + 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                var values = ParseLine(lines[i]);
                if (values.All(string.IsNullOrWhiteSpace)) continue;

                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var col = 0; col < headers.Length; col++)
                    row[headers[col]] = col < values.Length ? values[col] : string.Empty;

                rows.Add(row);
            }

            return (headers, rows);
        }
    }
}
