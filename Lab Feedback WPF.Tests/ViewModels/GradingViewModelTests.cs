using Lab_Feedback_WPF.Models;
using Lab_Feedback_WPF.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestResult = Lab_Feedback_WPF.Models.TestResult;

namespace Lab_Feedback_WPF_Tests.ViewModels;

[TestClass]
public class GradingViewModelTests
{
    // Deductions in the order GradingViewModel creates them.
    private const int MultipleReturns = 0;
    private const int NoComments = 1;
    private const int ExternalResource = 2;
    private const int BadSubmission = 3;

    // Two 10-point tests, one passed and one half done: 15 / 20 (75%).
    private static GradingViewModel Loaded()
    {
        var results = new LabResults
        {
            Name = "Lab 3",
            Results =
            {
                new TestResult { Name = "Add", Description = "Adds two numbers", PointsTotal = 10, PointsReceived = 10 },
                new TestResult { Name = "Divide", Description = "Divides safely", PointsTotal = 10, PointsReceived = 5 }
            }
        };
        var grading = new GradingViewModel();
        grading.LoadResults(results, new Student("Ada", "Lovelace", "1", null));
        return grading;
    }

    [TestMethod]
    public void LoadResults_StartsFromAutograderPoints()
    {
        var grading = Loaded();

        Assert.IsTrue(grading.HasResults);
        Assert.AreEqual("Lab 3", grading.LabTitle);
        Assert.AreEqual("Ada", grading.StudentName);
        Assert.AreEqual("15.00", grading.TotalScore);
        Assert.AreEqual(" / 20.00", grading.TotalPossible);
    }

    [TestMethod]
    public void LoadResults_NoResults_ShowsNoResults()
    {
        var grading = new GradingViewModel();

        grading.LoadResults(null);

        Assert.IsFalse(grading.HasResults);
        Assert.IsTrue(grading.NoResultsVisible);
        Assert.AreEqual(string.Empty, grading.GenerateFeedbackHtml());
    }

    [TestMethod]
    public void EditingPoints_UpdatesTotal_AndClampsToRange()
    {
        var grading = Loaded();

        grading.TestResults[1].GradedPoints = 99;
        Assert.AreEqual("20.00", grading.TotalScore);

        grading.TestResults[1].GradedPoints = -3;
        Assert.AreEqual("10.00", grading.TotalScore);
    }

    [TestMethod]
    public void TickingDeduction_UpdatesTotal()
    {
        var grading = Loaded();

        grading.Deductions[MultipleReturns].IsApplied = true;
        Assert.AreEqual("5.00", grading.TotalScore);

        grading.Deductions[MultipleReturns].IsApplied = false;
        Assert.AreEqual("15.00", grading.TotalScore);
    }

    [TestMethod]
    public void ExternalResourceViolation_ZeroesGrade_AndRemovingRestores()
    {
        var grading = Loaded();

        grading.Deductions[ExternalResource].IsApplied = true;
        Assert.AreEqual("0.00", grading.TotalScore);
        StringAssert.Contains(grading.Remarks, "External Research Use Policy");

        grading.Deductions[ExternalResource].IsApplied = false;
        Assert.AreEqual("15.00", grading.TotalScore);
        Assert.AreEqual("<p>Good job on this assignment!</p>", grading.Remarks);
    }

    [TestMethod]
    public void BadSubmission_LocksControls_AndFeedbackIsTheResubmitMessage()
    {
        var grading = Loaded();

        grading.Deductions[BadSubmission].IsApplied = true;

        Assert.IsFalse(grading.ControlsEnabled);
        Assert.IsFalse(grading.Deductions[MultipleReturns].IsEnabled);
        Assert.IsTrue(grading.Deductions[BadSubmission].IsEnabled);
        Assert.AreEqual("0.00", grading.TotalScore);
        StringAssert.StartsWith(grading.Remarks, "<p>Hey Ada,</p>");
        Assert.AreEqual(grading.Remarks, grading.GenerateFeedbackHtml());

        grading.Deductions[BadSubmission].IsApplied = false;
        Assert.IsTrue(grading.ControlsEnabled);
        Assert.IsTrue(grading.Deductions[MultipleReturns].IsEnabled);
        Assert.AreEqual("15.00", grading.TotalScore);
    }

    [TestMethod]
    public void AutoRemarks_FollowTheScore()
    {
        var grading = Loaded();
        Assert.AreEqual("<p>Good job on this assignment!</p>", grading.Remarks); // 75%

        grading.TestResults[1].GradedPoints = 10;
        Assert.AreEqual("<p>Excellent job on this assignment!</p>", grading.Remarks); // 100%
    }

    [TestMethod]
    public void AutoRemarks_DoNotOverwriteGraderEdits()
    {
        var grading = Loaded();
        grading.Remarks = "<p>See me after class.</p>";

        grading.TestResults[1].GradedPoints = 10;

        Assert.AreEqual("<p>See me after class.</p>", grading.Remarks);
    }

    [TestMethod]
    public void FeedbackHtml_UsesStudentNameAndCurrentRemarks()
    {
        var grading = Loaded();
        grading.Remarks = "<p>See me after class.</p>";

        var html = grading.GenerateFeedbackHtml();

        StringAssert.Contains(html, "<span class=\"student\">Ada</span>");
        StringAssert.Contains(html, "<span class=\"intro\"><p>See me after class.</p></span>");
    }

    [TestMethod]
    public void LoadResults_ResetsDeductionsFromPreviousStudent()
    {
        var grading = Loaded();
        grading.Deductions[MultipleReturns].IsApplied = true;

        grading.LoadResults(new LabResults { Name = "Lab 3", Results = { new TestResult { Name = "Add", PointsTotal = 10, PointsReceived = 10 } } });

        Assert.IsFalse(grading.Deductions[MultipleReturns].IsApplied);
        Assert.AreEqual("10.00", grading.TotalScore);
    }

    [TestMethod]
    public void Clear_ShowsPlaceholderAgain()
    {
        var grading = Loaded();

        grading.Clear();

        Assert.IsTrue(grading.IsPlaceholderVisible);
        Assert.AreEqual(0, grading.TestResults.Count);
        Assert.AreEqual(string.Empty, grading.TotalScore);
    }
}
