using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.Query.WorkersQuery
{
    public record GetWorkerByIdQuery(Guid Id) : IRequest<WorkerDto>;

    public class GetWorkerByIdValidator : AbstractValidator<GetWorkerByIdQuery>
    {
        public GetWorkerByIdValidator()
        {
            RuleFor(x => x.Id)
                .NotEmpty()
                .WithMessage("Worker Id is required")
                .Must(id => id != Guid.Empty)
                .WithMessage("Worker ID cannot be empty");
        }
    }

    public class GetWorkerByIdQueryHandler : IRequestHandler<GetWorkerByIdQuery, WorkerDto>
    {

        private readonly IUnitOfWork repo;
        private readonly ILogger<GetWorkerByIdQueryHandler> logger;

        public GetWorkerByIdQueryHandler(IUnitOfWork _repo, ILogger<GetWorkerByIdQueryHandler> _logger)
        {
            repo = _repo;
            logger = _logger;
        }
        public async Task<WorkerDto> Handle(GetWorkerByIdQuery request, CancellationToken cancellationToken)
        {
           

            var check = await repo.Workers.GetWorkerById(request.Id, cancellationToken);

            if (check == null)
            {
                logger.LogWarning("GetWorkerByIdQueryHandler: Worker with Id {WorkerId} not found.", request.Id);
                throw new ArgumentException($"Worker with Id {request.Id} not found.");
            }

            var worker = new WorkerDto
            {
                Name = check.FullName,
                Address = check.Address,
                Contact = check.Contact,
                DateOfBirth = check.DateOfBirth,
                ServiceType = check.ServiceType.Name,
                //Location = check.Location.ToString(),
                Age = check.Age,
                Rating = check.AverageRating,
                State = check.State,
                Email = check.Email,
                Gender = check.Gender.ToString()
            };

            logger.LogInformation("GetWorkerByIdQueryHandler: Successfully retrieved worker with Id {WorkerId}.", request.Id);

            return worker;
        }
    }
}