using Clean_Connect.Domain.Enums;
using Clean_Connect.Domain.Events;
using Clean_Connect.Domain.Utilities;
using Clean_Connect.Domain.Value_Objects;

namespace Clean_Connect.Domain.Entities
{
    public class Client : BaseEntity
    {
        // Private constructor to prevent direct instantiation
        private Client() { }


        // Private constructor to initialize properties
        private Client(FullName name, Address address, Email email, Location location, Gender gender, PhoneNumber contact, string state, DateTime dob, string referralCode, string? createdBy = null)
        {
            FullName = name ?? throw new ArgumentNullException(nameof(name));
            Address = address ?? throw new ArgumentNullException(nameof(address));
            Email = email ?? throw new ArgumentNullException(nameof(email));
            Gender = gender;
            Location = location;
            PhoneNumber = contact ?? throw new ArgumentException(nameof(contact));
            ValidateState(state);
            State = state;
            ValidateDateOfBirth(dob);
            DateOfBirth = dob;
            ReferralCode = referralCode;
            UpdateMetadata(createdBy);
        }

        // Properties
        public FullName FullName { get; private set; } = default!;

        public Address Address { get; private set; } = default!;

        public Location Location { get; private set; } = default!;

        public List<Booking> Bookings { get; private set; }

     

        public Email Email { get; private set; } = default!;

        //Age is computed from DateOfBirth
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

        public Gender Gender { get; private set; } = default!;

        public PhoneNumber PhoneNumber { get; private set; } = default!;

        public string State { get; private set; } = default!;

        public DateTime DateOfBirth { get; private set; }

        public string ReferralCode { get; private set; } = default!;

        public Guid? ReferredById { get; private set; }

        public Client? ReferredBy { get; private set; }

        public int SuccessfulReferralCount { get; private set; }

        // Factory method to create a new Clients instance
        public static Client Create(FullName name, Address address, Email email, Location location, Gender gender, PhoneNumber contact, string state, DateTime dob, string referralCode, string? createdBy = null)
        {
            ValidateDateOfBirth(dob);
            ValidateState(state);

            var client = new Client(name, address, email, location, gender, contact, state, dob, referralCode);
            //Domain Event
            client.AddDomainEvent(new ClientCreatedDomainEvent(client.Id));

            client.UpdateMetadata(createdBy);
            return client;


        }

        public void SetReferrer(Guid referrerId)
        {
            if (referrerId == Id)
                throw new InvalidOperationException("A client cannot refer themselves.");

            ReferredById = referrerId;
        }

        public void IncrementSuccessfulReferral()
        {
            SuccessfulReferralCount++;
        }

        // Methods to update properties
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

        public void UpdateLocation(double Latitude, double Longitude, string? modifiedBy = null)
        {
            Location = Location.Create(Latitude, Longitude);
            UpdateMetadata(modifiedBy);
        }

        public void UpdateEmail(string newEmail, string? modifiedBy = null)
        {
            if (Email.Equals(newEmail))
                throw new InvalidOperationException("New email cannot be the same as the current email.");

            Email = Email.Create(newEmail);
            UpdateMetadata(modifiedBy);
        }
        public void UpdateContact(string newContact, string? modifiedBy = null)
        {
            PhoneNumber = PhoneNumber.Create(newContact);
            UpdateMetadata(modifiedBy);
        }

      
        public void UpdateState(string newState,  string? modifiedBy = null)
        {
            ValidateState(newState);
            State = newState;
            UpdateMetadata(modifiedBy);
        }

        public void UpdateDateOfBirth(DateTime newDob, string? modifiedBy = null)
        {
            ValidateDateOfBirth(newDob);
            DateOfBirth = newDob;
            UpdateMetadata(modifiedBy);
        }

public void UpdateGender(Gender gender, string? modifiedBy = null)
        {
            Gender = gender;
            UpdateMetadata(modifiedBy);
        }


        public static void ValidateDateOfBirth(DateTime dob)
        {
            if (dob.Date > DateTime.Today)
                throw new ArgumentOutOfRangeException(nameof(dob), "Date of birth cannot be in the future.");
            var today = DateTime.Today;
            var age = today.Year - dob.Year;
            if (dob.Date > today.AddYears(-age))
                age--;
            if (age < 15)
                throw new ArgumentOutOfRangeException(nameof(dob), "Client must be at least 15 years old.");
        }

        public static void ValidateState(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                throw new ArgumentException(nameof(state), "State cannot be null or empty.");
            if (state.Length < 3 || state.Length > 20)
                throw new ArgumentOutOfRangeException(nameof(State), "state name length must be between 3 and 20.");
        }
    }
}

