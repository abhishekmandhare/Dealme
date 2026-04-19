namespace DealMe.Tests;

using DealMe.Infrastructure.Services;
using Xunit;

public class QueryNormalizerTests
{
    [Theory]
    [InlineData("iPhone 16 Pro 256 GB", "iPhone 16 Pro 256gb")]
    [InlineData("Samsung SSD 2 TB", "Samsung SSD 2tb")]
    // Only spaced forms are normalised; already-joined "32GB" is left untouched.
    [InlineData("Corsair DDR 5 32GB RAM", "Corsair ddr5 32GB RAM")]
    [InlineData("Netgear Wi Fi 6 router", "Netgear wifi 6 router")]
    [InlineData("Apple Mac Book Air M4", "Apple macbook Air M4")]
    [InlineData("macbook  pro   16", "macbook pro 16")]  // multi-space collapse
    public void Normalize_applies_common_rewrites(string input, string expected)
    {
        Assert.Equal(expected, QueryNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_trims_surrounding_whitespace()
    {
        Assert.Equal("PS5 Slim", QueryNormalizer.Normalize("   PS5 Slim   "));
    }

    [Fact]
    public void Normalize_leaves_already_clean_query_untouched()
    {
        Assert.Equal("PS5 disc edition", QueryNormalizer.Normalize("PS5 disc edition"));
    }
}
