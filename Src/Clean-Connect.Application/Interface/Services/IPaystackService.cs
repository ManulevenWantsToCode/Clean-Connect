using Clean_Connect.Application.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.Interface.Services
{
    public interface IPaystackService
    {
        Task<PaystackInitResponse> InitializePayment(decimal amount, string email, string reference, string? callbackUrl = null);

        Task<PaystackVerifyResponse> VerifyTransaction(string reference);

        Task<TransferRecipientResponse> CreateTransferRecipientAsync(WorkerBankAccountDto bankAccount, CancellationToken cancellationToken);

        Task<PaystackResolveAccountResponse?> ResolveAccountAsync(string accountNumber, string bankCode, CancellationToken cancellationToken);

        Task<TransferInitiationResponse> InitiateTransferAsync(string recipientCode, decimal amount, string reason, CancellationToken cancellationToken);

        Task<bool> RefundTransactionAsync(string transactionReference, decimal amount, CancellationToken cancellationToken);

        Task<List<PaystackBankResponse>> ListBanksAsync(string currency, CancellationToken cancellationToken);
    }
}
