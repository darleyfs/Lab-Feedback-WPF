using System.IO.Compression;
using Lab_Feedback_WPF.Models;
using Lab_Feedback_WPF.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public class SubmissionsTests
{
    private string _student = null!;

    [TestInitialize]
    public void CreateStudentFolder()
    {
        _student = Path.Combine(Path.GetTempPath(), "SubmissionsTests_" + Guid.NewGuid().ToString("N"), "Lovelace_Ada-123");
        Directory.CreateDirectory(_student);
    }

    [TestCleanup]
    public void DeleteStudentFolder() => Directory.Delete(Path.GetDirectoryName(_student)!, recursive: true);

    private void Write(string relativePath, string text = "")
    {
        var path = Path.Combine(_student, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private void Zip(string relativeZipPath, params (string Entry, string Text)[] entries)
    {
        var path = Path.Combine(_student, relativeZipPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (entry, text) in entries)
        {
            using var writer = new StreamWriter(archive.CreateEntry(entry).Open());
            writer.Write(text);
        }
    }

    private Assignment Assignment(string name) => new(name, Path.Combine(_student, name));

    // ─── PrepareAsync ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Prepare_UnzipsUpload_AndFindsTheAssignment()
    {
        Zip("upload.zip", ("Lab 3A/Lab 3A/Submission.cpp", "int main() {}"));

        var assignments = await Submissions.PrepareAsync(_student, null);

        Assert.AreEqual(1, assignments.Count);
        Assert.AreEqual("Lab 3A", assignments[0].Name);
        Assert.AreEqual(AssignmentKind.Lab, assignments[0].Kind);
        Assert.IsTrue(File.Exists(Path.Combine(_student, "Lab 3A", "Lab 3A", "Submission.cpp")));
        Assert.IsFalse(File.Exists(Path.Combine(_student, "upload.zip")), "Extracted zips are deleted");
    }

    [TestMethod]
    public async Task Prepare_KeepsLatestAttempt_AndRemovesAttemptFolders()
    {
        Zip("submission_2026-10-01_10-00-00/a.zip", ("PracticalShop/PracticalShop.cpp", "old"));
        Zip("submission_2026-10-05_10-00-00/b.zip", ("PracticalShop/PracticalShop.cpp", "new"));

        var assignments = await Submissions.PrepareAsync(_student, null);

        Assert.AreEqual("PracticalShop", assignments.Single().Name);
        Assert.AreEqual(AssignmentKind.Practical, assignments[0].Kind);
        Assert.AreEqual("new", File.ReadAllText(Path.Combine(_student, "PracticalShop", "PracticalShop.cpp")));
        Assert.IsFalse(Directory.GetDirectories(_student).Any(d => Path.GetFileName(d).StartsWith("submission_")));
    }

    [TestMethod]
    public async Task Prepare_SkipsDotFolders_AndBuildOutput()
    {
        Write("Lab 2/Submission.cpp");
        Write(".vs/Lab 4A/x.txt");
        Write("bin/Lab 5A/x.txt");

        var assignments = await Submissions.PrepareAsync(_student, null);

        CollectionAssert.AreEqual(new[] { "Lab 2" }, assignments.Select(a => a.Name).ToArray());
    }

    [TestMethod]
    public async Task Prepare_OnlyCreatesProgressWhenThereIsSomethingToUnzip()
    {
        Write("Lab 2/Submission.cpp");
        var created = false;

        await Submissions.PrepareAsync(_student, () => { created = true; throw new AssertFailedException("no zips, no dialog"); });

        Assert.IsFalse(created);
    }

    [TestMethod]
    public async Task Prepare_IgnoresZipEntriesThatEscapeTheFolder()
    {
        Zip("upload.zip", ("../escaped.txt", "nope"), ("Lab 2/Submission.cpp", ""));

        await Submissions.PrepareAsync(_student, null);

        Assert.IsFalse(File.Exists(Path.Combine(Path.GetDirectoryName(_student)!, "escaped.txt")));
        Assert.IsTrue(File.Exists(Path.Combine(_student, "Lab 2", "Submission.cpp")));
    }

    [TestMethod]
    public async Task Prepare_MissingFolder_FindsNothing()
    {
        Assert.AreEqual(0, (await Submissions.PrepareAsync(Path.Combine(_student, "nope"), null)).Count);
    }

    // ─── Open ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Open_Lab_HidesStarterFiles_AndOpensSubmissionFirst()
    {
        Write("Lab 3A/Lab 3A/StudentWork.h");
        Write("Lab 3A/Lab 3A/Submission.cpp");
        Write("Lab 3A/Lab 3A/Lab3A.cpp");
        Write("Lab 3A/Lab 3A/Utility.h");
        Write("Lab 3A/x64/Debug/hdkvkt.txt", "2026-10-01 10:00:00 - 40\n2026-10-01 10:05:00 - 85\n");
        Write("Lab 3A/x64/Debug/results.json", "{\"name\": \"Lab 3A\", \"results\": [{\"name\": \"Add\", \"pointsTotal\": 10, \"pointsReceived\": 10}]}");

        var submission = Submissions.Open(Assignment("Lab 3A"));

        CollectionAssert.AreEquivalent(new[] { "StudentWork.h", "Submission.cpp" },
            submission.StudentFiles.Select(Path.GetFileName).ToArray());
        Assert.AreEqual("Submission.cpp", Path.GetFileName(submission.PrimaryFile));
        Assert.AreEqual("Lab 3A", submission.Results!.Name);
        Assert.AreEqual(2, submission.BuildLog!.Count);
        Assert.AreEqual(85, submission.BuildLog[^1].Number);
        Assert.AreEqual(0, submission.Problems.Count);
    }

    [TestMethod]
    public void Open_Lab_FallsBackToStudentWork()
    {
        Write("Lab 2/Lab 2/StudentWork.h");
        Write("Lab 2/Lab 2/Other.cpp");

        Assert.AreEqual("StudentWork.h", Path.GetFileName(Submissions.Open(Assignment("Lab 2")).PrimaryFile));
    }

    [TestMethod]
    public void Open_CurrentPractical_OpensFileNamedAfterProject_AndReadsBuildLog()
    {
        Write("PracticalShop/PracticalShop/Inventory.cpp");
        Write("PracticalShop/PracticalShop/PracticalShop.cpp");
        Write("PracticalShop/PracticalShop/ShopUtils.cpp");
        Write("PracticalShop/x64/Debug/hdkvkt.txt", "2026-10-01 10:00:00 - 72\n");

        var submission = Submissions.Open(Assignment("PracticalShop"));

        Assert.AreEqual("PracticalShop.cpp", Path.GetFileName(submission.PrimaryFile));
        Assert.IsFalse(submission.StudentFiles.Any(f => f.EndsWith("ShopUtils.cpp")));
        Assert.AreEqual(72, submission.BuildLog!.Single().Number);
    }

    [TestMethod]
    public void Open_OldMidtermPractical_OpensHighScoreTable()
    {
        Write("Midterm_Practical_Highscore/HighScore_Table/Helper.cpp");
        Write("Midterm_Practical_Highscore/HighScore_Table/HighScore_Table.cpp");
        Write("Midterm_Practical_Highscore/HighScore_Table/Aaa.cpp");

        var submission = Submissions.Open(Assignment("Midterm_Practical_Highscore"));

        Assert.AreEqual("HighScore_Table.cpp", Path.GetFileName(submission.PrimaryFile));
    }

    [TestMethod]
    public void Open_UnknownLayout_FallsBackToFirstStudentFile()
    {
        Write("PracticalNew/Main.cpp");

        Assert.AreEqual("Main.cpp", Path.GetFileName(Submissions.Open(Assignment("PracticalNew")).PrimaryFile));
    }

    [TestMethod]
    public void Open_MissingBuildLogAndResults_AreReportedNotThrown()
    {
        Write("Lab 2/Submission.cpp");

        var submission = Submissions.Open(Assignment("Lab 2"));

        Assert.IsNull(submission.BuildLog);
        Assert.IsNull(submission.Results);
        CollectionAssert.AreEqual(new[] { "Build logs not found for Lab 2." }, submission.Problems.ToArray());
    }

    [TestMethod]
    public void Open_NoStudentFiles_ReportsFolderProblem()
    {
        Write("Lab 2/Utility.h");

        var submission = Submissions.Open(Assignment("Lab 2"));

        Assert.IsNull(submission.PrimaryFile);
        CollectionAssert.Contains(submission.Problems.ToList(), "There may be a problem with the folder structure.");
    }

    [TestMethod]
    public void Open_BrokenResultsJson_IsReported()
    {
        Write("Lab 2/Submission.cpp");
        Write("Lab 2/results.json", "{ not json");

        var submission = Submissions.Open(Assignment("Lab 2"));

        Assert.IsNull(submission.Results);
        CollectionAssert.Contains(submission.Problems.ToList(), "The autograder's results.json could not be read.");
    }
}
