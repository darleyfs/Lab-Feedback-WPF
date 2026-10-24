using Lab_Feedback_WPF.Models;
using Lab_Feedback_WPF.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public class GradebookServiceTests
{
    private const string Header = "ID,Activity,Actual Grade,Best Possible,Status\n";
    private const string FailingSmith = "0001234567,Smith John,0,40,ACTIVE\n";

    private string _folder = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), $"gradebook_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_folder);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    private void WriteGradebook(string section, string rows)
    {
        var folder = Path.Combine(_folder, section);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, $"Gradebook_{section}.csv"), Header + rows);
    }

    private void WriteAtRoot(string fileName, string contents) =>
        File.WriteAllText(Path.Combine(_folder, fileName), contents);

    private Gradebook Load(params string[] studentSections) => GradebookService.Load(_folder, studentSections);

    // -------------------------------------------------------------------------
    // Sections
    // -------------------------------------------------------------------------

    [TestMethod]
    public void NoSections_IsEmpty()
    {
        WriteAtRoot("reschedules.csv", "Student Name,Student ID#\nSmith John,0001234567\n");

        var gradebook = Load();

        Assert.AreEqual(0, gradebook.Records.Count);
        Assert.AreEqual(0, gradebook.SectionChecks.Count);
        Assert.IsFalse(gradebook.RescheduleFileFound);
    }

    [TestMethod]
    public void Sections_MergeStudentFoldersAndGradebookFolders_SortedWithoutDuplicates()
    {
        WriteGradebook("04", FailingSmith);
        WriteGradebook("01", FailingSmith);
        Directory.CreateDirectory(Path.Combine(_folder, "Empty"));

        var gradebook = Load("07", "04", "", "07");

        CollectionAssert.AreEqual(new[] { "01", "04", "07" }, gradebook.SectionChecks.Select(c => c.Section).ToArray());
    }

    [TestMethod]
    public void StudentSectionWithoutGradebook_IsReportedMissing()
    {
        var gradebook = Load("01");

        var check = gradebook.SectionChecks.Single();
        Assert.AreEqual("01", check.Section);
        Assert.IsFalse(check.GradebookFound);
        Assert.AreEqual(0, gradebook.Records.Count);
    }

    // -------------------------------------------------------------------------
    // Gradebook rows
    // -------------------------------------------------------------------------

    [TestMethod]
    public void GradebookRows_AreClassifiedByStatus()
    {
        WriteGradebook("01",
            FailingSmith +
            "0007654321,Doe Jane,95,100,ACTIVE\n" +
            "0009999999,Old Student,0,0,DROPPED\n");

        var gradebook = Load();

        Assert.IsTrue(gradebook.SectionChecks.Single().GradebookFound);
        Assert.AreEqual(3, gradebook.Records.Count);
        Assert.IsTrue(gradebook.Records.All(r => !r.AlreadyRescheduled));

        var failing = gradebook.Records.Single(r => r.Name == "Smith John");
        Assert.AreEqual(GradeStatus.Failing, failing.Status);
        Assert.IsTrue(failing.NeedsReschedule);
        Assert.AreEqual(GradeStatus.Passing, gradebook.Records.Single(r => r.Name == "Doe Jane").Status);
        Assert.AreEqual(GradeStatus.Inactive, gradebook.Records.Single(r => r.Name == "Old Student").Status);
    }

    [TestMethod]
    public void NonStudentRows_AreSkipped()
    {
        WriteGradebook("01", "Weight,Weight,,,\n,,,,\n0001234567,Smith John,60,100,ACTIVE\n");

        var record = Load().Records.Single();

        Assert.AreEqual("Smith John", record.Name);
    }

    // -------------------------------------------------------------------------
    // Reschedule list (class-wide CSVs at the root folder)
    // -------------------------------------------------------------------------

    [TestMethod]
    public void NoRescheduleFile_IsNotFound()
    {
        WriteGradebook("01", FailingSmith);

        var gradebook = Load();

        Assert.IsFalse(gradebook.RescheduleFileFound);
        Assert.IsTrue(gradebook.Records.Single().NeedsReschedule);
    }

    [TestMethod]
    public void StudentOnRescheduleList_IsFlaggedAndNoLongerNeedsReschedule()
    {
        WriteGradebook("01", FailingSmith);
        WriteAtRoot("reschedules.csv", "Student Name,Student ID#,Reason\nSmith John,0001234567,Illness\n");

        var gradebook = Load();

        var record = gradebook.Records.Single();
        Assert.IsTrue(gradebook.RescheduleFileFound);
        Assert.IsTrue(record.AlreadyRescheduled);
        Assert.IsFalse(record.NeedsReschedule);
        Assert.AreEqual("On Reschedule List", record.RescheduleStatusText);
    }

    [TestMethod]
    public void RescheduleFile_AnyNameWithMatchingHeader_IsAccepted()
    {
        WriteGradebook("01", FailingSmith);
        WriteAtRoot("Week 5 export.csv", "Exported 9/23/2026\nStudent Name,Student ID#,Reason\nSmith John,0001234567,Illness\n");

        var gradebook = Load();

        Assert.IsTrue(gradebook.RescheduleFileFound);
        Assert.IsTrue(gradebook.Records.Single().AlreadyRescheduled);
    }

    [TestMethod]
    public void CsvWithoutRescheduleHeader_IsIgnored()
    {
        WriteGradebook("01", FailingSmith);
        WriteAtRoot("reschedules.csv", Header + FailingSmith);

        var gradebook = Load();

        Assert.IsFalse(gradebook.RescheduleFileFound);
        Assert.IsFalse(gradebook.Records.Single().AlreadyRescheduled);
    }

    [TestMethod]
    public void MultipleRescheduleFiles_AreMerged()
    {
        WriteGradebook("01", FailingSmith + "0007654321,Doe Jane,0,40,ACTIVE\n");
        WriteAtRoot("a.csv", "Student Name,Student ID#\nSmith John,0001234567\n");
        WriteAtRoot("b.csv", "Student Name,Student ID\nDoe Jane,0007654321\n");

        var gradebook = Load();

        Assert.IsTrue(gradebook.Records.All(r => r.AlreadyRescheduled));
    }

    [TestMethod]
    public void RescheduleList_AppliesAcrossSections()
    {
        WriteGradebook("01", FailingSmith);
        WriteGradebook("04", FailingSmith);
        WriteAtRoot("reschedules.csv", "Student Name,Student ID#,Reason\nSmith John,0001234567,Illness\n");

        var gradebook = Load();

        Assert.AreEqual(2, gradebook.Records.Count);
        Assert.IsTrue(gradebook.Records.All(r => r.AlreadyRescheduled));
    }
}
