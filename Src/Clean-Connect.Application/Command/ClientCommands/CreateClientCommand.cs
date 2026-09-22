using Clean_Connect.Application.Command.Services;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Domain.Value_Objects;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.ClientCommands
{
    public record CreateClientCommand(string FirstName, string LastName, double? Latitude, double? Longitude, string Email, string Contact, string Gender, string State, DateTime Dob, string? ReferralCode = null, string? CreatedBy = null) : IRequest<bool>;

    public class CreateClientValidator : AbstractValidator<CreateClientCommand>
    {
        public CreateClientValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("First name is required")
                .Length(2, 50).WithMessage("First name must be between 2-50 characters");

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Last name is required")
                .Length(2, 50).WithMessage("Last name must be between 2-50 characters");

            RuleFor(x => x.Latitude)
                .Must(lat => lat == null || (lat >= 4.0 && lat <= 14.0))
                .WithMessage("Latitude must be between 4.0 and 14.0.");

            RuleFor(x => x.Longitude)
                .Must(lng => lng == null || (lng >= 2.5 && lng <= 15.5))
                .WithMessage("Longitude must be between 2.5 and 15.5.");

            RuleFor(x => x.Gender)
                .NotEmpty().WithMessage("Gender is required")
                .Must(g => Enum.TryParse<Gender>(g, true, out _))
                .WithMessage("Gender must be Male or Female");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Email is invalid")
                .Length(10, 100).WithMessage("Email must be between 10-100 characters");

            RuleFor(x => x.Contact)
                .NotEmpty().WithMessage("Contact is required")
                .Length(11, 15).WithMessage("Contact must be between 11-15 characters");

            RuleFor(x => x.State)
                .NotEmpty().WithMessage("State is required")
                .Length(3, 20).WithMessage("State name must be 3-20 characters");

            RuleFor(x => x.Dob)
                .LessThan(DateTime.UtcNow).WithMessage("Date of birth cannot be in the future");
        }
    }

    public class CreateClientHandler(IUnitOfWork repo, GeocodingService geocodingService, ICurrentUser currentUser, UserManager<ApplicationUser> userManager, ILogger<CreateClientHandler> logger) : IRequestHandler<CreateClientCommand, bool>
    {
        public async Task<bool> Handle(CreateClientCommand request, CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<Gender>(request.Gender, true, out var gender))
                throw new ValidationException("Invalid gender value");

            if (string.IsNullOrWhiteSpace(currentUser.UserId))
            {
                logger.LogWarning("Client creation failed because no authenticated user was found.");
                throw new UnauthorizedAccessException("You must be logged in to create a client profile.");
            }

            var appUser = await userManager.FindByIdAsync(currentUser.UserId);

            if (appUser == null)
            {
                logger.LogError("ApplicationUser not found for Id: {UserId}", currentUser.UserId);
                throw new UnauthorizedAccessException("Unable to find the logged-in user.");
            }

            if (!string.Equals(appUser.Email, request.Email, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Client creation failed. Profile email {ProfileEmail} does not match logged-in user {UserEmail}", request.Email, appUser.Email);
                throw new ValidationException("Client email must match your login email.");
            }

            var checkExistingEmail = await repo.Clients.GetByEmail(request.Email, cancellationToken);

            if (checkExistingEmail != null)
            {
                logger.LogWarning("Client creation failed. Email already in use: {Email}", request.Email);
                throw new ValidationException("Email already in use");
            }

            var fullname = FullName.Create(request.FirstName, request.LastName);
            var email = Email.Create(request.Email);

            var (latitude, longitude) = request.Latitude.HasValue && request.Longitude.HasValue
                ? (request.Latitude.Value, request.Longitude.Value)
                : (8.5, 6.5);

            var location = Location.Create(latitude, longitude);

            string discoverAddress;
            if (request.Latitude.HasValue && request.Longitude.HasValue)
            {
                try
                {
                    discoverAddress = await geocodingService.GetAddressAsync(request.Latitude.Value, request.Longitude.Value);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Geocoding failed for lat={Lat}, lng={Lng}, falling back to State-based address", request.Latitude, request.Longitude);
                    discoverAddress = $"{request.State}, Nigeria";
                }

                if (!discoverAddress.Contains("Nigeria"))
                    discoverAddress = $"{request.State}, Nigeria";
            }
            else
            {
                logger.LogInformation("Location not provided. Using State-based address.");
                discoverAddress = $"{request.State}, Nigeria";
            }

            var address = Address.Create(discoverAddress);
            var contact = PhoneNumber.Create(request.Contact);
            var referralCode = await GenerateUniqueReferralCodeAsync(repo, cancellationToken);

            var client = Client.Create(fullname, address, email, location, gender, contact, request.State, request.Dob, referralCode);

            if (!string.IsNullOrWhiteSpace(request.ReferralCode))
            {
                var referrer = await repo.Clients.GetByReferralCodeAsync(request.ReferralCode.Trim().ToUpperInvariant(), cancellationToken);
                if (referrer == null)
                    throw new ValidationException($"Referral code '{request.ReferralCode}' is invalid.");

                client.SetReferrer(referrer.Id);
                logger.LogInformation("Client {ClientEmail} was referred by {ReferrerId}", request.Email, referrer.Id);
            }

            logger.LogInformation("Creating client profile for {Email}", request.Email);

            await repo.Clients.CreateClient(client, cancellationToken);

            var saveResult = await repo.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Client SaveChangesAsync returned: {SaveResult}", saveResult);

            if (saveResult <= 0)
            {
                logger.LogWarning("Client profile was not saved.");
                return false;
            }

            appUser.CompleteClientProfile();

            var identityResult = await userManager.UpdateAsync(appUser);

            if (!identityResult.Succeeded)
            {
                var errorMessages = string.Join("; ", identityResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
                logger.LogError("Identity Update Error: {Errors}", errorMessages);
                throw new Exception($"Failed to update ApplicationUser: {errorMessages}");
            }

            logger.LogInformation("Client profile created successfully for {Email}", request.Email);
            return true;
        }

        private static async Task<string> GenerateUniqueReferralCodeAsync(IUnitOfWork repo, CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var code = Guid.NewGuid().ToString("N")[..8].ToUpper();
                if (await repo.Clients.GetByReferralCodeAsync(code, cancellationToken) == null)
                    return code;
            }

            return Guid.NewGuid().ToString("N")[..8].ToUpper();
        }
    }
}
