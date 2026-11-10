using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.DTO
{
    public record NearbyWorkerDto
    {
        public Guid WorkerId { get; init; }
        public string Name { get; set; }

        public string ServiceType { get; set; }

        public double DistanceInKm { get; set; }

        public double Rating { get; set; }
        public string State { get; init; }
        public decimal HourlyRate { get; init; }
    }
}
