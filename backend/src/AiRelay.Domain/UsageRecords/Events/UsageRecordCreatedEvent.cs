using Leistd.EventBus.Core.Event;

namespace AiRelay.Domain.UsageRecords.Events;

public class UsageRecordCreatedEvent : BaseEvent
{
    public Guid UsageId { get; }
    public decimal Cost { get; }

    public UsageRecordCreatedEvent(Guid usageId, decimal cost)
    {
        UsageId = usageId;
        Cost = cost;
    }
}
