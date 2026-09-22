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


        public BookingRuleService(
            IUnitOfWork repo,
            WorkerAvailabilityService availabilityService,
            ILogger<BookingRuleService> logger)
        {
            _repo = repo;
            _availabilityService = availabilityService;
            _logger = logger;
        }


        public async Task ValidateBookingAsync(Guid workerId, Guid clientId, Guid serviceTypeId, decimal originalAmount, decimal amount, DateTime dateOfService, TimeRange timeRange, CancellationToken cancellationToken)
        {
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
                !_availabilityService.IsWorkerAvailable(checkWorkerId, dateOfService, timeRange))
            {
                _logger.LogWarning("Booking creation failed. Worker is not available at the requested date: {DateOfService}", dateOfService);
                throw new ValidationException("Worker is not available at the requested date");

            }

            if (originalAmount != checkServiceType.Amount)
            {
                _logger.LogWarning("Booking creation failed. Amount does not match service type price: {Amount}", originalAmount);
                throw new ValidationException("Amount does not match service type price");
            }

            if (amount <= 0 || amount > originalAmount)
            {
                _logger.LogWarning("Booking creation failed. Invalid amount after discount: {Amount}", amount);
                throw new ValidationException("Invalid booking amount");
            }

            if (dateOfService.Date < DateTime.UtcNow.Date)
            {
                _logger.LogWarning("Booking creation failed. Date of service cannot be in the past: {DateOfService}", dateOfService);
                throw new ValidationException("Date of service cannot be in the past");
            }
        }
    }
}
