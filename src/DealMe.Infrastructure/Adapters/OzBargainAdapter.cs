namespace DealMe.Infrastructure.Adapters;

using Microsoft.Extensions.Logging;

public sealed class OzBargainAdapter : RssDealAdapter
{
    public OzBargainAdapter(HttpClient http, ILogger<OzBargainAdapter> logger)
        : base(http, logger) { }

    public override string AdapterName => "ozbargain";
    protected override string FeedUrl => "https://www.ozbargain.com.au/deals/feed";
}
