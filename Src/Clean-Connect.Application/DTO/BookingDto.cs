using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.DTO
{
    public record BookingDto
    {
        public Guid Id { get; set; }

        public string ServiceName { get; set; }

        public string ClientName { get; set; }
        public string WorkersName { get; set; }
        public double Rating { get; set; }
        public DateTime BookingDate { get; set; }
        public DateTime DateOfService { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public decimal HourlyRate { get; set; }
        public decimal TotalAmount { get; set; }

        public string TimeRange { get; set; }

        public decimal Amount { get; set; }

        public string Address { get; set; }

        public string PaymentStatus { get; set; }

        public string BookingStatus { get; set; }

        public bool HasRating { get; set; }
    }
}
