using System.ComponentModel.DataAnnotations;

namespace Common.Entities
{
    public abstract class BaseEntity
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public interface IAggregateRoot
    {
        List<IDomainEvent> DomainEvents { get; }
        void ClearDomainEvents();
    }

    public interface IDomainEvent
    {
        DateTime OccurredOn { get; }
    }

    public abstract class Entity : BaseEntity, IAggregateRoot
    {
        private readonly List<IDomainEvent> _domainEvents = new();

        public List<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly().ToList();

        protected void AddDomainEvent(IDomainEvent eventItem)
        {
            _domainEvents.Add(eventItem);
        }

        public void ClearDomainEvents()
        {
            _domainEvents.Clear();
        }
    }
}