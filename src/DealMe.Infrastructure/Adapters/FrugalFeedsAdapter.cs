namespace DealMe.Infrastructure.Adapters;

using Microsoft.Extensions.Logging;

public sealed class FrugalFeedsAdapter : RssDealAdapter
{
    public FrugalFeedsAdapter(HttpClient http, ILogger<FrugalFeedsAdapter> logger)
        : base(http, logger) { }

    public override string AdapterName => "frugalfeeds";
    protected override string FeedUrl => "https://www.frugalfeeds.com.au/feed/";
}
