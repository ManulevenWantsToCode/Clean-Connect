namespace Clean_Connect.Application.DTO
{
    public record WorkerDto
    {
        public string Name { get; set; }

        public string Email { get; set; }

        public string Address { get; set; }

        public string Contact { get; set; }

        public string Gender { get; set; }

        public string ServiceType { get; set; }

        public double Rating { get; set; }
        public int Age { get; set;}

      
        public string Location { get; set; }
        public DateTime DateOfBirth {  get; set; }

        public string State { get; set; }

    }
}
