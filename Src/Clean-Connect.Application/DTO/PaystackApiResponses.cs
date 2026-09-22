using System.Text.Json.Serialization;

namespace Clean_Connect.Application.DTO
{
    public sealed record PaystackInitializeApiResponse(
        bool Status,
        string? Message,
        PaystackInitializeData? Data);

    public sealed record PaystackInitializeData(
        [property: JsonPropertyName("authorization_url")] string AuthorizationUrl,
        string Reference);

    public sealed record PaystackTransferRecipientResponse(
        bool Status,
        string? Message,
        TransferRecipientResponse? Data);

    public sealed record PaystackTransferResponse(
        bool Status,
        string? Message,
        TransferInitiationResponse? Data);

    public sealed record PaystackRefundResponse(
        bool Status,
        string? Message,
        PaystackRefundData? Data);

    public sealed record PaystackRefundData(
        [property: JsonPropertyName("status")] string? Status);

    public sealed record PaystackBankResponse(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("code")] string Code);

    public sealed record PaystackListBanksResponse(
        bool Status,
        string? Message,
        List<PaystackBankResponse>? Data);
}
