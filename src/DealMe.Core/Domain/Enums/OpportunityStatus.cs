namespace DealMe.Core.Domain.Enums;

public enum OpportunityStatus : byte
{
    New = 0,
    Seen = 1,
    Saved = 2,
    ActedOn = 3,
    Dismissed = 4,
    Expired = 5
}
