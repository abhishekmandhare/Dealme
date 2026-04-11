namespace DealMe.Infrastructure.Adapters;

using Microsoft.Extensions.Logging;

public sealed class CheapiesAdapter : RssDealAdapter
{
    public CheapiesAdapter(HttpClient http, ILogger<CheapiesAdapter> logger)
        : base(http, logger) { }

    public override string AdapterName => "cheapies";
    protected override string FeedUrl => "https://www.cheapies.nz/deals/feed";
}
