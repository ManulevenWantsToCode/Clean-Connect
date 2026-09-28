using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.ServiceTypeCommands
{
    public record CreateServiceTypeCommand(string Name, string Description, string? CreatedBy = null) : IRequest<OperationResult>;

    public class CreateServiceValidator : AbstractValidator<CreateServiceTypeCommand>
    {
        public CreateServiceValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Service name is required")
                .Length(10, 50)
                .WithMessage("Service name must be between 10 - 50 character");


            RuleFor(x => x.Description)
                .NotEmpty()
                .WithMessage("Description is required")
                .Length(10, 200)
                .WithMessage("Service name must be between 10 - 200 character");


        }

        public class CreateServiceHandler : IRequestHandler<CreateServiceTypeCommand, OperationResult>
        {
            private readonly ILogger<CreateServiceHandler> logger;
            private readonly IUnitOfWork repo;
            public CreateServiceHandler(ILogger<CreateServiceHandler> _logger, IUnitOfWork _repo)
            {
                logger = _logger;
                repo = _repo;
            }

            public async Task<OperationResult> Handle(CreateServiceTypeCommand request, CancellationToken cancellationToken)
            {
                try
                {
                    var checkexisting = await repo.ServiceTypes.CheckExistingByName(request.Name.ToLower(), cancellationToken);
                    if (checkexisting)
                    {
                        logger.LogWarning("Service type '{ServiceName}' already exists.", request.Name);
                        return new OperationResult 
                        { 
                            Success = false,
                            ErrorMessage = "Service type already exists."
                        };
                    }

                    var serviceType = ServiceType.Create(
                        request.Name,
                        request.Description,
                        request.CreatedBy
                    );

                    await repo.ServiceTypes.AddAsync(serviceType, cancellationToken);
                    await repo.SaveChangesAsync(cancellationToken);

                    logger.LogInformation("Service type '{ServiceName}' created successfully by {CreatedBy}.", request.Name, request.CreatedBy ?? "System");
                    return new OperationResult
                    {
                        Success = true
                    };
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error occurred while creating service type '{ServiceName}'.", request.Name);
                    return new OperationResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message
                    };
                }
            }
        }
    }
}
