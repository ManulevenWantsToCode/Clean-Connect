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
    public record GetAllServiceTypeQuery : IRequest<List<ServiceTypeDto>>;

    public class GetAllServiceTypeQueryHandler : IRequestHandler<GetAllServiceTypeQuery, List<ServiceTypeDto>>
    {
        private readonly ILogger<GetAllServiceTypeQueryHandler> logger;
        private readonly IUnitOfWork repo;

        public GetAllServiceTypeQueryHandler(ILogger<GetAllServiceTypeQueryHandler> _logger, IUnitOfWork _repo)
        {
            logger = _logger;
            repo = _repo;
        }
        public async Task<List<ServiceTypeDto>> Handle(GetAllServiceTypeQuery request, CancellationToken cancellationToken)
        {
            logger.LogInformation("GetAllServiceTypeHandler operation");
            var check = await repo.ServiceTypes.GetAllAsync(cancellationToken);

            if(check.Count() == 0 || check == null)
            {
                logger.LogWarning("No workers found");
                return new List<ServiceTypeDto>();
            }

            var servicesList = check.Select(s => new ServiceTypeDto
            {
                Name = s.Name,
                Description = s.Description,
                ModifiedBy = s.ModifiedBy,
            }).ToList();

            logger.LogInformation("Successfully retrieved {Count} services", servicesList.Count);
            
            return servicesList;
        }
    }

}
