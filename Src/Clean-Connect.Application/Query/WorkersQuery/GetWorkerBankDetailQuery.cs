using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using MediatR;

namespace Clean_Connect.Application.Query.WorkersQuery
{
    public record GetWorkerBankDetailQuery(Guid WorkerId) : IRequest<WorkerBankDetailDto>;

    public class GetWorkerBankDetailQueryHandler : IRequestHandler<GetWorkerBankDetailQuery, WorkerBankDetailDto>
    {
        private readonly IUnitOfWork _repo;

        public GetWorkerBankDetailQueryHandler(IUnitOfWork repo)
        {
            _repo = repo;
        }

        public async Task<WorkerBankDetailDto> Handle(GetWorkerBankDetailQuery request, CancellationToken cancellationToken)
        {
            var detail = await _repo.WorkerBankDetails.GetActiveByWorkerIdAsync(request.WorkerId, cancellationToken);
            if (detail == null)
            {
                return new WorkerBankDetailDto();
            }

            return new WorkerBankDetailDto
            {
                HasDetails = true,
                BankCode = detail.BankCode,
                BankName = detail.BankName,
                AccountNumber = detail.AccountNumber,
                AccountName = detail.AccountName,
                Currency = detail.Currency
            };
        }
    }
}