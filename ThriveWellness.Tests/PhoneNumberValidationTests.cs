using System.ComponentModel.DataAnnotations;
using ThriveWellness.Models;

namespace ThriveWellness.Tests;

// Guards the server-side [Phone] validation on BookingSubmitViewModel -
// investigated as part of the booking details phone field bug (the real
// cause turned out to be a static `readonly` attribute, not validation),
// but this pins down that the existing [Phone] rule was never the problem:
// it already accepts every format the fix needs to allow.
public class PhoneNumberValidationTests
{
    [Theory]
    [InlineData("0821234567")]
    [InlineData("+27821234567")]
    [InlineData("082 123 4567")]
    [InlineData("+27 82 555 0123")]
    [InlineData("(082) 123-4567")]
    public void PhoneNumber_AcceptsEveryFormatTheBookingFormNeedsToAllow(string phoneNumber)
    {
        var model = new BookingSubmitViewModel { PhoneNumber = phoneNumber };
        var context = new ValidationContext(model) { MemberName = nameof(BookingSubmitViewModel.PhoneNumber) };
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateProperty(phoneNumber, context, results);

        Assert.True(isValid, string.Join("; ", results.Select(r => r.ErrorMessage)));
    }
}
