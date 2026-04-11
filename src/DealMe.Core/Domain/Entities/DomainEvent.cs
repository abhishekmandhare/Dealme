namespace DealMe.Core.Domain.Entities;

public class DomainEvent
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public Guid AggregateId { get; set; }
    public string AggregateType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid? CausationId { get; set; }
}
