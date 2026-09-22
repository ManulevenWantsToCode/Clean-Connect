using Clean_Connect.Domain.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Domain.Utilities
{
    public abstract class BaseEntity
    {
       
        private readonly List<DomainEvent> domainEvents = new();
        protected BaseEntity()
        {
            Id = Guid.NewGuid();
            DateCreated = DateTime.Now;
        }

        protected BaseEntity(Guid id)
        {
            Id = id;
            DateCreated = DateTime.Now;
        }

        public Guid Id { get; set; }

        public DateTime DateCreated { get; set; }

        public string? ModifiedBy { get; private set; } 

        public DateTime? DateModified { get; private set; }

        public string? CreatedBy { get;private set; }

        public bool IsDeleted { get;private set; }

         
        public IReadOnlyCollection<DomainEvent> DomainEvents => domainEvents.AsReadOnly();

        protected void AddDomainEvent(DomainEvent domainEvent)
        {
            domainEvents.Add(domainEvent);
        }

        public void ClearDomainEvents()
        {
            domainEvents.Clear();
        }

        //soft delete
        public virtual void MarkAsDeleted(string? deletedBy = null)
        {
            if(IsDeleted)
                throw new InvalidOperationException("Entity is already marked as deleted.");

            IsDeleted = true;
            ModifiedBy = deletedBy;
            DateModified = DateTime.Now;
           // AddDomainEvent(new EntityDeletedEvent(Id));

        }

        public virtual void Restore(string? restoredBy = null)
        {
            if (!IsDeleted)
                throw new InvalidOperationException("Entity is not marked as deleted.");

            IsDeleted = false;
            ModifiedBy = restoredBy;
            DateModified = DateTime.Now;
        }

        public virtual void UpdateMetadata(string?modifiedBy = null)
        {
            ModifiedBy = modifiedBy;
            DateModified = DateTime.Now;
        }

        public void Delete()
        {
            IsDeleted = true ;
        }
    }
}
