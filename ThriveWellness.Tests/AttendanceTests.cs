using ThriveWellness.Models;
using ThriveWellness.Services;

namespace ThriveWellness.Tests;

public class AttendanceTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0);
    private static readonly DateTime Yesterday = Now.AddDays(-1);

    [Fact]
    public void PaidPastClass_IsAttended()
    {
        Assert.True(Attendance.IsAttended("Confirmed", false, PaymentStatuses.Confirmed, Yesterday, Now));
    }

    [Fact]
    public void UnpaidPastClass_IsNotAttended()
    {
        Assert.False(Attendance.IsAttended("Awaiting Payment", false, PaymentStatuses.Pending, Yesterday, Now));
    }

    [Theory]
    [InlineData(PaymentStatuses.Confirmed)]
    [InlineData(PaymentStatuses.Refunded)]
    [InlineData(PaymentStatuses.RefundDue)]
    public void CancelledClass_IsNotAttended(string paymentStatus)
    {
        Assert.False(Attendance.IsAttended("Cancelled", false, paymentStatus, Yesterday, Now));
    }

    [Fact]
    public void FuturePaidClass_IsNotAttendedYet()
    {
        Assert.False(Attendance.IsAttended("Confirmed", false, PaymentStatuses.Confirmed, Now.AddMinutes(1), Now));
    }

    [Fact]
    public void ClassThatHasJustStarted_IsAttended()
    {
        Assert.True(Attendance.IsAttended("Confirmed", false, PaymentStatuses.Confirmed, Now, Now));
    }

    [Fact]
    public void NoShow_IsNotAttended()
    {
        Assert.False(Attendance.IsAttended("Confirmed", true, PaymentStatuses.Confirmed, Yesterday, Now));
    }

    [Fact]
    public void ConfirmedBookingWithoutAPaymentRow_IsAttended()
    {
        Assert.True(Attendance.IsAttended("Confirmed", false, null, Yesterday, Now));
    }

    [Fact]
    public void AwaitingPaymentBookingWithoutAPaymentRow_IsNotAttended()
    {
        Assert.False(Attendance.IsAttended("Awaiting Payment", false, null, Yesterday, Now));
    }
}
