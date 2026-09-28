using Clean_Connect.Domain.Events;
using Clean_Connect.Domain.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Domain.Entities
{
    public class ServiceType : BaseEntity
    {
        private ServiceType () { }

        private ServiceType(string name, string description, string? createdBy = null)
        {
            Name =  name ?? throw new ArgumentNullException ("name");
            Description = description ?? throw new ArgumentNullException ("description");
        }

        public string Name { get; private set; } = default!;

        public string Description { get; private set; } = default!;

        public List<Worker> Workers { get; private set; } = new List<Worker>(); 

        public static ServiceType Create (string name, string description, string? createdBy = null)
        {
            Validate(name, description);
            var service = new ServiceType(name, description, createdBy);
            service.AddDomainEvent(new ServiceTypeCreatedEvent(service.Id));
            service.UpdateMetadata(createdBy);
            return service;
        }

        public void UpdateService(string newName, string newDescription, string? modifiedBy = null)
        {
            Name = newName;
            Description = newDescription;

            UpdateMetadata(modifiedBy);
        }

        private static void Validate(string name, string description, string createdBy = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name),"Name cannot be null or empty");


            if(string.IsNullOrWhiteSpace(description))
                throw new ArgumentNullException(nameof(description), "description cannot be null or empty");

           

            if (name.Length < 10 || name.Length > 50)
                throw new ArgumentOutOfRangeException(nameof(name), "Name length must be between 10 and 50.");

            if (description.Length < 10 || description.Length > 200)
                throw new ArgumentOutOfRangeException(nameof(name), "Description length must be between 10 and 200.");
        }
    }
}
