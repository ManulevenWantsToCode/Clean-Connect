using Azure.Core;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.Command.Services
{
    public class BookingRuleService
    {
        private readonly IUnitOfWork _repo;
        private readonly WorkerAvailabilityService _availabilityService;
        private readonly ILogger<BookingRuleService> _logger;
        private readonly IPlatformSettingsRepository _platformSettings;


        public BookingRuleService(
            IUnitOfWork repo,
            WorkerAvailabilityService availabilityService,
            ILogger<BookingRuleService> logger,
            IPlatformSettingsRepository platformSettings)
        {
            _repo = repo;
            _availabilityService = availabilityService;
            _logger = logger;
            _platformSettings = platformSettings;
        }


        public async Task ValidateBookingAsync(Guid workerId, Guid clientId, Guid serviceTypeId, decimal totalAmount, decimal amount, decimal hourlyRate, double durationHours, DateTime dateOfService, DateTime startTime, DateTime endTime, TimeRange timeRange, CancellationToken cancellationToken)
        {
            var settings = await _platformSettings.GetAsync(cancellationToken);
            if (settings.MaintenanceMode)
                throw new ValidationException("New bookings are temporarily unavailable while the platform is under maintenance.");
            var checkWorkerId = await _repo.Workers.GetWorkerById(workerId, cancellationToken);


            if (checkWorkerId == null)
            {
                _logger.LogWarning("Booking creation failed. Worker not found: {WorkerId}", workerId);
                throw new ValidationException("Worker with Id not found");
            }

            var checkClientId = await _repo.Clients.GetClientById(clientId, cancellationToken);


            if (checkClientId == null)
            {
                _logger.LogWarning("Booking creation failed. Client not found: {ClientId}", clientId);
                throw new ValidationException("Client with Id not found");
            }

            var checkServiceType = await _repo.ServiceTypes.GetByIdAsync(serviceTypeId, cancellationToken);
            if (checkServiceType == null)
            {
                _logger.LogWarning("Worker creation failed. ServiceTypeId not found: {ServiceTypeId}", serviceTypeId);
                throw new ValidationException("Service Type not found");
            }

            var validateServiceType = checkWorkerId.ServiceType.Id == serviceTypeId;

            if (!validateServiceType)
            {
                _logger.LogWarning("Booking creation failed. Service type does not match worker's service type: {ServiceTypeId}", serviceTypeId);
                throw new ValidationException("Service type does not match worker's service type");
            }

            if (!checkWorkerId.IsAvailable ||
                !_availabilityService.IsWorkerAvailable(checkWorkerId, dateOfService, startTime, endTime))
            {
                _logger.LogWarning("Booking creation failed. Worker is not available at the requested date: {DateOfService}", dateOfService);
                throw new ValidationException("Worker is not available at the requested date");

            }

            var activeStatuses = new[] { BookingStatus.Pending, BookingStatus.AcceptedAwaitingPayment, BookingStatus.MarkAsPaid,
                BookingStatus.AwaitingClientStartConfirmation, BookingStatus.InProgress, BookingStatus.AwaitingClientConfirmation };
            if (checkWorkerId.Bookings.Count(b => b.DateOfService.Date == dateOfService.Date && activeStatuses.Contains(b.BookingStatus))
                >= settings.MaxActiveJobsPerCleanerPerDay)
                throw new ValidationException("This worker has reached the maximum number of active jobs for that day.");

            if (hourlyRate <= 0 || totalAmount != Math.Round(hourlyRate * (decimal)durationHours, 2))
            {
                _logger.LogWarning("Booking creation failed. Total does not match hourly rate and duration: {TotalAmount}", totalAmount);
                throw new ValidationException("Total amount does not match the worker's hourly rate and duration");
            }

            if (amount <= 0 || amount > totalAmount)
            {
                _logger.LogWarning("Booking creation failed. Invalid amount after discount: {Amount}", amount);
                throw new ValidationException("Invalid booking amount");
            }

            if (dateOfService < DateTime.UtcNow.AddHours(settings.MinimumAdvanceNoticeHours))
            {
                _logger.LogWarning("Booking creation failed. Date of service is before the configured advance notice: {DateOfService}", dateOfService);
                throw new ValidationException($"Bookings must be made at least {settings.MinimumAdvanceNoticeHours} hours in advance.");
            }
        }
    }
}
