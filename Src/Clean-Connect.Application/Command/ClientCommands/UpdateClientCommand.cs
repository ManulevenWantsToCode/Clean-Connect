using Clean_Connect.Application.Command.Services;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Domain.Value_Objects;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.ClientCommands
{
    public record UpdateClientCommand(Guid ClientId, string FirstName, string LastName, string Contact, string Gender, string State, string Address, double Latitude, double Longitude, DateTime DateOfBirth, string? ModifiedBy = null) : IRequest<bool>;

    public class UpdateClientValidator : AbstractValidator<UpdateClientCommand>
    {
        public UpdateClientValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("First name is required")
                .Length(2, 50).WithMessage("First name must be between 2-50 characters");

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Last name is required")
                .Length(2, 50).WithMessage("Last name must be between 2-50 characters");

            RuleFor(x => x.Contact)
                .NotEmpty().WithMessage("Contact is required")
                .Length(11, 15).WithMessage("Contact must be between 11-15 characters");

            RuleFor(x => x.Gender)
                .NotEmpty().WithMessage("Gender is required")
                .Must(g => Enum.TryParse<Gender>(g, true, out _))
                .WithMessage("Gender must be Male or Female");

            RuleFor(x => x.State)
                .NotEmpty().WithMessage("State is required")
                .Length(3, 20).WithMessage("State name must be 3-20 characters");

            RuleFor(x => x.Address)
                .NotEmpty().WithMessage("Address is required")
                .Length(5, 200).WithMessage("Address must be between 5-200 characters");

            RuleFor(x => x.Latitude)
                .InclusiveBetween(4.0, 14.5)
                .WithMessage("Latitude must be between 4.0 and 14.5");

            RuleFor(x => x.Longitude)
                .InclusiveBetween(2.5, 15.5)
                .WithMessage("Longitude must be between 2.5 and 15.5");

            RuleFor(x => x.DateOfBirth)
                .LessThan(DateTime.UtcNow).WithMessage("Date of birth cannot be in the future");
        }
    }

    public class UpdateClientHandler(IUnitOfWork repo, GeocodingService geocodingService, ILogger<UpdateClientHandler> logger) : IRequestHandler<UpdateClientCommand, bool>
    {
        public async Task<bool> Handle(UpdateClientCommand request, CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<Gender>(request.Gender, true, out var gender))
                throw new ValidationException("Invalid gender value");

            var client = await repo.Clients.GetClientById(request.ClientId, cancellationToken);

            if (client == null)
            {
                logger.LogWarning("Client not found for Id: {ClientId}", request.ClientId);
                throw new KeyNotFoundException("Client not found.");
            }

            string newAddress;
            try
            {
                newAddress = await geocodingService.GetAddressAsync(request.Latitude, request.Longitude);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Geocoding failed for lat={Lat}, lng={Lng}, falling back to State-based address", request.Latitude, request.Longitude);
                newAddress = $"{request.State}, Nigeria";
            }

            if (!newAddress.Contains("Nigeria"))
                newAddress = $"{request.State}, Nigeria";

            client.UpdateName(request.FirstName, request.LastName, request.ModifiedBy);
            client.UpdateContact(request.Contact, request.ModifiedBy);
            client.UpdateLocation(request.Latitude, request.Longitude, request.ModifiedBy);
            client.UpdateAddress(newAddress, request.ModifiedBy);
            client.UpdateState(request.State, request.ModifiedBy);
            client.UpdateDateOfBirth(request.DateOfBirth, request.ModifiedBy);
            client.UpdateGender(gender, request.ModifiedBy);

            await repo.Clients.UpdateClient(client, cancellationToken);
            var saveResult = await repo.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Client updated successfully for Id: {ClientId}", request.ClientId);
            return saveResult > 0;
        }
    }
}
