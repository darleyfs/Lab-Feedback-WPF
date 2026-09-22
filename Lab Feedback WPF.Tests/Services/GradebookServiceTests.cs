using Lab_Feedback_WPF.Models;
using Lab_Feedback_WPF.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public class GradebookServiceTests
{
    private string _sectionFolder = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _sectionFolder = Path.Combine(Path.GetTempPath(), $"gradebook_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_sectionFolder);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_sectionFolder))
            Directory.Delete(_sectionFolder, recursive: true);
    }

    [TestMethod]
    public void LoadSection_NoGradebookFile_ReturnsEmpty()
    {
        var records = GradebookService.LoadSection(_sectionFolder, "01");
        Assert.AreEqual(0, records.Count);
    }

    [TestMethod]
    public void LoadSection_GradebookWithoutReschedule_ClassifiesEveryRowNotRescheduled()
    {
        File.WriteAllText(Path.Combine(_sectionFolder, "Gradebook_01.csv"),
            "ID,Activity,Actual Grade,Best Possible,Status\n" +
            "0001234567,Smith John,0,40,ACTIVE\n" +
            "0007654321,Doe Jane,95,100,ACTIVE\n" +
            "0009999999,Old Student,0,0,DROPPED\n");

        var records = GradebookService.LoadSection(_sectionFolder, "01");

        Assert.AreEqual(3, records.Count);
        Assert.IsTrue(records.All(r => !r.AlreadyRescheduled));

        var failing = records.Single(r => r.Name == "Smith John");
        Assert.AreEqual(GradeStatus.Failing, failing.Status);
        Assert.IsTrue(failing.NeedsReschedule);

        var passing = records.Single(r => r.Name == "Doe Jane");
        Assert.AreEqual(GradeStatus.Passing, passing.Status);

        var inactive = records.Single(r => r.Name == "Old Student");
        Assert.AreEqual(GradeStatus.Inactive, inactive.Status);
    }

    [TestMethod]
    public void LoadSection_StudentOnRescheduleList_IsFlaggedAndNoLongerNeedsReschedule()
    {
        File.WriteAllText(Path.Combine(_sectionFolder, "Gradebook_01.csv"),
            "ID,Activity,Actual Grade,Best Possible,Status\n" +
            "0001234567,Smith John,0,40,ACTIVE\n");
        File.WriteAllText(Path.Combine(_sectionFolder, "reschedules.csv"),
            "Student Name,Student ID#,Reason\n" +
            "Smith John,0001234567,Illness\n");

        var records = GradebookService.LoadSection(_sectionFolder, "01");

        var record = records.Single();
        Assert.IsTrue(record.AlreadyRescheduled);
        Assert.IsFalse(record.NeedsReschedule);
        Assert.AreEqual("On Reschedule List", record.RescheduleStatusText);
    }

    [TestMethod]
    public void LoadSection_NonStudentRows_AreSkipped()
    {
        File.WriteAllText(Path.Combine(_sectionFolder, "Gradebook_01.csv"),
            "ID,Activity,Actual Grade,Best Possible,Status\n" +
            "Weight,Weight,,,\n" +
            ",,,,\n" +
            "0001234567,Smith John,60,100,ACTIVE\n");

        var records = GradebookService.LoadSection(_sectionFolder, "01");

        Assert.AreEqual(1, records.Count);
        Assert.AreEqual("Smith John", records[0].Name);
    }
}
