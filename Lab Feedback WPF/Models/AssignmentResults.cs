using System.Text.Json.Serialization;

namespace Lab_Feedback_WPF.Models
{
    public class LabResults
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        // Percent of a rubric row's weight awarded per level, in TestResult.LevelNames order.
        [JsonPropertyName("levelPercents")]
        public List<float> LevelPercents { get; set; } = new() { 100f, 89f, 79f, 69f, 59f, 0f };

        [JsonPropertyName("results")]
        public List<TestResult> Results { get; set; } = new();

        [JsonPropertyName("deductions")]
        public List<object> Deductions { get; set; } = new();
    }

    public class TestResult
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("pointsTotal")]
        public float PointsTotal { get; set; }

        [JsonPropertyName("pointsReceived")]
        public float PointsReceived { get; set; }

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        // Only Practical rubric rows have requirements; Lab tests leave these empty.
        [JsonPropertyName("requirements")]
        public List<Requirement> Requirements { get; set; } = new();

        // Fewest requirements met for each level, Exemplary..Insufficient; negative means the level is not used.
        [JsonPropertyName("minimumMet")]
        public List<int> MinimumMet { get; set; } = new();

        public static readonly string[] LevelNames =
            { "Exemplary", "Effective", "Developing", "Beginning", "Insufficient", "Not Attempted" };

        public bool HasRequirements => Requirements.Count > 0;

        // Same rule as LabTester's Rubric::LevelFor; keep the two in step (docs/adr/0001).
        public int LevelIndex()
        {
            var met = Requirements.Count(r => r.Met);
            for (var i = 0; i < MinimumMet.Count && i < 5; i++)
                if (MinimumMet[i] >= 0 && met >= MinimumMet[i]) return i;
            return 5;
        }

        public float PointsForLevel(IReadOnlyList<float> levelPercents) =>
            PointsTotal * levelPercents[LevelIndex()] / 100f;

        // Not in JSON — used for grading form
        public float GradedPoints { get; set; }
        public string Comments { get; set; } = string.Empty;
    }

    public class Requirement
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("met")]
        public bool Met { get; set; }

        // Why the requirement wasn't met; shown to the grader only.
        [JsonPropertyName("note")]
        public string Note { get; set; } = string.Empty;
    }
}