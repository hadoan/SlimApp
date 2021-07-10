using Abp.Events.Bus;
using System.Collections.Generic;

namespace Abp.Domain.Entities
{
    public interface IGeneratesDomainEvents
    {
        ICollection<IEventData> DomainEvents { get; }
    }
}