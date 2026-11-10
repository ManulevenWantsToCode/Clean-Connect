using Clean_Connect.Application.Command.BookingCommand;
using Clean_Connect.Application.Command.Services;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Domain.Value_Objects;
using Xunit;

namespace Clean_Connect.Application.Tests;

public class BookingConcurrencyAndIdempotencyTests
{
    private readonly WorkerAvailabilityService _availability = new();

    [Fact]
    public void Overlapping_active_bookings_are_rejected()
    {
        var worker = WorkerWith(BookingAt(9, 12));
        Assert.False(_availability.IsWorkerAvailable(worker, Day, At(11), At(13)));
    }

    [Fact]
    public void Non_overlapping_and_adjacent_bookings_are_allowed()
    {
        var worker = WorkerWith(BookingAt(9, 11));
        Assert.True(_availability.IsWorkerAvailable(worker, Day, At(11), At(13)));
        Assert.True(_availability.IsWorkerAvailable(worker, Day, At(12), At(13)));
    }

    [Fact]
    public void Rejected_and_expired_bookings_release_availability()
    {
        var rejected = BookingAt(9, 11);
        rejected.Reject();
        var expired = BookingAt(12, 14);
        expired.MarkAsExpired();
        var worker = WorkerWith(rejected, expired);

        Assert.True(_availability.IsWorkerAvailable(worker, Day, At(9), At(11)));
        Assert.True(_availability.IsWorkerAvailable(worker, Day, At(12), At(14)));
    }

    [Fact]
    public void Completed_bookings_do_not_block_future_reservations()
    {
        var completed = BookingAt(9, 11);
        completed.Accept();
        completed.MarkAsPaid();
        completed.RequestStartJob();
        completed.StartJob();
        completed.MarkAsAwaitingClientConfirmation();
        completed.MarkAsCompleted();

        Assert.True(_availability.IsWorkerAvailable(WorkerWith(completed), Day, At(9), At(11)));
    }

    [Fact]
    public void Booking_rejects_invalid_intervals()
    {
        Assert.Throws<ArgumentException>(() => BookingAt(11, 11));
        Assert.Throws<ArgumentException>(() => BookingAt(12, 11));
    }

    [Fact]
    public void Booking_lifecycle_still_allows_valid_transitions()
    {
        var booking = BookingAt(9, 11);
        booking.Accept();
        booking.MarkAsPaid();
        booking.RequestStartJob();
        booking.StartJob();
        booking.MarkAsAwaitingClientConfirmation();
        booking.MarkAsCompleted();
        Assert.Equal(BookingStatus.Completed, booking.BookingStatus);
    }

    [Fact]
    public async Task Booking_creation_requires_an_idempotency_key()
    {
        var command = new CreateBookingCommand(Guid.NewGuid(), Guid.NewGuid(), 6.5, 3.3, 1000,
            Guid.NewGuid(), Day, "Morning", At(9), At(11));
        var result = await new CreateBookingValidator().ValidateAsync(command, TestContext.Current.CancellationToken);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateBookingCommand.IdempotencyKey));
    }

    private static readonly DateTime Day = new(2030, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    private static DateTime At(int hour) => Day.AddHours(hour);

    private static Booking BookingAt(int start, int end) => Booking.Create(
        Guid.NewGuid(), Guid.NewGuid(), Location.Create(6.5, 3.3), Day, DateTime.UtcNow,
        At(start), At(end), 100, 200, 200, 200, TimeRange.Morning,
        Address.Create("Lagos, Nigeria"), BookingStatus.Pending, PaymentStatus.Pending, Guid.NewGuid());

    private static Worker WorkerWith(params Booking[] bookings)
    {
        var worker = Worker.Create(FullName.Create("Test", "Worker"), Address.Create("Lagos, Nigeria"),
            PhoneNumber.Create("08012345678"), Location.Create(6.5, 3.3), Gender.Others,
            Guid.NewGuid(), Email.Create("worker@example.com"), "Lagos", DateTime.UtcNow.AddYears(-30), 1000);
        foreach (var booking in bookings)
            worker.Bookings.Add(booking);
        return worker;
    }
}
