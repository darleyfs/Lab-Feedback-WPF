using Lab_Feedback_WPF.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public class CsvUtilsTests
{
    // -------------------------------------------------------------------------
    // ParseLine
    // -------------------------------------------------------------------------

    [TestMethod]
    public void ParseLine_SimpleCommaSeparated_SplitsFields()
    {
        var fields = CsvUtils.ParseLine("a,b,c");
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, fields);
    }

    [TestMethod]
    public void ParseLine_QuotedFieldWithEmbeddedComma_KeepsCommaInField()
    {
        var fields = CsvUtils.ParseLine("Smith, John,\"123 Main, Apt 4\",99");
        // Note: the unquoted "Smith, John" itself splits on the comma (matches Python's csv module
        // behavior for unquoted fields) — the point of this test is the quoted field.
        Assert.AreEqual("123 Main, Apt 4", fields[2]);
    }

    [TestMethod]
    public void ParseLine_DoubledQuoteInsideQuotedField_UnescapesToSingleQuote()
    {
        var fields = CsvUtils.ParseLine("\"She said \"\"hi\"\"\",ok");
        Assert.AreEqual("She said \"hi\"", fields[0]);
        Assert.AreEqual("ok", fields[1]);
    }

    // -------------------------------------------------------------------------
    // ParseWithHeader
    // -------------------------------------------------------------------------

    [TestMethod]
    public void ParseWithHeader_NoDetector_UsesFirstLineAsHeader()
    {
        var path = WriteTempCsv("ID,Activity,Actual Grade\n1,Alice,90\n2,Bob,40\n");
        try
        {
            var (headers, rows) = CsvUtils.ParseWithHeader(path);

            CollectionAssert.AreEqual(new[] { "ID", "Activity", "Actual Grade" }, headers);
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("Alice", rows[0]["Activity"]);
            Assert.AreEqual("40", rows[1]["Actual Grade"]);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void ParseWithHeader_HeaderDetector_SkipsPreambleLines()
    {
        var path = WriteTempCsv(
            "Export generated 2026-01-01\n" +
            "\n" +
            "Student Name,Student ID,Reason\n" +
            "Doe Jane,00012345,Illness\n");
        try
        {
            var (headers, rows) = CsvUtils.ParseWithHeader(path,
                fields => fields.Contains("Student Name") && fields.Any(f => f.Contains("Student ID")));

            CollectionAssert.AreEqual(new[] { "Student Name", "Student ID", "Reason" }, headers);
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("00012345", rows[0]["Student ID"]);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void ParseWithHeader_BlankRows_AreSkipped()
    {
        var path = WriteTempCsv("ID,Activity\n1,Alice\n\n2,Bob\n");
        try
        {
            var (_, rows) = CsvUtils.ParseWithHeader(path);
            Assert.AreEqual(2, rows.Count);
        }
        finally { File.Delete(path); }
    }

    private static string WriteTempCsv(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"csvutils_test_{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content);
        return path;
    }
}
