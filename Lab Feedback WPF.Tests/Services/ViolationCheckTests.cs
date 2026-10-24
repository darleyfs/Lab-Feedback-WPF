using Lab_Feedback_WPF.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public class ViolationCheckTests
{
    private static ViolationReport CheckCode(string code) =>
        ViolationCheck.Check(new Dictionary<string, string> { ["Main.cpp"] = code });

    private static int Count(string code) => CheckCode(code).Total;

    // -------------------------------------------------------------------------
    // Core word-boundary matching
    // -------------------------------------------------------------------------

    [TestMethod]
    public void ExactKeyword_IsDetected() => Assert.AreEqual(1, Count("auto x = 5;"));

    [TestMethod]
    public void KeywordPartOfLongerWord_IsNotDetected() => Assert.AreEqual(0, Count("int automatic = 0;"));

    [TestMethod]
    public void MultipleKeywordOccurrences_AllCounted() => Assert.AreEqual(2, Count("auto x = 0;\nauto y = 1;"));

    [TestMethod]
    public void MultipleDistinctTerms_AllCounted() => Assert.AreEqual(3, Count("try { } catch (...) { }")); // try, catch, (...)

    // -------------------------------------------------------------------------
    // Symbols
    // -------------------------------------------------------------------------

    [TestMethod]
    public void AddressOrReference_IsDetected() => Assert.AreEqual(1, Count("int* p = &x;"));

    [TestMethod]
    public void LogicalAnd_IsNotDetected() => Assert.AreEqual(0, Count("if (a && b) { }"));

    [TestMethod]
    public void NullTerminatorLiteral_IsDetected() => Assert.AreEqual(1, Count(@"char c = '\0';"));

    // -------------------------------------------------------------------------
    // Method-call pattern  (e.g. "resize()" matches any call to resize)
    // -------------------------------------------------------------------------

    [TestMethod]
    public void MethodPattern_MatchesNoArgCall() => Assert.AreEqual(1, Count("v.resize();"));

    [TestMethod]
    public void MethodPattern_MatchesSingleArgCall() => Assert.AreEqual(1, Count("v.resize(10);"));

    [TestMethod]
    public void MethodPattern_MatchesMultiArgCall() => Assert.AreEqual(1, Count("v.resize(n, 0);"));

    [TestMethod]
    public void MethodPattern_DoesNotMatchDifferentMethodWithSamePrefix() => Assert.AreEqual(0, Count("v.my_resize(10);"));

    [TestMethod]
    public void MethodPattern_IsCaseSensitive() => Assert.AreEqual(0, Count("v.Resize(10);"));

    [TestMethod]
    public void MethodPattern_CountsMultipleCalls() => Assert.AreEqual(2, Count("a.resize(5); b.resize(n);"));

    [TestMethod]
    public void MethodPattern_IgnoreAndClear() => Assert.AreEqual(2, Count("cin.ignore(100, 'x'); v.clear();"));

    // -------------------------------------------------------------------------
    // Comment stripping
    // -------------------------------------------------------------------------

    [TestMethod]
    public void KeywordInSingleLineComment_IsNotDetected() => Assert.AreEqual(0, Count("// try this approach"));

    [TestMethod]
    public void KeywordAfterInlineComment_IsNotDetected() => Assert.AreEqual(0, Count("int x = 0; // try again"));

    [TestMethod]
    public void KeywordInCodeBeforeComment_IsDetected() => Assert.AreEqual(1, Count("try { } // handle errors"));

    [TestMethod]
    public void KeywordInBlockComment_IsNotDetected() => Assert.AreEqual(0, Count("/* try to avoid exceptions */"));

    [TestMethod]
    public void KeywordInBlockCommentOnlyOneOfTwo_CountsOne() => Assert.AreEqual(1, Count("/* try */ try { }"));

    [TestMethod]
    public void KeywordInMultilineBlockComment_IsNotDetected() => Assert.AreEqual(1, Count("/*\n * try this\n */\ntry { }"));

    // -------------------------------------------------------------------------
    // Report
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Report_GivesLineNumbersPerFile()
    {
        var report = ViolationCheck.Check(new Dictionary<string, string>
        {
            ["C:/s/Main.cpp"] = "int x = 0;\nauto y = 1;",
            ["C:/s/Shop.cpp"] = "// auto in a comment\nint z;"
        });

        Assert.AreEqual(1, report.Total);
        CollectionAssert.AreEqual(new[] { 2 }, report.LinesIn("C:/s/Main.cpp").ToArray());
        Assert.AreEqual(0, report.LinesIn("C:/s/Shop.cpp").Count);
        Assert.AreEqual(0, report.LinesIn("C:/s/Other.cpp").Count);
    }

    [TestMethod]
    public void Summary_GroupsByFileName()
    {
        var report = ViolationCheck.Check(new Dictionary<string, string>
        {
            ["C:/s/Main.cpp"] = "auto y = 1;",
            ["C:/s/Clean.cpp"] = "int z;"
        });

        StringAssert.StartsWith(report.Summary, "Found 1 violation(s):");
        StringAssert.Contains(report.Summary, "── Main.cpp");
        StringAssert.Contains(report.Summary, "  Line 1: 'auto'");
        Assert.IsFalse(report.Summary.Contains("Clean.cpp"), "Files without violations are left out");
    }

    [TestMethod]
    public void Empty_HasNothing()
    {
        Assert.AreEqual(0, ViolationReport.Empty.Total);
        Assert.AreEqual(string.Empty, ViolationReport.Empty.Summary);
    }
}
