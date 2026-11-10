using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using MediatR;

namespace Clean_Connect.Application.Query.WorkersQuery;

public record GetWorkerMarketplaceProfileQuery(Guid WorkerId) : IRequest<WorkerMarketplaceDto?>;

public sealed class GetWorkerMarketplaceProfileHandler(IUnitOfWork repo)
    : IRequestHandler<GetWorkerMarketplaceProfileQuery, WorkerMarketplaceDto?>
{
    public async Task<WorkerMarketplaceDto?> Handle(GetWorkerMarketplaceProfileQuery request, CancellationToken cancellationToken)
    {
        var worker = await repo.Workers.GetWorkerById(request.WorkerId, cancellationToken);
        return worker is null ? null : new WorkerMarketplaceDto
        {
            WorkerId = worker.Id,
            Name = worker.FullName,
            ServiceType = worker.ServiceType.Name,
            State = worker.State,
            Rating = worker.AverageRating,
            HourlyRate = worker.HourlyRate
        };
    }
}
