using SlimApp.Events.Bus;
using System.Collections.Generic;

namespace SlimApp.Domain.Entities
{
    public interface IGeneratesDomainEvents
    {
        ICollection<IEventData> DomainEvents { get; }
    }
}