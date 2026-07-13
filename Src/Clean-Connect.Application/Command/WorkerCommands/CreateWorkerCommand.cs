using Clean_Connect.Application.Command.Services;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Domain.Value_Objects;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Reflection.Metadata.Ecma335;

namespace Clean_Connect.Application.Command.WorkerCommands
{
    public record CreateWorkerCommand(string FirstName, string LastName,double Latitude, double Longitude, string Email, string Contact, string Gender, Guid ServiceTypeId,  string State, DateTime Dob, string? CreatedBy = null) : IRequest<bool>;

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
               .InclusiveBetween(4.0, 14.0)
               .WithMessage("Latitude must be between 4.0 and 14.0.");

            RuleFor(x => x.Longitude)
                .InclusiveBetween(2.5, 15.5)
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

        }
    }
    public class CreateWorkerHandler(IUnitOfWork repo, GeocodingService geocodingService, ILogger<CreateWorkerHandler> logger) : IRequestHandler<CreateWorkerCommand, bool>
    {
        public async Task<bool> Handle(CreateWorkerCommand request, CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<Gender>(request.Gender, true, out var gender))
            {
                throw new ValidationException("Invalid gender value");
            }

            var checkServiceType = await repo.ServiceTypes.GetByIdAsync(request.ServiceTypeId, cancellationToken);
            if(checkServiceType == null)
            {
                logger.LogWarning("Worker creation failed. ServiceTypeId not found: {ServiceTypeId}", request.ServiceTypeId);
                throw  new ValidationException("Service Type not found");
            }


            var checkExistingEmail = await repo.Workers.GetByEmail(request.Email, cancellationToken);

            if (checkExistingEmail != null)
            {
                logger.LogWarning("Worker creation failed. Email already in use: {Email}", request.Email);
                throw new ValidationException("Email already in use");
            }




            var fullname = FullName.Create(request.FirstName, request.LastName);
            var email = Email.Create(request.Email);
            var location = Location.Create(request.Latitude, request.Longitude);
            var discoverAddress = await geocodingService.GetAddressAsync(request.Latitude, request.Longitude);
            if (!discoverAddress.Contains("Nigeria"))
                throw new Exception("Location must be in Nigeria.");
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
                request.CreatedBy
                );

            await repo.Workers.CreateWorker(worker, cancellationToken);
            await repo.SaveChangesAsync(cancellationToken);

            logger.LogInformation("CreateWorker succeeded. WorkerId={WorkerId}, Email={Email}", worker.Id, request.Email);
            return true;
        }
    }

}
