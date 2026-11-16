using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Clean_Connect.Application.DTO
{
    public record PaystackVerifyResponse
    {
        public bool Status { get; set; }

        public string? Message { get; set; }

        public VerifyData? Data { get; set; }
    }

    public record VerifyData
    {
        public string? Status { get; set; }
        public int Amount { get; set; }

        public string? Reference { get; set; }

        public string? Currency { get; set; }

        public long? Id { get; set; }

        [JsonPropertyName("gateway_response")]
        public string? GatewayResponse { get; set; }
        public Authorization? Authorization { get; set; }
    }

    public record Authorization
    {
        [JsonPropertyName("authorization_code")]
        public string? AuthorizationCode { get; set; }
        
    }
}
