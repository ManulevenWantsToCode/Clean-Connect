using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.Command.Services
{
    public class WorkerAvailabilityService
    {
        public bool IsWorkerAvailable(Worker worker, DateTime desiredDate, DateTime desiredStart, DateTime desiredEnd)
        {
            var reservingStatuses = new[]
            {
                BookingStatus.Pending, BookingStatus.AcceptedAwaitingPayment, BookingStatus.MarkAsPaid,
                BookingStatus.AwaitingClientStartConfirmation, BookingStatus.InProgress,
                BookingStatus.AwaitingClientConfirmation
            };
            return !worker
                .Bookings
                .Any(booking =>
                !booking.IsDeleted &&
                reservingStatuses.Contains(booking.BookingStatus) &&
                booking.StartTime < desiredEnd && desiredStart < booking.EndTime
                );
        }

        public bool IsWorkerAvailableForDay(Worker worker, DateTime desiredDate)
        {
            var active = new[] { BookingStatus.Pending, BookingStatus.AcceptedAwaitingPayment, BookingStatus.MarkAsPaid, BookingStatus.AwaitingClientStartConfirmation, BookingStatus.InProgress, BookingStatus.AwaitingClientConfirmation };

            return !worker
                .Bookings
                .Any(booking =>
                booking.DateOfService.Date == desiredDate.Date &&
                active.Contains(booking.BookingStatus));
        }
    }
}
