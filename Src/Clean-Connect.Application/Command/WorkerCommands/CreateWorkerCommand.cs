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

namespace Clean_Connect.Application.Command.WorkerCommands
{
    public record CreateWorkerCommand(string FirstName, string LastName, double? Latitude, double? Longitude, string Email, string Contact, string Gender, Guid ServiceTypeId, string State, DateTime Dob, decimal HourlyRate, string? CreatedBy = null) : IRequest<bool>;

    public class RegisterWorkerValidator : AbstractValidator<CreateWorkerCommand>
    {
        public RegisterWorkerValidator()
        {


            RuleFor(x => x.FirstName)
                .NotEmpty()
                .WithMessage("First name is required")
                .Length(2, 50)
                .WithMessage("First name must be between 2-50 characters");

            RuleFor(x => x.LastName)
                .NotEmpty()
                .WithMessage("Last name is required")
                .Length(2, 50)
                .WithMessage("Last name must be between 2-50 characters");

            RuleFor(x => x.Latitude)
                .Must(lat => lat == null || (lat >= 4.0 && lat <= 14.0))
                .WithMessage("Latitude must be between 4.0 and 14.0.");

            RuleFor(x => x.Longitude)
                .Must(lng => lng == null || (lng >= 2.5 && lng <= 15.5))
                .WithMessage("Longitude must be between 2.5 and 15.5.");


            RuleFor(x => x.Gender)
                .NotEmpty()
                .Must(g => Enum.TryParse<Gender>(g, true, out _))
                .WithMessage("Gender must be Male or Female");

            RuleFor(x => x.ServiceTypeId)
                .NotEmpty()
                .WithMessage("Service Type is required")
                .Must(id => id != Guid.Empty)
                .WithMessage("Invalid Id");

            RuleFor(x => x.Email)
                .NotEmpty()
                .WithMessage("Email is required")
                .EmailAddress()
                .WithMessage("Email is invalid")
                .Length(10, 100)
                .WithMessage("Email must be between 10-100 characters");

            RuleFor(x => x.Contact)
                .NotEmpty()
                .WithMessage("Contact is required")
                .Length(11, 15)
                .WithMessage("Contact must be between 11-15 characters");

            RuleFor(x => x.State)
                .NotEmpty()
                .WithMessage("State is required")
                .Length(3, 15)
                .WithMessage("State name must be 3-15 characters");

            RuleFor(x => x.Dob)
                .LessThan(DateTime.UtcNow)
                .WithMessage("Date of birth cannot be in the future");

            RuleFor(x => x.HourlyRate)
                .GreaterThan(0)
                .WithMessage("Hourly rate must be greater than zero")
                .LessThanOrEqualTo(1_000_000)
                .WithMessage("Hourly rate cannot exceed 1,000,000")
                .PrecisionScale(18, 2, true)
                .WithMessage("Invalid hourly rate");

        }
    }
    public class CreateWorkerHandler(IUnitOfWork repo, GeocodingService geocodingService, ICurrentUser currentUser, UserManager<ApplicationUser> userManager, ILogger<CreateWorkerHandler> logger) : IRequestHandler<CreateWorkerCommand, bool>
    {
        public async Task<bool> Handle(CreateWorkerCommand request, CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<Gender>(request.Gender, true, out var gender))
            {
                throw new ValidationException("Invalid gender value");
            }

            var checkServiceType = await repo.ServiceTypes.GetByIdAsync(request.ServiceTypeId, cancellationToken);
            if (checkServiceType == null)
            {
                logger.LogWarning("Worker creation failed. ServiceTypeId not found: {ServiceTypeId}", request.ServiceTypeId);
                throw new ValidationException("Service Type not found");
            }

            if (string.IsNullOrWhiteSpace(currentUser.UserId))
            {
                logger.LogWarning("Worker creation failed because no authenticated user was found.");
                throw new UnauthorizedAccessException("You must be logged in to create a worker profile.");
            }

            var appUser = await userManager.FindByIdAsync(currentUser.UserId);

            if (appUser == null)
            {
                logger.LogError("ApplicationUser not found for Id: {UserId}", currentUser.UserId);
                throw new UnauthorizedAccessException("Unable to find the logged-in user.");
            }

            if (!string.Equals(appUser.Email, request.Email, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "Worker creation failed. Profile email {ProfileEmail} does not match logged-in user {UserEmail}",
                    request.Email,
                    appUser.Email);

                throw new ValidationException("Worker profile email must match your login email.");
            }

            var checkExistingEmail = await repo.Workers.GetByEmail(request.Email, cancellationToken);

            if (checkExistingEmail != null)
            {
                logger.LogWarning("Worker creation failed. Email already in use: {Email}", request.Email);
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

            var worker = Worker.Create(
                fullname,
                address,
                contact,
                location,
                gender,
                request.ServiceTypeId,
                email,
                request.State,
                request.Dob,
                request.HourlyRate,
                request.CreatedBy);

            logger.LogInformation("Creating worker profile for {Email}", request.Email);

            await repo.Workers.CreateWorker(worker, cancellationToken);

            var saveResult = await repo.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Worker SaveChangesAsync returned: {SaveResult}", saveResult);

            var result = saveResult > 0;

            if (!result)
            {
                logger.LogWarning("Worker profile was not saved.");
                return false;
            }

            logger.LogInformation("Current Logged-in UserId: {UserId}", currentUser.UserId);

            logger.LogInformation(
                "User found. Email={Email}, IsWorkerProfileCompleted BEFORE={Status}",
                appUser.Email,
                appUser.IsWorkerProfileCompleted);

            appUser.CompleteWorkerProfile();

            logger.LogInformation(
                "After CompleteWorkerProfile(), IsWorkerProfileCompleted={Status}",
                appUser.IsWorkerProfileCompleted);

            var identityResult = await userManager.UpdateAsync(appUser);

            if (!identityResult.Succeeded)
            {
                var errorMessages = string.Join("; ", identityResult.Errors.Select(e => $"{e.Code}: {e.Description}"));

                logger.LogError("Identity Update Error: {Errors}", errorMessages);

                throw new Exception($"Failed to update ApplicationUser: {errorMessages}");
            }

            logger.LogInformation("ApplicationUser updated successfully.");

            // Read it again from the database
            var updatedUser = await userManager.FindByIdAsync(currentUser.UserId!);

            logger.LogInformation(
                "Database Value After Update: IsWorkerProfileCompleted={Status}",
                updatedUser?.IsWorkerProfileCompleted);

            logger.LogInformation(
                "CreateWorker succeeded. WorkerId={WorkerId}, Email={Email}",
                worker.Id,
                request.Email);

            return true;
        }
    }

}
