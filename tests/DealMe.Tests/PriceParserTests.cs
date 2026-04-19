namespace DealMe.Tests;

using DealMe.Infrastructure.Adapters;
using Xunit;

public class PriceParserTests
{
    [Fact]
    public void ExtractFirst_returns_null_when_no_dollar_sign()
    {
        Assert.Null(PriceParser.ExtractFirst("Free pizza at IKEA today"));
    }

    [Theory]
    [InlineData("PlayStation 5 Slim $649", 649)]
    [InlineData("Coffee beans $24.99 at Coles", 24.99)]
    [InlineData("Sale! $1,299 off RRP", 1299)]
    public void ExtractFirst_parses_common_price_formats(string text, double expected)
    {
        Assert.Equal((decimal)expected, PriceParser.ExtractFirst(text));
    }

    [Fact]
    public void ExtractDealPricing_pulls_explicit_percent_off()
    {
        var (_, _, pct) = PriceParser.ExtractDealPricing("50% off sitewide at Kogan", null);
        Assert.Equal(50, pct);
    }

    [Fact]
    public void ExtractDealPricing_picks_now_price_over_first_price()
    {
        // The regex should prefer "now $267" over "was $500" for dealPrice.
        var (deal, original, _) = PriceParser.ExtractDealPricing(
            "Philips Shaver was $500, now $267", null);
        Assert.Equal(267m, deal);
        Assert.Equal(500m, original);
    }

    [Fact]
    public void ExtractDealPricing_derives_percent_from_was_now_pair()
    {
        var (_, _, pct) = PriceParser.ExtractDealPricing(
            "Beats Studio Pro was $550, now $220", null);
        // (550-220)/550 = 60%
        Assert.Equal(60, pct);
    }

    [Fact]
    public void ExtractDealPricing_handles_RRP_prefix()
    {
        var (_, original, _) = PriceParser.ExtractDealPricing(
            "Nespresso Essenza Mini $119 (RRP $229)", null);
        Assert.Equal(229m, original);
    }

    [Fact]
    public void ExtractDealPricing_derives_original_from_deal_and_percent()
    {
        // "$100 (50% off)" implies original was ~$200
        var (deal, original, pct) = PriceParser.ExtractDealPricing("$100 50% off", null);
        Assert.Equal(100m, deal);
        Assert.Equal(200m, original);
        Assert.Equal(50, pct);
    }

    [Fact]
    public void ExtractDealPricing_rejects_invalid_percent_values()
    {
        // 150% off isn't a real discount — guard against regex false positives.
        var (_, _, pct) = PriceParser.ExtractDealPricing("Save 150% off!", null);
        Assert.Null(pct);
    }

    [Fact]
    public void ExtractDealPricing_treats_Free_before_price_as_zero_deal()
    {
        // Regression: Cheapies title "Free - X (Was $24.99)" was parsing as $24.99/$24.99.
        var (deal, original, pct) = PriceParser.ExtractDealPricing(
            "[PC, Linux, Mac, Steam] Free - Legend of Keepers: Career of a Dungeon Manager (Was $24.99) @ Steam",
            null);
        Assert.Equal(0m, deal);
        Assert.Equal(24.99m, original);
        Assert.Equal(100, pct);
    }

    [Fact]
    public void ExtractDealPricing_does_not_zero_out_when_Free_follows_the_price()
    {
        // "$49 case + Free shipping" — the deal is $49, not $0.
        var (deal, _, _) = PriceParser.ExtractDealPricing(
            "$49 iPhone case + Free shipping at eBay", null);
        Assert.Equal(49m, deal);
    }

    [Fact]
    public void ExtractDealPricing_returns_nulls_for_text_without_prices()
    {
        var (deal, original, pct) = PriceParser.ExtractDealPricing(
            "Newsletter signup — no deal today", null);
        Assert.Null(deal);
        Assert.Null(original);
        Assert.Null(pct);
    }
}
