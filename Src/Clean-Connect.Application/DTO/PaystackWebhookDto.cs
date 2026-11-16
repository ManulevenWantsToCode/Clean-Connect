using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace Clean_Connect.Application.DTO
{
    public record PaystackWebhookDto
    {
        public string Event { get; set; } = string.Empty;

        public PaystackWebhookData? Data { get; set; }
    }

    public record PaystackWebhookData
    {
        public long? Id { get; set; }
        public string? Reference { get; set; }
        public decimal Amount { get; set; }
        public string? Currency { get; set; }
        public Authorization? Authorization { get; set; }
        public string? Status { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("transfer_code")]
        public string? TransferCode { get; set; }
        [JsonPropertyName("refund_reference")]
        public string? RefundReference { get; set; }
        [JsonPropertyName("transaction_reference")]
        public string? TransactionReference { get; set; }
        public PaystackCustomerDto? Customer { get; set; }
    }

    public record PaystackCustomerDto
    {
        public string? Email { get; set; }
    }
}
