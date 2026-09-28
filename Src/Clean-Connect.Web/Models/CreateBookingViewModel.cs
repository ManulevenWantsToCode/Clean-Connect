using System.ComponentModel.DataAnnotations;

namespace Clean_Connect.Web.Models
{
    public class CreateBookingViewModel
    {
        [Required]
        public Guid WorkerId { get; set; }

        [Required]
        public Guid ServiceTypeId { get; set; }

        [Required]
        [Range(4.0, 14.0)]
        public double Latitude { get; set; }

        [Required]
        [Range(2.5, 15.5)]
        public double Longitude { get; set; }

        [Range(100, 100000)]
        public double RadiusInMeters { get; set; } = 10000;

        [Required]
        [DataType(DataType.Date)]
        public DateTime DateOfService { get; set; } = DateTime.Today.AddDays(1);

        [Required]
        public string TimeRange { get; set; } = "Morning";

        [Required]
        [DataType(DataType.Time)]
        public DateTime StartTime { get; set; } = DateTime.Today.AddHours(9);

        [Required]
        [DataType(DataType.Time)]
        public DateTime EndTime { get; set; } = DateTime.Today.AddHours(11);

        public string? CouponCode { get; set; }
    }
}
