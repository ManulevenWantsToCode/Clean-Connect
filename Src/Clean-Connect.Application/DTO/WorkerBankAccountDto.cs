using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Clean_Connect.Application.DTO
{

    public record WorkerBankAccountDto(
        string AccountNumber,
        string BankCode,
        string AccountName,
        string Currency = "NGN");

    public class TransferRecipientResponse
    {
        [JsonPropertyName("recipient_code")]
        public string RecipientCode { get; set; } = default!;

        [JsonPropertyName("domain")]
        public string? Domain { get; set; }

        [JsonPropertyName("id")]
        public int? Id { get; set; }
    }

    public class TransferInitiationResponse
    {
        [JsonPropertyName("transfer_code")]
        public string TransferCode { get; set; } = default!;

        [JsonPropertyName("status")]
        public string Status { get; set; } = default!;

        [JsonPropertyName("reference")]
        public string? Reference { get; set; }
    }

    public record PayoutResult(bool Success, string Message, string? ProviderReference = null);

    public class PaystackResolveAccountData
    {
        [JsonPropertyName("account_number")]
        public string? AccountNumber { get; set; }

        [JsonPropertyName("account_name")]
        public string? AccountName { get; set; }

        [JsonPropertyName("bank_id")]
        public int? BankId { get; set; }
    }

    public class PaystackResolveAccountResponse
    {
        public bool Status { get; set; }

        public string? Message { get; set; }

        public PaystackResolveAccountData? Data { get; set; }
    }
}
