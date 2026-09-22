using Lab_Feedback_WPF.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Models;

[TestClass]
public class StudentSectionDetectionTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), $"student_section_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public void GetStudentsFromFolders_FlatStudentFolders_TreatedAsSingleSection()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Smith_John-0001234567"));
        Directory.CreateDirectory(Path.Combine(_root, "Doe_Jane-0007654321"));

        var students = Student.GetStudentsFromFolders(_root);

        Assert.AreEqual(2, students.Count);
        Assert.IsTrue(students.All(s => s.Section == string.Empty));
        Assert.IsTrue(students.Any(s => s.LastName == "Smith" && s.IdNumber == "0001234567"));
    }

    [TestMethod]
    public void GetStudentsFromFolders_SectionedLayout_TagsEachStudentWithItsSection()
    {
        var section01 = Path.Combine(_root, "01");
        var section04 = Path.Combine(_root, "04");
        Directory.CreateDirectory(Path.Combine(section01, "Smith_John-0001234567"));
        Directory.CreateDirectory(Path.Combine(section04, "Doe_Jane-0007654321"));

        var students = Student.GetStudentsFromFolders(_root);

        Assert.AreEqual(2, students.Count);
        Assert.AreEqual("01", students.Single(s => s.LastName == "Smith").Section);
        Assert.AreEqual("04", students.Single(s => s.LastName == "Doe").Section);
    }

    [TestMethod]
    public void GetStudentsFromFolders_SectionWithGradebookCsvAlongsideStudents_IgnoresTheCsv()
    {
        var section01 = Path.Combine(_root, "01");
        Directory.CreateDirectory(Path.Combine(section01, "Smith_John-0001234567"));
        File.WriteAllText(Path.Combine(section01, "Gradebook_01.csv"), "ID,Activity\n1,Alice\n");

        var students = Student.GetStudentsFromFolders(_root);

        Assert.AreEqual(1, students.Count);
        Assert.AreEqual("01", students[0].Section);
    }
}
