using Clean_Connect.Application.Interface.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Clean_Connect.Application.Command.ServiceTypeCommands.CreateServiceValidator;

namespace Clean_Connect.Application.Command.ServiceTypeCommands
{
    public record UpdateServiceTypeCommands(Guid Id, string Name, string Description, string? CreatedBy = null) : IRequest<bool>;

    public class UpdateServiceTypeValidator : AbstractValidator<UpdateServiceTypeCommands>
    {
        public UpdateServiceTypeValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Service name is required")
                .Length(10, 50)
                .WithMessage("Service name must be between 10 - 50 character");

            RuleFor(x => x.Description)
                .NotEmpty()
                .WithMessage("Description is required")
                .Length(20, 50)
                .WithMessage("Service Description must be between 20 - 50 character");

        }

    }

    public class UpdateServiceTypeHandler : IRequestHandler<UpdateServiceTypeCommands, bool>
    {
        private readonly ILogger<UpdateServiceTypeHandler> logger;
        private readonly IUnitOfWork repo;
        public UpdateServiceTypeHandler(ILogger<UpdateServiceTypeHandler> _logger, IUnitOfWork _repo)
        {
         
      
            logger = _logger;
            repo = _repo;

        }
        public async Task<bool> Handle(UpdateServiceTypeCommands request, CancellationToken cancellationToken)
        {


            //var checkExisting = await repo.ServiceTypes.GetByIdAsync(request.id, cancellationToken);

            //if (checkExisting != null)
            //{

            //    logger.LogError("Service type with Id {Id} already exists", request.Name);
            //    throw new ValidationException("Service type with the same id already exists");

            //}

            var serviceTypeToUpdate = await repo.ServiceTypes.GetByIdAsync(request.Id, cancellationToken);

            if (serviceTypeToUpdate == null)
            {
                logger.LogError("Service type with id {Id} not found", request.Id);
                throw new KeyNotFoundException("Service type not found");
            }

            serviceTypeToUpdate.UpdateService(request.Name,
                request.Description,
                request.CreatedBy
                );

            await repo.ServiceTypes.UpdateAsync(serviceTypeToUpdate, cancellationToken);
            await repo.SaveChangesAsync(cancellationToken);

            return true;


        }
    }

}
