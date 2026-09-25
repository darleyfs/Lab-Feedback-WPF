using Lab_Feedback_WPF.Models;
using Lab_Feedback_WPF.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public class GradebookServiceTests
{
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

    // -------------------------------------------------------------------------
    // LoadSection (per-section gradebook)
    // -------------------------------------------------------------------------

    [TestMethod]
    public void LoadSection_NoGradebookFile_ReturnsEmptyAndReportsMissing()
    {
        var (records, gradebookFound) = GradebookService.LoadSection(_folder, "01", new HashSet<string>());

        Assert.AreEqual(0, records.Count);
        Assert.IsFalse(gradebookFound);
    }

    [TestMethod]
    public void LoadSection_GradebookWithEmptyRescheduleSet_ClassifiesEveryRowNotRescheduled()
    {
        File.WriteAllText(Path.Combine(_folder, "Gradebook_01.csv"),
            "ID,Activity,Actual Grade,Best Possible,Status\n" +
            "0001234567,Smith John,0,40,ACTIVE\n" +
            "0007654321,Doe Jane,95,100,ACTIVE\n" +
            "0009999999,Old Student,0,0,DROPPED\n");

        var (records, gradebookFound) = GradebookService.LoadSection(_folder, "01", new HashSet<string>());

        Assert.AreEqual(3, records.Count);
        Assert.IsTrue(records.All(r => !r.AlreadyRescheduled));
        Assert.IsTrue(gradebookFound);

        var failing = records.Single(r => r.Name == "Smith John");
        Assert.AreEqual(GradeStatus.Failing, failing.Status);
        Assert.IsTrue(failing.NeedsReschedule);

        var passing = records.Single(r => r.Name == "Doe Jane");
        Assert.AreEqual(GradeStatus.Passing, passing.Status);

        var inactive = records.Single(r => r.Name == "Old Student");
        Assert.AreEqual(GradeStatus.Inactive, inactive.Status);
    }

    [TestMethod]
    public void LoadSection_StudentIdInRescheduleSet_IsFlaggedAndNoLongerNeedsReschedule()
    {
        File.WriteAllText(Path.Combine(_folder, "Gradebook_01.csv"),
            "ID,Activity,Actual Grade,Best Possible,Status\n" +
            "0001234567,Smith John,0,40,ACTIVE\n");

        var rescheduledIds = new HashSet<string> { GradeRecord.NormalizeId("0001234567") };
        var (records, gradebookFound) = GradebookService.LoadSection(_folder, "01", rescheduledIds);

        var record = records.Single();
        Assert.IsTrue(record.AlreadyRescheduled);
        Assert.IsFalse(record.NeedsReschedule);
        Assert.AreEqual("On Reschedule List", record.RescheduleStatusText);
        Assert.IsTrue(gradebookFound);
    }

    [TestMethod]
    public void LoadSection_NonStudentRows_AreSkipped()
    {
        File.WriteAllText(Path.Combine(_folder, "Gradebook_01.csv"),
            "ID,Activity,Actual Grade,Best Possible,Status\n" +
            "Weight,Weight,,,\n" +
            ",,,,\n" +
            "0001234567,Smith John,60,100,ACTIVE\n");

        var (records, _) = GradebookService.LoadSection(_folder, "01", new HashSet<string>());

        Assert.AreEqual(1, records.Count);
        Assert.AreEqual("Smith John", records[0].Name);
    }

    // -------------------------------------------------------------------------
    // LoadRescheduleList (single class-wide file, at the root folder)
    // -------------------------------------------------------------------------

    [TestMethod]
    public void LoadRescheduleList_NoFile_ReturnsEmptyAndNotFound()
    {
        var (ids, found) = GradebookService.LoadRescheduleList(_folder);

        Assert.AreEqual(0, ids.Count);
        Assert.IsFalse(found);
    }

    [TestMethod]
    public void LoadRescheduleList_FileAtRoot_ParsesNormalizedIds()
    {
        File.WriteAllText(Path.Combine(_folder, "reschedules.csv"),
            "Student Name,Student ID#,Reason\n" +
            "Smith John,0001234567,Illness\n" +
            "Doe Jane,0007654321,Family emergency\n");

        var (ids, found) = GradebookService.LoadRescheduleList(_folder);

        Assert.IsTrue(found);
        Assert.AreEqual(2, ids.Count);
        Assert.IsTrue(ids.Contains(GradeRecord.NormalizeId("0001234567")));
        Assert.IsTrue(ids.Contains(GradeRecord.NormalizeId("0007654321")));
    }

    [TestMethod]
    public void LoadRescheduleList_AnyFileNameWithMatchingFormat_IsAccepted()
    {
        File.WriteAllText(Path.Combine(_folder, "Week 5 export.csv"),
            "Exported 9/23/2026\n" +
            "Student Name,Student ID#,Reason\n" +
            "Smith John,0001234567,Illness\n");

        var (ids, found) = GradebookService.LoadRescheduleList(_folder);

        Assert.IsTrue(found);
        Assert.IsTrue(ids.Contains(GradeRecord.NormalizeId("0001234567")));
    }

    [TestMethod]
    public void LoadRescheduleList_CsvWithoutRescheduleHeader_IsIgnored()
    {
        File.WriteAllText(Path.Combine(_folder, "reschedules.csv"),
            "ID,Activity,Actual Grade,Best Possible,Status\n" +
            "0001234567,Smith John,0,40,ACTIVE\n");

        var (ids, found) = GradebookService.LoadRescheduleList(_folder);

        Assert.IsFalse(found);
        Assert.AreEqual(0, ids.Count);
    }

    [TestMethod]
    public void LoadRescheduleList_MultipleMatchingFiles_MergesIds()
    {
        File.WriteAllText(Path.Combine(_folder, "a.csv"),
            "Student Name,Student ID#\nSmith John,0001234567\n");
        File.WriteAllText(Path.Combine(_folder, "b.csv"),
            "Student Name,Student ID\nDoe Jane,0007654321\n");

        var (ids, found) = GradebookService.LoadRescheduleList(_folder);

        Assert.IsTrue(found);
        Assert.AreEqual(2, ids.Count);
    }

    [TestMethod]
    public void LoadRescheduleList_IdsApplyAcrossSections_WhenSharedWithLoadSection()
    {
        // The reschedule list is class-wide: one file at the root, cross-referenced
        // against every section's gradebook.
        File.WriteAllText(Path.Combine(_folder, "reschedules.csv"),
            "Student Name,Student ID#,Reason\n" +
            "Smith John,0001234567,Illness\n");

        var section01 = Path.Combine(_folder, "01");
        var section04 = Path.Combine(_folder, "04");
        Directory.CreateDirectory(section01);
        Directory.CreateDirectory(section04);
        File.WriteAllText(Path.Combine(section01, "Gradebook_01.csv"),
            "ID,Activity,Actual Grade,Best Possible,Status\n0001234567,Smith John,0,40,ACTIVE\n");
        File.WriteAllText(Path.Combine(section04, "Gradebook_04.csv"),
            "ID,Activity,Actual Grade,Best Possible,Status\n0001234567,Smith John,0,40,ACTIVE\n");

        var (rescheduledIds, found) = GradebookService.LoadRescheduleList(_folder);
        Assert.IsTrue(found);

        var (records01, _) = GradebookService.LoadSection(section01, "01", rescheduledIds);
        var (records04, _) = GradebookService.LoadSection(section04, "04", rescheduledIds);

        Assert.IsTrue(records01.Single().AlreadyRescheduled);
        Assert.IsTrue(records04.Single().AlreadyRescheduled);
    }

    // -------------------------------------------------------------------------
    // FindSectionsWithGradebooks
    // -------------------------------------------------------------------------

    [TestMethod]
    public void FindSectionsWithGradebooks_IncludesSectionsWithoutStudentFolders()
    {
        var csvOnly = Path.Combine(_folder, "01");
        Directory.CreateDirectory(csvOnly);
        File.WriteAllText(Path.Combine(csvOnly, "Gradebook_01.csv"), "ID,Activity\n1,Alice\n");

        var withStudents = Path.Combine(_folder, "04");
        Directory.CreateDirectory(Path.Combine(withStudents, "Doe_Jane-0007654321"));
        File.WriteAllText(Path.Combine(withStudents, "Gradebook_04.csv"), "ID,Activity\n2,Bob\n");

        Directory.CreateDirectory(Path.Combine(_folder, "Empty"));

        var sections = GradebookService.FindSectionsWithGradebooks(_folder);

        CollectionAssert.AreEquivalent(new[] { "01", "04" }, sections);
    }
}
