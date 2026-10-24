using Lab_Feedback_WPF.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestResult = Lab_Feedback_WPF.Models.TestResult;

namespace Lab_Feedback_WPF_Tests.Models;

[TestClass]
public class FeedbackSheetTests
{
    // Two 10-point tests, one passed and one half done: 15 / 20 (75%).
    private static FeedbackSheet Loaded()
    {
        var sheet = new FeedbackSheet();
        sheet.Load(new LabResults
        {
            Name = "Lab 3",
            Results =
            {
                new TestResult { Name = "Add", Description = "Adds two numbers", PointsTotal = 10, PointsReceived = 10 },
                new TestResult { Name = "Divide", Description = "Divides safely", PointsTotal = 10, PointsReceived = 5 }
            }
        });
        return sheet;
    }

    private static Deduction Tick(FeedbackSheet sheet, string label)
    {
        var deduction = sheet.Deductions.Single(d => d.Label == label);
        deduction.IsApplied = true;
        return deduction;
    }

    // ─── Grade ────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Load_StartsFromAutograderPoints()
    {
        var sheet = Loaded();

        Assert.AreEqual(15f, sheet.FinalGrade);
        Assert.AreEqual(20f, sheet.Possible);
        Assert.AreEqual(75f, sheet.Percentage);
    }

    [TestMethod]
    public void Deductions_SubtractPoints_AndNeverGoBelowZero()
    {
        var sheet = Loaded();

        Tick(sheet, "Method has multiple returns");
        Assert.AreEqual(5f, sheet.FinalGrade);

        Tick(sheet, "Method lacks descriptive comments");
        Assert.AreEqual(0f, sheet.FinalGrade);
    }

    [TestMethod]
    public void AutoZero_MakesGradeZero()
    {
        var sheet = Loaded();

        Tick(sheet, "Violates external resource policy");

        Assert.IsTrue(sheet.IsAutoZero);
        Assert.IsFalse(sheet.IsBadSubmission);
        Assert.AreEqual(0f, sheet.FinalGrade);
    }

    [TestMethod]
    public void RemovingBadSubmission_KeepsOtherAutoZero()
    {
        var sheet = Loaded();
        Tick(sheet, "Violates external resource policy");
        var bad = Tick(sheet, "Bad submission");

        bad.IsApplied = false;

        Assert.IsTrue(sheet.IsAutoZero, "The external-resource zero still applies");
        Assert.AreEqual(0f, sheet.FinalGrade);
    }

    [TestMethod]
    public void Load_ResetsDeductionsAndPoints()
    {
        var sheet = Loaded();
        Tick(sheet, "Bad submission");
        sheet.Rows[1].GradedPoints = 0;

        sheet.Load(new LabResults { Results = { new TestResult { Name = "Add", PointsTotal = 10, PointsReceived = 8 } } });

        Assert.IsFalse(sheet.Deductions.Any(d => d.IsApplied));
        Assert.AreEqual(8f, sheet.FinalGrade);
    }

    [TestMethod]
    public void Band_UsesFullCloseLowThresholds()
    {
        Assert.AreEqual(ScoreBand.Full, FeedbackSheet.Band(10, 10));
        Assert.AreEqual(ScoreBand.Close, FeedbackSheet.Band(7, 10));
        Assert.AreEqual(ScoreBand.Low, FeedbackSheet.Band(6.9f, 10));
        Assert.AreEqual(ScoreBand.Unscored, FeedbackSheet.Band(0, 0));
    }

    // ─── Remarks ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void DefaultRemarks_FollowTheFinalGrade()
    {
        var sheet = Loaded();
        Assert.AreEqual("<p>Good job on this assignment!</p>", sheet.DefaultRemarks("Ada")); // 75%

        sheet.Rows[1].GradedPoints = 6;
        Assert.AreEqual("<p>Great job on this assignment!</p>", sheet.DefaultRemarks("Ada")); // 80%

        sheet.Rows[1].GradedPoints = 10;
        Assert.AreEqual("<p>Excellent job on this assignment!</p>", sheet.DefaultRemarks("Ada")); // 100%

        Tick(sheet, "Method has multiple returns");
        Assert.AreEqual("<p>Good work on this assignment!</p>", sheet.DefaultRemarks("Ada")); // 50% after the deduction
    }

    [TestMethod]
    public void DefaultRemarks_ExplainAutoZeros()
    {
        var sheet = Loaded();

        Tick(sheet, "Violates external resource policy");
        StringAssert.Contains(sheet.DefaultRemarks("Ada"), "External Research Use Policy");

        Tick(sheet, "Bad submission");
        StringAssert.StartsWith(sheet.DefaultRemarks("Ada"), "<p>Hey Ada,</p>");
        StringAssert.Contains(sheet.DefaultRemarks("Ada"), "<em>Required Software</em>");
    }

    // ─── Feedback HTML ────────────────────────────────────────────────────────

    [TestMethod]
    public void ToHtml_HasEachTestDeductionsAndFinalGrade()
    {
        var sheet = Loaded();
        Tick(sheet, "Method has multiple returns");

        var html = sheet.ToHtml("Ada", "<p>Hi</p>");

        StringAssert.Contains(html, "<span class=\"student\">Ada</span>");
        StringAssert.Contains(html, "<span class=\"intro\"><p>Hi</p></span>");
        StringAssert.Contains(html, "Add: <span style=\"color: rgb(0, 199, 199);\">10.0/10.0</span>");
        StringAssert.Contains(html, "Divide: <span style=\"color: rgb(161, 0, 0);\">5.0/10.0</span>");
        StringAssert.Contains(html, "<strong>-10pts:</strong> Method has multiple returns");
        StringAssert.Contains(html, "Final Grade: <span class=\"grade\">5.0</span>/20");
        Assert.IsFalse(html.Contains("<table"), "No comments means no comment blocks");
    }

    [TestMethod]
    public void ToHtml_AutoZeroListsHundredPointDeduction()
    {
        var sheet = Loaded();
        Tick(sheet, "Violates external resource policy");

        var html = sheet.ToHtml("Ada", "");

        StringAssert.Contains(html, "<strong>-100pts:</strong> Violates external resource policy");
        StringAssert.Contains(html, "Final Grade: <span class=\"grade\">0.0</span>/20");
    }

    [TestMethod]
    public void ToHtml_BadSubmissionIsJustTheRemarks()
    {
        var sheet = Loaded();
        Tick(sheet, "Bad submission");

        Assert.AreEqual("<p>Please resubmit.</p>", sheet.ToHtml("Ada", "<p>Please resubmit.</p>"));
    }

    [TestMethod]
    public void ToHtml_NothingLoaded_IsEmpty()
    {
        Assert.AreEqual(string.Empty, new FeedbackSheet().ToHtml("Ada", "<p>Hi</p>"));
    }
}
