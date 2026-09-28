using Clean_Connect.Application.Command.Services;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Application.Query.WorkersQuery;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Domain.Value_Objects;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.BookingCommand
{
    public record CreateBookingCommand(Guid ClientId, Guid WorkerId, double Latitude, double Longitude, double RadiusInMeters, Guid ServiceTypeId, DateTime DateOfService, string TimeRange, DateTime StartTime, DateTime EndTime, string? CouponCode = null, string? CreatedBy = null) : IRequest<Guid>;

    public class CreateBookingValidator : AbstractValidator<CreateBookingCommand>
    {
        public CreateBookingValidator()
        {
            RuleFor(x => x.WorkerId)
                .NotEmpty()
                .WithMessage("Worker Id is required")
                .Must(id => id != Guid.Empty)
                .WithMessage("Invalid Id");

            RuleFor(x => x.ClientId)
                .NotEmpty()
                .WithMessage("Client Id is required")
                .Must(id => id != Guid.Empty)
                .WithMessage("Invalid Id");

            RuleFor(x => x.ServiceTypeId)
                .NotEmpty()
                .WithMessage("Service type Id is required")
                .Must(id => id != Guid.Empty)
                .WithMessage("Invalid Id");

            RuleFor(x => x.Latitude)
                .InclusiveBetween(4.0, 14.0)
                .WithMessage("Latitude must be between 4.0 and 14.0");

            RuleFor(x => x.Longitude)
                .InclusiveBetween(-180, 180)
                .WithMessage("Longitude must be between 2.5 and 15.5.");

            RuleFor(x => x.StartTime)
                .NotEmpty()
                .WithMessage("Start time is required");

            RuleFor(x => x.EndTime)
                .NotEmpty()
                .WithMessage("End time is required");

            RuleFor(x => x.EndTime)
                .GreaterThan(x => x.StartTime)
                .WithMessage("End time must be after start time.");


        }


    }

    public class CreateBookingHandler(IUnitOfWork repo, BookingRuleService bookingRuleService, GeocodingService geocodingService, INotificationService notificationService, ILogger<CreateBookingHandler> logger) : IRequestHandler<CreateBookingCommand, Guid>
    {
        public async Task<Guid> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
        {


            if (!Enum.TryParse<TimeRange>(request.TimeRange, true, out var timeRange))
            {
                throw new ValidationException("Invalid Time range value");
            }


            var checkServiceType = await repo.ServiceTypes.GetByIdAsync(request.ServiceTypeId, cancellationToken);
            if (checkServiceType == null)
            {
                logger.LogWarning("Worker creation failed. ServiceTypeId not found: {ServiceTypeId}", request.ServiceTypeId);
                throw new ValidationException("Service Type not found");
            }


            var paymentStatus = PaymentStatus.Pending;

            var bookingStatus = BookingStatus.Pending;

            var location = Location.Create(request.Latitude, request.Longitude);

            var worker = await repo.Workers.GetWorkerById(request.WorkerId, cancellationToken);
            if (worker == null)
                throw new ValidationException("Worker with Id not found");

            var startTime = request.DateOfService.Date.Add(request.StartTime.TimeOfDay);
            var endTime = request.DateOfService.Date.Add(request.EndTime.TimeOfDay);
            var durationHours = (endTime - startTime).TotalHours;
            if (durationHours <= 0)
                throw new ValidationException("End time must be after start time.");

            var hourlyRate = worker.HourlyRate;
            var totalAmount = Math.Round(hourlyRate * (decimal)durationHours, 2);

            var originalAmount = totalAmount;
            var amount = totalAmount;
            Guid? couponId = null;

            if (!string.IsNullOrWhiteSpace(request.CouponCode))
            {
                var coupon = await repo.Coupons.GetByCodeAsync(request.CouponCode, cancellationToken);
                if (coupon == null)
                    throw new ValidationException($"Coupon code {request.CouponCode} does not exist.");

                if (!coupon.IsValid())
                    throw new ValidationException($"Coupon code {request.CouponCode} is expired or its usage limit has been reached.");

                amount = coupon.ApplyDiscount(originalAmount);
                coupon.IncrementUsage(request.CreatedBy);
                couponId = coupon.Id;
            }
            else
            {
                // New Year Logic: Automatic 10% discount if it's the first week of Jan
                var now = DateTime.UtcNow;
                if (now.Month == 1 && now.Day <= 7)
                {
                    amount = originalAmount * 0.9m;
                    logger.LogInformation("Applied automatic 10% New Year discount for booking.");
                }
            }

            var discoverAddress = await geocodingService.GetAddressAsync(request.Latitude, request.Longitude);

            if (!discoverAddress.Contains("Nigeria"))
                throw new Exception("Location must be in Nigeria.");

            var address = Address.Create(discoverAddress);


            await bookingRuleService
                .ValidateBookingAsync(request.WorkerId,
                request.ClientId,
                request.ServiceTypeId,
                totalAmount,
                amount,
                hourlyRate,
                durationHours,
                request.DateOfService,
                timeRange,
                cancellationToken);





            var newBooking = Booking.Create(
                request.ClientId,
                request.WorkerId,
                location,
                request.DateOfService,
                DateTime.UtcNow,
                startTime,
                endTime,
                hourlyRate,
                totalAmount,
                amount,
                originalAmount,
                timeRange,
                address,
                bookingStatus,
                paymentStatus,
                request.ServiceTypeId,
                couponId,
                request.CreatedBy
                );

            await repo.Bookings.CreateBooking(newBooking, cancellationToken);

            await repo.SaveChangesAsync(cancellationToken);

            await notificationService.NewBookingRequestAsync(newBooking, cancellationToken);

            return newBooking.Id;
        }
    }
}