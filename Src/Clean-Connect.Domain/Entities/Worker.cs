using Clean_Connect.Domain.Enums;
using Clean_Connect.Domain.Events;
using Clean_Connect.Domain.Utilities;
using Clean_Connect.Domain.Value_Objects;

namespace Clean_Connect.Domain.Entities
{
    public class Worker : BaseEntity
    {
        private Worker() { }

        private Worker(FullName name, Address address,Location location, PhoneNumber contact, Gender gender, Guid ServiceType, Email email, string state, DateTime dob, decimal hourlyRate, string? createdBy = null)
        {
            FullName = name ?? throw new ArgumentNullException("name");
            Address = address ?? throw new ArgumentNullException("address");
            Contact = contact ?? throw new ArgumentNullException("contact");
            Location = location ?? throw new ArgumentNullException("location"); 
            Gender = gender ;
            ServiceTypeId = ServiceType;
            Email = email ?? throw new ArgumentNullException("email");
            State = state ?? throw new ArgumentNullException("state");
            ValidateDateOfBirth(dob);
            DateOfBirth = dob;
            HourlyRate = ValidateHourlyRate(hourlyRate);

            UpdateMetadata(createdBy);
        }

        public FullName FullName { get; private set; } = default!;

        public Address Address { get; private set; } = default!;

        public PhoneNumber Contact { get; private set; } = default!;

        public Location Location { get; private set; } = default!;
        
        public ServiceType ServiceType { get; private set; } = default!;    

        public List<Booking> Bookings { get; private set; } = new List<Booking>();

        public ICollection<Ratings> Ratings { get; private set; } = new List<Ratings>();

        public double AverageRating { get; private set; }

        public int TotalRating { get; private set; }
        public Guid ServiceTypeId { get; private set; } = default!;

        public Gender Gender { get; private set; } = default!;

        public Email Email { get; private set; } = default!;
 

        public string State { get; private set; } = default!;

        public bool IsAvailable { get; private set; } = true;

        public DateTime DateOfBirth { get; private set; } = default!;

        public decimal HourlyRate { get; private set; }

        public int Age
        {
            get
            {
                var today = DateTime.Today;

                var age = today.Year - DateOfBirth.Year;

                if (DateOfBirth.Date > today.AddYears(-age)) age--;
                return age;
            }
        }

        public static Worker Create(FullName name, Address address, PhoneNumber contact,Location location, Gender gender, Guid serviceTypeId, Email email, string state, DateTime dob, decimal hourlyRate, string? createdBy = null)
        {
            ValidateDateOfBirth(dob);
            ValidateState(state);
            var worker = new Worker(name, address,location, contact, gender, serviceTypeId, email, state, dob, hourlyRate);

            //domain event
            worker.AddDomainEvent(new WorkerCreatedDomainEvent(worker.Id));
            worker.UpdateMetadata(createdBy);
            return worker;
        }

        public void UpdateLocation(double Latitude, double Longitude, string? modifiedBy = null)
        {
            Location = Location.Create(Latitude, Longitude);
            UpdateMetadata(modifiedBy);
        }

        public void UpdateRating(int newTotalRating, double newAverageRating, string? modifiedBy = null)
        {
            TotalRating = newTotalRating;
            AverageRating = newAverageRating;
            UpdateMetadata(modifiedBy);
        }

        public void UpdateState(string state, string? modifiedBy = null)
        {
            ValidateState(state);
            State = state.Trim();
            UpdateMetadata(modifiedBy);
        }

        public void UpdateServiceType(Guid newServiceTypeId, string? modifiedBy = null)
        {
            if (newServiceTypeId == Guid.Empty)
                throw new ArgumentException("Service Type Id cannot be empty.", nameof(newServiceTypeId));
            ServiceTypeId = newServiceTypeId;
            UpdateMetadata(modifiedBy);
        }
        public void UpdateName(string newFirstName, string newLastName, string? modifiedBy = null)
        {

            FullName = FullName.Create(newFirstName, newLastName);
            UpdateMetadata(modifiedBy);
        }

        public void UpdateAddress(string newAddress, string? modifiedBy = null)
        {

            Address = Address.Create(newAddress);
            UpdateMetadata(modifiedBy);
        }
        public void UpdateGender(Gender gender, string? modifiedBy = null)
        {

            if (!Enum.IsDefined(typeof(Gender), gender))
                throw new InvalidOperationException("Invalid gender value");

            Gender = gender;
            UpdateMetadata(modifiedBy);
        }
        public void UpdateEmail(string newEmail, string? modifiedBy = null)
        {

            Email = Email.Create(newEmail.Trim().ToLowerInvariant());
            UpdateMetadata(modifiedBy);
        }

        public void UpdateContact(string newContact, string? modifiedBy = null)
        {

            Contact = PhoneNumber.Create(newContact.Trim());
            UpdateMetadata(modifiedBy);
        }

        public void UpdateDateOfBirth(DateTime newDob, string? modifiedBy = null)
        {
            ValidateDateOfBirth(newDob);

            DateOfBirth = newDob;

            UpdateMetadata(modifiedBy);
        }

        public void UpdateHourlyRate(decimal newHourlyRate, string? modifiedBy = null)
        {
            HourlyRate = ValidateHourlyRate(newHourlyRate);

            UpdateMetadata(modifiedBy);
        }

        
        private static void ValidateState(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                throw new ArgumentException("State can't be empty", nameof(state));
            if (state.Length < 3 || state.Length > 15)
                throw new ArgumentOutOfRangeException("State name length can't be less than 3 or greater than 15", nameof(state));
        }

        private static void ValidateDateOfBirth(DateTime dob)
        {
            if (dob > DateTime.UtcNow)
                throw new ArgumentNullException("Date of birth can't be in the future", nameof(dob));

            var today = DateTime.UtcNow;
            var age = today.Year - dob.Year;

            if (dob.Date > today.AddYears(-age))

                age--;

            if (age < 18)
                throw new ArgumentException("Worker must be at least 18 years old", nameof(dob));


        }

        private static decimal ValidateHourlyRate(decimal hourlyRate)
        {
            if (hourlyRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(hourlyRate), "Hourly rate must be greater than zero");
            if (hourlyRate > 1_000_000)
                throw new ArgumentOutOfRangeException(nameof(hourlyRate), "Hourly rate cannot exceed 1,000,000");

            return Math.Round(hourlyRate, 2);
        }
    }
}
