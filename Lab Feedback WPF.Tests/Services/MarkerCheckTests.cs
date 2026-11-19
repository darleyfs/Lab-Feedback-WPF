using Lab_Feedback_WPF.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public class MarkerCheckTests
{
    private static readonly MarkerSet Set = MarkerSet.Parse("""
        { "term": "2026-11", "prefix": "tag0_", "label": "assisted:", "markers": { "tag0_abc": "Lab 3A" } }
        """);

    private static ViolationReport CheckCode(string code) =>
        MarkerCheck.Check(new Dictionary<string, string> { ["StudentWork.h"] = code }, Set);

    [TestMethod]
    public void CleanCode_HasNoMarkers() => Assert.AreEqual(0, CheckCode("int Test1(int years)\n{\n\treturn years * 365;\n}").Total);

    [TestMethod]
    public void LabelComment_IsFound_AndNamesTheAssignment()
    {
        var marker = CheckCode("int x;\n// assisted: Copilot tag0_abc\nint Test1(int years)").ByFile["StudentWork.h"].Single();
        Assert.AreEqual(2, marker.LineNumber);
        Assert.AreEqual("tag0_abc", marker.MatchedText);
        Assert.AreEqual("token for Lab 3A", marker.Context);
    }

    [TestMethod]
    public void PragmaRegion_IsFound() => Assert.AreEqual(1, CheckCode("#pragma region tag0_abc").Total);

    [TestMethod]
    public void HelperSuffix_IsFound() => Assert.AreEqual(1, CheckCode("int Sum_tag0_abc(int a, int b)").Total);

    [TestMethod]
    public void LabelWithTokenRemoved_IsStillFound() =>
        Assert.AreEqual("label without a token", CheckCode("// assisted: ChatGPT").ByFile["StudentWork.h"].Single().Context);

    [TestMethod]
    public void TokenFromAnotherTerm_IsReportedAsUnknown() =>
        Assert.AreEqual("unknown token", CheckCode("#pragma region tag0_zz9").ByFile["StudentWork.h"].Single().Context);

    [TestMethod]
    public void SeveralMarkersOnOneLine_CountOnce() => Assert.AreEqual(1, CheckCode("// assisted: Claude tag0_abc tag0_abc").Total);

    [TestMethod]
    public void Summary_SaysMarkers() => StringAssert.StartsWith(CheckCode("#pragma region tag0_abc").Summary, "Found 1 marker(s)");

    [TestMethod]
    public void Parse_RejectsMissingPrefix() =>
        Assert.ThrowsException<InvalidDataException>(() => MarkerSet.Parse("""{ "label": "assisted:", "markers": {} }"""));
}
