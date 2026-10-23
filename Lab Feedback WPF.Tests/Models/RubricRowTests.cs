using System.Text.Json;
using Lab_Feedback_WPF.Models;
using Lab_Feedback_WPF.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Models;

[TestClass]
public class RubricRowTests
{
    // Same shape LabTester's Rubric::Save writes: a 20-point row graded "4 of 5" for Exemplary.
    private const string PracticalJson =
        "{\"name\": \"Practical 1\", \"levelPercents\": [100.000000, 89.000000, 79.000000, 69.000000, 59.000000, 0.000000], " +
        "\"results\": [{\"name\": \"Menu\", \"description\": \"Effective (3/5 requirements met).\", " +
        "\"pointsTotal\": 20.000000, \"pointsReceived\": 17.799999, \"tags\": [\"Effective\", \"missing:loop\", \"missing:exit\"], " +
        "\"requirements\": [" +
        "{\"id\": \"show\", \"label\": \"Shows the menu\", \"met\": true, \"note\": \"\"}, " +
        "{\"id\": \"read\", \"label\": \"Reads a choice\", \"met\": true, \"note\": \"\"}, " +
        "{\"id\": \"switch\", \"label\": \"Uses a switch\", \"met\": true, \"note\": \"\"}, " +
        "{\"id\": \"loop\", \"label\": \"Repeats the menu\", \"met\": false, \"note\": \"menu shown once\"}, " +
        "{\"id\": \"exit\", \"label\": \"Exits on 0\", \"met\": false, \"note\": \"\"}], " +
        "\"minimumMet\": [4, 3, 2, -1, 1]}], " +
        "\"deductions\": []}";

    private static LabResults Load(string json) => JsonSerializer.Deserialize<LabResults>(json)!;

    [TestMethod]
    public void Deserialize_PracticalRow_ReadsRequirementsAndThresholds()
    {
        var results = Load(PracticalJson);
        var row = results.Results[0];

        Assert.IsTrue(row.HasRequirements);
        Assert.AreEqual(5, row.Requirements.Count);
        Assert.AreEqual("menu shown once", row.Requirements[3].Note);
        CollectionAssert.AreEqual(new[] { 4, 3, 2, -1, 1 }, row.MinimumMet);
        Assert.AreEqual(89f, results.LevelPercents[1]);
    }

    [TestMethod]
    public void Deserialize_LabRowWithoutRequirements_KeepsOldBehaviour()
    {
        var results = Load("{\"name\": \"Lab 1\", \"results\": [{\"name\": \"Add\", \"pointsTotal\": 10, \"pointsReceived\": 10, \"tags\": []}], \"deductions\": []}");

        Assert.IsFalse(results.Results[0].HasRequirements);
        Assert.AreEqual(6, results.LevelPercents.Count);
    }

    [TestMethod]
    public void LevelIndex_FollowsThresholds_AndSkipsUnusedLevels()
    {
        var row = Load(PracticalJson).Results[0];

        Assert.AreEqual(1, row.LevelIndex()); // 3 met -> Effective

        row.Requirements[3].Met = true;
        Assert.AreEqual(0, row.LevelIndex()); // 4 of 5 -> Exemplary

        foreach (var r in row.Requirements) r.Met = false;
        row.Requirements[0].Met = true;
        Assert.AreEqual(4, row.LevelIndex()); // 1 met skips the unused Beginning level -> Insufficient

        row.Requirements[0].Met = false;
        Assert.AreEqual(5, row.LevelIndex()); // none met -> Not Attempted
    }

    [TestMethod]
    public void TickingRequirement_RecalculatesPointsAndLevel()
    {
        var results = Load(PracticalJson);
        var vm = new TestResultViewModel(results.Results[0], results.LevelPercents);

        vm.Requirements[3].IsMet = true;

        Assert.AreEqual(20f, vm.GradedPoints, 0.001f);
        Assert.AreEqual("Exemplary", vm.LevelText);
        Assert.IsFalse(vm.IsPointsEditable);
    }

    [TestMethod]
    public void FeedbackHtml_ListsEachRequirement_AndOmitsEmptyComment()
    {
        var grading = new GradingViewModel();
        grading.LoadResults(Load(PracticalJson));

        var html = grading.GenerateFeedbackHtml();

        Assert.AreEqual(5, html.Split("<li class=\"menu-").Length - 1);
        StringAssert.Contains(html, "Repeats the menu");
        Assert.IsFalse(html.Contains("menu shown once"), "Notes are for the grader only");
        Assert.IsFalse(html.Contains("<table"), "Empty comments leave no comment block");

        grading.TestResults[0].Comments = "Nice menu";
        StringAssert.Contains(grading.GenerateFeedbackHtml(), "<p>Nice menu</p>");
    }
}
