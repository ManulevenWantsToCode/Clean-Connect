using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.Query.ServiceTypeQuery
{
    public record GetServiceByIdQuery(Guid id) : IRequest<ServiceTypeDto>;
   
    public class GetServiceByIdQueryHandler : IRequestHandler<GetServiceByIdQuery, ServiceTypeDto>
    {
        private readonly IUnitOfWork repo;
        private readonly ILogger<GetServiceByIdQueryHandler> logger;
        public GetServiceByIdQueryHandler(IUnitOfWork repository, ILogger<GetServiceByIdQueryHandler> _logger)
        {
            repo = repository;
            logger = _logger;
        }
        public async Task<ServiceTypeDto> Handle(GetServiceByIdQuery request, CancellationToken cancellationToken)
        {
            if(request.id == Guid.Empty)
            {
                throw new ArgumentException("Invalid service type ID.");
            }   

            var serviceType = await repo.ServiceTypes.GetByIdAsync(request.id, cancellationToken);
            if (serviceType == null)
            {
                logger.LogWarning("Service type with ID {ServiceTypeId} not found.", request.id);
                throw new KeyNotFoundException($"Service type with ID {request.id} not found.");
            }

           var serviceTypeResult = new ServiceTypeDto
           {
               Name = serviceType.Name,
               Description = serviceType.Description,
               ModifiedBy = serviceType.CreatedBy
           };

            logger.LogInformation("Service type with ID {ServiceTypeId} retrieved successfully.", request.id);
            return serviceTypeResult;

        }
    }

}
