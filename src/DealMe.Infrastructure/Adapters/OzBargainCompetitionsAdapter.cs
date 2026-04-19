namespace DealMe.Infrastructure.Adapters;

using DealMe.Core.Domain.Enums;
using Microsoft.Extensions.Logging;

public sealed class OzBargainCompetitionsAdapter : RssDealAdapter
{
    public OzBargainCompetitionsAdapter(HttpClient http, ILogger<OzBargainCompetitionsAdapter> logger)
        : base(http, logger) { }

    public override string AdapterName => "ozbargain-competitions";
    // OzBargain's /tag/competitions/ and /tag/competition/ feeds are essentially dead —
    // /freebies/feed is the actual source of live free-entry listings (free apps, free
    // registrations, free services, giveaways). Our defensive auto-entry is scoped to
    // simple email forms so it will naturally skip anything that doesn't fit (DOB,
    // social login, receipt upload, etc.).
    protected override string FeedUrl => "https://www.ozbargain.com.au/freebies/feed";
    protected override OpportunityType DefaultType => OpportunityType.Competition;
}
