using Azure.Core;
using Clean_Connect.Application.Command.Services;
using Clean_Connect.Application.Command.WorkerCommands;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.Command.ClientCommands
{
    public record MarkAsCompletedCommand(Guid BookingId, Guid ClientId) : IRequest<bool>;

    public class  MarkAsCompletedCommandValidator : AbstractValidator<MarkAsCompletedCommand>
    {
        public MarkAsCompletedCommandValidator()
        {
            RuleFor(x => x.BookingId)
            .NotEmpty()
            .WithMessage("BookingId is required.");

            RuleFor(x => x.ClientId)
            .NotEmpty()
            .Must(id => id != Guid.Empty)
            .WithMessage("ClientId is required."); 
        }
        
    }

    public class MarkAsCompletedHandler : IRequestHandler<MarkAsCompletedCommand, bool>
    {
        private readonly IUnitOfWork repo;
        private readonly MarkAsCompletedService service;
        private readonly EscrowService escrowService;
        private readonly INotificationService notificationService;
        private readonly ILogger<MarkAsCompletedHandler> logger;

        public MarkAsCompletedHandler(IUnitOfWork _repo, MarkAsCompletedService _service, EscrowService escrowService, INotificationService notificationService, ILogger<MarkAsCompletedHandler> _logger)
        {
            repo = _repo;
            service = _service;
            this.escrowService = escrowService;
            this.notificationService = notificationService;
            logger = _logger;
        }

        public async Task <bool> Handle(MarkAsCompletedCommand request, CancellationToken cancellationToken)
        {
            var booking = await repo.Bookings.GetBookingById(request.BookingId, cancellationToken)
               ?? throw new KeyNotFoundException($"Booking with ID {request.BookingId} not found.");

            await service.MarkAsCompletedServiceAsync(request.BookingId, request.ClientId, cancellationToken);

            booking.MarkAsCompleted();
            await escrowService.ReleaseEscrowToWorkerWalletAsync(booking, request.ClientId.ToString(), cancellationToken);

            // Referral Reward Logic
            var client = await repo.Clients.GetClientById(request.ClientId, cancellationToken);
            if (client != null && client.ReferredById.HasValue)
            {
                var hasPriorCompletedBooking = client.Bookings.Any(b => b.Id != booking.Id && b.BookingStatus == BookingStatus.Completed);
                if (!hasPriorCompletedBooking)
                {
                    var referrer = await repo.Clients.GetClientById(client.ReferredById.Value, cancellationToken);
                    if (referrer != null)
                    {
                        referrer.IncrementSuccessfulReferral();
                        logger.LogInformation("Referrer {ReferrerId} successful referral count: {Count}", referrer.Id, referrer.SuccessfulReferralCount);

                        if (referrer.SuccessfulReferralCount % 10 == 0)
                        {
                            var couponCode = $"REF-30-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
                            var coupon = Coupon.Create(couponCode, 30, DateTime.UtcNow.AddMonths(1), 1, "System-Referral");
                            await repo.Coupons.CreateCouponAsync(coupon, cancellationToken);
                            logger.LogInformation("Generated 30% referral coupon for referrer {ReferrerId}: {CouponCode}", referrer.Id, couponCode);
                            await notificationService.ReferralCouponAwardedAsync(referrer, couponCode, cancellationToken);
                        }
                    }
                }
            }

            logger.LogInformation("Worker {WorkerId} Completed booking {BookingId}", booking.WorkerId, request.BookingId);

            await repo.SaveChangesAsync(cancellationToken);
            await notificationService.JobCompletedAsync(booking, cancellationToken);
            logger.LogInformation("Worker {WorkerId} Completed booking {BookingId}", booking.WorkerId, request.BookingId);
            return true;

            
        }
    }
  
}
