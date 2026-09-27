using Lab_Feedback_WPF.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Models;

[TestClass]
public class GradeRecordTests
{
    // -------------------------------------------------------------------------
    // CalculateStatus
    // -------------------------------------------------------------------------

    [TestMethod]
    public void CalculateStatus_NonActiveStatus_IsInactive()
    {
        Assert.AreEqual(GradeStatus.Inactive, GradeRecord.CalculateStatus("DROPPED", 100, 100));
    }

    [TestMethod]
    public void CalculateStatus_NullStatus_TreatedAsActive()
    {
        Assert.AreEqual(GradeStatus.Passing, GradeRecord.CalculateStatus(null, 90, 100));
    }

    [TestMethod]
    public void CalculateStatus_BestPossibleBelowThreshold_IsFailing()
    {
        Assert.AreEqual(GradeStatus.Failing, GradeRecord.CalculateStatus("ACTIVE", 0, 59.4));
    }

    [TestMethod]
    public void CalculateStatus_ActualGradeAboveThreshold_IsPassing()
    {
        Assert.AreEqual(GradeStatus.Passing, GradeRecord.CalculateStatus("ACTIVE", 60, 100));
    }

    [TestMethod]
    public void CalculateStatus_BetweenThresholds_IsTBD()
    {
        Assert.AreEqual(GradeStatus.TBD, GradeRecord.CalculateStatus("ACTIVE", 55, 80));
    }

    [TestMethod]
    public void CalculateStatus_CaseInsensitiveActive_IsRespected()
    {
        Assert.AreEqual(GradeStatus.Passing, GradeRecord.CalculateStatus("active", 90, 100));
    }

    // -------------------------------------------------------------------------
    // NormalizeId
    // -------------------------------------------------------------------------

    [TestMethod]
    public void NormalizeId_LeadingZeros_AreStripped()
    {
        Assert.AreEqual("5512345", GradeRecord.NormalizeId("0005512345"));
    }

    [TestMethod]
    public void NormalizeId_DifferentPaddingSameNumber_MatchesEqual()
    {
        Assert.AreEqual(GradeRecord.NormalizeId("0000012345"), GradeRecord.NormalizeId("12345"));
    }

    [TestMethod]
    public void NormalizeId_NonNumeric_FallsBackToTrimmedString()
    {
        Assert.AreEqual("ABC123", GradeRecord.NormalizeId("  ABC123  "));
    }

    // -------------------------------------------------------------------------
    // Derived properties
    // -------------------------------------------------------------------------

    [TestMethod]
    public void NeedsReschedule_FailingAndNotRescheduled_IsTrue()
    {
        var record = new GradeRecord("01", "Doe Jane", "123", GradeStatus.Failing, 0, 10, alreadyRescheduled: false);
        Assert.IsTrue(record.NeedsReschedule);
    }

    [TestMethod]
    public void NeedsReschedule_FailingButAlreadyRescheduled_IsFalse()
    {
        var record = new GradeRecord("01", "Doe Jane", "123", GradeStatus.Failing, 0, 10, alreadyRescheduled: true);
        Assert.IsFalse(record.NeedsReschedule);
    }

    [TestMethod]
    public void RescheduleStatusText_AlreadyRescheduled_TakesPrecedenceOverStatus()
    {
        var record = new GradeRecord("01", "Doe Jane", "123", GradeStatus.Failing, 0, 10, alreadyRescheduled: true);
        Assert.AreEqual("On Reschedule List", record.RescheduleStatusText);
    }

    [TestMethod]
    [DataRow("00", "CAMPUS")]
    [DataRow("01", "ONLINE")]
    [DataRow("04", "ONLINE")]
    [DataRow("", "ONLINE")]
    public void Modality_Section00IsCampus_OthersOnline(string section, string expected)
    {
        var record = new GradeRecord(section, "Smith John", "1234567", GradeStatus.Failing, 0, 40, false);

        Assert.AreEqual(expected, record.Modality);
    }

    [TestMethod]
    public void ToRescheduleRow_MatchesRescheduleSheetColumns()
    {
        var record = new GradeRecord("00", "Smith John", "1234567", GradeStatus.Failing, 0, 40, false);

        Assert.AreEqual("Smith John\t1234567\tCAMPUS\t\tDoug Arley", record.ToRescheduleRow(" Doug Arley "));
        Assert.AreEqual("Smith John\t1234567\tCAMPUS\t\t", record.ToRescheduleRow(null));
    }
}
