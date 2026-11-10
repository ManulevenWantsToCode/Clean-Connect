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
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Clean_Connect.Application.Command.BookingCommand
{
    public record CreateBookingCommand(Guid ClientId, Guid WorkerId, double Latitude, double Longitude, double RadiusInMeters, Guid ServiceTypeId, DateTime DateOfService, string TimeRange, DateTime StartTime, DateTime EndTime, string? CouponCode = null, string? CreatedBy = null, string? IdempotencyKey = null) : IRequest<BookingCreateResult>;

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
                .WithMessage("Longitude must be between -180 and 180.");

            RuleFor(x => x.StartTime)
                .NotEmpty()
                .WithMessage("Start time is required");

            RuleFor(x => x.EndTime)
                .NotEmpty()
                .WithMessage("End time is required");

            RuleFor(x => x.EndTime)
                .GreaterThan(x => x.StartTime)
                .WithMessage("End time must be after start time.");

            RuleFor(x => x.IdempotencyKey)
                .NotEmpty().WithMessage("Idempotency-Key is required.")
                .MaximumLength(128)
                .Matches("^[!-~]+$").WithMessage("Idempotency-Key contains invalid characters.");


        }


    }

    public class CreateBookingHandler(IUnitOfWork repo, BookingRuleService bookingRuleService, GeocodingService geocodingService, INotificationService notificationService, ILogger<CreateBookingHandler> logger) : IRequestHandler<CreateBookingCommand, BookingCreateResult>
    {
        public async Task<BookingCreateResult> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
        {
            var idempotencyKey = request.IdempotencyKey!.Trim();
            var requestHash = ComputeRequestHash(request);
            var replay = await repo.Bookings.GetIdempotentBookingAsync(request.ClientId, idempotencyKey, requestHash, cancellationToken);
            if (replay is not null)
                return replay;

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

            var serviceDateUtc = AsUtc(request.DateOfService).Date;
            var startTime = serviceDateUtc.Add(AsUtc(request.StartTime).TimeOfDay);
            var endTime = serviceDateUtc.Add(AsUtc(request.EndTime).TimeOfDay);
            var durationHours = (endTime - startTime).TotalHours;
            if (durationHours <= 0 || durationHours > 24)
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

            string discoverAddress;
            try
            {
                discoverAddress = await geocodingService.GetAddressAsync(request.Latitude, request.Longitude);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Geocoding failed with exception type {ExceptionType}; using the default address.", ex.GetType().Name);
                discoverAddress = "Lagos, Nigeria";
            }

            if (!discoverAddress.Contains("Nigeria"))
            {
                logger.LogWarning("Geocoding returned non-Nigeria address: {Address}. Falling back to default.", discoverAddress);
                discoverAddress = "Lagos, Nigeria";
            }

            var address = Address.Create(discoverAddress);


            await bookingRuleService
                .ValidateBookingAsync(request.WorkerId,
                request.ClientId,
                request.ServiceTypeId,
                totalAmount,
                amount,
                hourlyRate,
                durationHours,
                serviceDateUtc,
                startTime,
                endTime,
                timeRange,
                cancellationToken);





            var newBooking = Booking.Create(
                request.ClientId,
                request.WorkerId,
                location,
                serviceDateUtc,
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
                request.CreatedBy,
                idempotencyKey,
                requestHash
                );

            var bookingChat = BookingChat.CreateForBooking(
                newBooking.Id,
                newBooking.ClientId,
                newBooking.WorkerId,
                request.CreatedBy);

            var saveResult = await repo.Bookings.CreateBookingIdempotentlyAsync(newBooking, bookingChat, cancellationToken);
            if (saveResult.Created)
            {
                await notificationService.NewBookingRequestAsync(newBooking, cancellationToken);
            }
            return saveResult;
        }

        private static DateTime AsUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        private static string ComputeRequestHash(CreateBookingCommand request)
        {
            var canonical = JsonSerializer.Serialize(new
            {
                request.WorkerId,
                request.Latitude,
                request.Longitude,
                request.RadiusInMeters,
                request.ServiceTypeId,
                ServiceDate = AsUtc(request.DateOfService).ToString("O"),
                request.TimeRange,
                Start = AsUtc(request.StartTime).ToString("O"),
                End = AsUtc(request.EndTime).ToString("O"),
                CouponCode = request.CouponCode?.Trim()
            });
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        }
    }
}
