using Lab_Feedback_WPF.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public class MarkerCheckTests
{
    // Made-up codes: this repo is public, so the real ones stay in the staff repo.
    private static readonly MarkerSet Set = MarkerSet.Parse("""
        { "term": "2026-11", "markers": { "bx": "Lab 3A", "fz": "Lab 4A" } }
        """);

    private static ViolationReport CheckCode(string code) =>
        MarkerCheck.Check(new Dictionary<string, string> { ["StudentWork.h"] = code }, Set);

    [TestMethod]
    public void CleanCode_HasNoMarkers() => Assert.AreEqual(0, CheckCode("int Test1(int years)\n{\n\treturn years * 365;\n}").Total);

    [TestMethod]
    public void SuffixedLocal_IsFound_AndNamesTheAssignment()
    {
        var marker = CheckCode("int Test1(int years)\n{\n\tint days_bx = years * 365;\n\treturn days_bx;\n}").ByFile["StudentWork.h"];
        CollectionAssert.AreEqual(new[] { 3, 4 }, marker.Select(m => m.LineNumber).ToList());
        Assert.AreEqual("days_bx", marker[0].MatchedText);
        Assert.AreEqual("section code for Lab 3A", marker[0].Context);
    }

    [TestMethod]
    public void CodeFromAnotherAssignment_NamesThatAssignment() =>
        Assert.AreEqual("section code for Lab 4A", CheckCode("int total_fz = 0;").ByFile["StudentWork.h"].Single().Context);

    [TestMethod]
    public void CodeInsideALongerWord_IsNotAMarker() => Assert.AreEqual(0, CheckCode("int days_bxy = 0; int box = 1;").Total);

    [TestMethod]
    public void CodeInAComment_IsNotAMarker() =>
        Assert.AreEqual(0, CheckCode("// every local variable you declare ends in `_bx` (e.g. `int total_bx = 0;`)\n/* total_bx */").Total);

    [TestMethod]
    public void SeveralMarkersOnOneLine_CountOnce() => Assert.AreEqual(1, CheckCode("int a_bx = 1, b_bx = 2;").Total);

    [TestMethod]
    public void Summary_SaysMarkers() => StringAssert.StartsWith(CheckCode("int a_bx = 1;").Summary, "Found 1 marker(s)");

    [TestMethod]
    public void Parse_RejectsMissingMarkers() =>
        Assert.ThrowsException<InvalidDataException>(() => MarkerSet.Parse("""{ "term": "2026-11" }"""));

    [TestMethod]
    public void Parse_RejectsCodesThatAreNotPlainLowercase() =>
        Assert.ThrowsException<InvalidDataException>(() => MarkerSet.Parse("""{ "markers": { "a|b": "Lab 3A" } }"""));
}
