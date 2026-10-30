using ThriveWellness.Models;
using ThriveWellness.Services.Implementations;

namespace ThriveWellness.Tests;

public class SimulatedCardGatewayTests
{
    // Pinned to the EXPO date so "expired" never depends on when the
    // tests happen to run.
    private class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static SimulatedCardGateway CreateGateway() => new(new FixedClock(), TimeSpan.Zero);

    private static GatewayChargeRequest Request(string number, int month = 12, int year = 2030, string cvc = "123")
    {
        return new GatewayChargeRequest
        {
            Amount = 120m,
            Currency = "ZAR",
            BookingReference = "Booking 1",
            Card = new CardDetails
            {
                CardholderName = "Test Client",
                Number = number,
                ExpiryMonth = month,
                ExpiryYear = year,
                Cvc = cvc
            }
        };
    }

    [Theory]
    [InlineData("4242424242424242", true)]
    [InlineData("5555555555554444", true)]
    [InlineData("4000000000000002", true)]
    [InlineData("4000000000009995", true)]
    [InlineData("378282246310005", true)]
    [InlineData("4242424242424241", false)]
    [InlineData("1234567812345678", false)]
    [InlineData("", false)]
    [InlineData("4242-4242", false)]
    public void PassesLuhn_MatchesTheChecksum(string number, bool expected)
    {
        Assert.Equal(expected, SimulatedCardGateway.PassesLuhn(number));
    }

    [Theory]
    [InlineData("4242424242424242", CardBrands.Visa)]
    [InlineData("5555555555554444", CardBrands.Mastercard)]
    [InlineData("2223003122003222", CardBrands.Mastercard)]
    [InlineData("378282246310005", CardBrands.Amex)]
    [InlineData("341111111111111", CardBrands.Amex)]
    [InlineData("6011111111111117", CardBrands.Other)]
    [InlineData("", CardBrands.Other)]
    public void DetectBrand_RecognisesVisaMastercardAndAmex(string number, string expected)
    {
        Assert.Equal(expected, SimulatedCardGateway.DetectBrand(number));
    }

    [Theory]
    [InlineData("4242424242424242", CardBrands.Visa)]
    [InlineData("5555555555554444", CardBrands.Mastercard)]
    public async Task ChargeAsync_SuccessTestCards_SucceedWithAReferenceBrandAndLast4(string number, string brand)
    {
        var result = await CreateGateway().ChargeAsync(Request(number));

        Assert.True(result.Success);
        Assert.Equal(brand, result.Brand);
        Assert.Equal(number[^4..], result.Last4);
        Assert.Matches("^TW-[A-Z0-9]{10}$", result.GatewayReference);
        Assert.Null(result.FailureCode);
        Assert.Null(result.FailureMessage);
    }

    [Fact]
    public async Task ChargeAsync_DeclinedTestCard_IsDeclined()
    {
        var result = await CreateGateway().ChargeAsync(Request("4000000000000002"));

        Assert.False(result.Success);
        Assert.Equal("card_declined", result.FailureCode);
        Assert.Equal("Your card was declined.", result.FailureMessage);
        Assert.Equal("0002", result.Last4);
    }

    [Fact]
    public async Task ChargeAsync_InsufficientFundsTestCard_IsDeclined()
    {
        var result = await CreateGateway().ChargeAsync(Request("4000000000009995"));

        Assert.False(result.Success);
        Assert.Equal("insufficient_funds", result.FailureCode);
        Assert.Equal("Insufficient funds.", result.FailureMessage);
    }

    [Fact]
    public async Task ChargeAsync_AnyOtherValidCard_Succeeds()
    {
        var result = await CreateGateway().ChargeAsync(Request("4111111111111111"));

        Assert.True(result.Success);
        Assert.Equal(CardBrands.Visa, result.Brand);
    }

    [Fact]
    public async Task ChargeAsync_NumberFailingLuhn_IsRejectedWithoutAReference()
    {
        var result = await CreateGateway().ChargeAsync(Request("4242424242424241"));

        Assert.False(result.Success);
        Assert.Equal("invalid_number", result.FailureCode);
        Assert.Equal(string.Empty, result.GatewayReference);
    }

    [Theory]
    [InlineData(10, 2026)]
    [InlineData(12, 2025)]
    [InlineData(13, 2030)]
    [InlineData(0, 2030)]
    public async Task ChargeAsync_ExpiredOrInvalidExpiry_IsRejected(int month, int year)
    {
        var result = await CreateGateway().ChargeAsync(Request("4242424242424242", month, year));

        Assert.False(result.Success);
        Assert.Equal("expired_card", result.FailureCode);
    }

    [Fact]
    public async Task ChargeAsync_CardExpiringThisMonth_StillWorks()
    {
        var result = await CreateGateway().ChargeAsync(Request("4242424242424242", 11, 2026));

        Assert.True(result.Success);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("1234")]
    [InlineData("12a")]
    [InlineData("")]
    public async Task ChargeAsync_BadCvc_IsRejected(string cvc)
    {
        var result = await CreateGateway().ChargeAsync(Request("4242424242424242", cvc: cvc));

        Assert.False(result.Success);
        Assert.Equal("invalid_cvc", result.FailureCode);
    }

    [Fact]
    public async Task ChargeAsync_Amex_NeedsAFourDigitCvc()
    {
        var gateway = CreateGateway();

        var threeDigits = await gateway.ChargeAsync(Request("378282246310005", cvc: "123"));
        var fourDigits = await gateway.ChargeAsync(Request("378282246310005", cvc: "1234"));

        Assert.Equal("invalid_cvc", threeDigits.FailureCode);
        Assert.True(fourDigits.Success);
        Assert.Equal(CardBrands.Amex, fourDigits.Brand);
    }

    [Fact]
    public async Task RefundAsync_EarlierCharge_IsRefundedWithItsOwnReference()
    {
        var gateway = CreateGateway();
        var charge = await gateway.ChargeAsync(Request("4242424242424242"));

        var refund = await gateway.RefundAsync(charge.GatewayReference, 120m);

        Assert.True(refund.Success);
        Assert.Matches("^TW-RF[A-Z0-9]{8}$", refund.RefundReference);
    }

    [Fact]
    public async Task RefundAsync_NoChargeReference_IsRejected()
    {
        var refund = await CreateGateway().RefundAsync(string.Empty, 120m);

        Assert.False(refund.Success);
    }

    [Fact]
    public void CardDetails_ToString_NeverShowsTheCard()
    {
        var card = Request("4242424242424242").Card;

        var text = $"{card}";

        Assert.DoesNotContain("4242", text);
        Assert.DoesNotContain("123", text);
    }
}
