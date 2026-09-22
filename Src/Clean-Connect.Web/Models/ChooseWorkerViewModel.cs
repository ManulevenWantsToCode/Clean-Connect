using Microsoft.AspNetCore.Mvc.Rendering;

namespace Clean_Connect.Web.Models
{
    public class WorkerChoiceItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public Guid ServiceTypeId { get; set; }
        public string ServiceName { get; set; } = default!;
        public double Rating { get; set; }
        public int TotalRating { get; set; }
        public decimal Amount { get; set; }
        public string? State { get; set; }
        public double? DistanceInKm { get; set; }
        public int Age { get; set; }
    }

    public class ChooseWorkerViewModel
    {
        public Guid? SelectedServiceTypeId { get; set; }

        public DateTime DateOfService { get; set; } = DateTime.Today.AddDays(1);

        public string TimeRange { get; set; } = "Morning";

        public double? SelectedRadiusInMeters { get; set; }

        public List<SelectListItem> RadiusOptions { get; set; } = new()
        {
            new SelectListItem { Value = "", Text = "Any distance" },
            new SelectListItem { Value = "5000", Text = "Within 5 km" },
            new SelectListItem { Value = "10000", Text = "Within 10 km" },
            new SelectListItem { Value = "20000", Text = "Within 20 km" },
            new SelectListItem { Value = "50000", Text = "Within 50 km" }
        };

        public List<WorkerChoiceItem> Workers { get; set; } = new();

        public IEnumerable<SelectListItem>? ServiceTypes { get; set; } = new List<SelectListItem>();

        public List<SelectListItem> TimeRanges { get; set; } = new();

        public int AvailableCount => Workers.Count;
    }
}