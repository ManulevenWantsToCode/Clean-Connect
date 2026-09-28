using Clean_Connect.Domain.Utilities;

namespace Clean_Connect.Domain.Value_Objects
{
    public class FullName : ValueObjects
    {
        public string FirstName { get; }
        public string LastName { get; }

        private FullName() { }
        private FullName(string firstName, string lastName)
        { 

            FirstName = firstName;
            LastName = lastName;

        }

        public static FullName Create(string firstName, string lastName)
        {

            if (string.IsNullOrEmpty(firstName))
                throw new ArgumentException("First name cannot be null or empty", nameof(firstName));

            if (string.IsNullOrEmpty(lastName))
                throw new ArgumentException("Last name cannot be null or empty", nameof(lastName));

            if (firstName.Length > 50)
                throw new ArgumentOutOfRangeException("First name cannot be longer than 50 characters", nameof(firstName));

            if (lastName.Length > 50)
                throw new ArgumentOutOfRangeException("Last name cannot be longer than 50 characters", nameof(lastName));

            return new FullName(firstName.Trim(), lastName.Trim());
        }

        
            

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return FirstName;
            yield return LastName;
        }

        public override string ToString()
        {
            return $"{FirstName} {LastName}";
        }

        public static implicit operator string(FullName fullName) => fullName.ToString();
    }
}
