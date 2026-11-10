using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.DTO
{
    public record PaystackWebhookDto
    {
        public string Event { get; set; }

        public PaystackWebhookData Data { get; set; }
    }

    public record PaystackWebhookData
    {
        public string Reference { get; set; }
        public decimal Amount { get; set; }
        public Authorization Authorization { get; set; }
        public string Status { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("transfer_code")]
        public string? TransferCode { get; set; }
        public PaystackCustomerDto Customer { get; set; }
    }

    public record PaystackCustomerDto
    {
        public string Email { get; set; }
    }
}
