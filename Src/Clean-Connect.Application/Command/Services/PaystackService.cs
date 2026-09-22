using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Clean_Connect.Application.Command.Services
{
    public class PaystackService : IPaystackService
    {
        private readonly HttpClient _httpClient;
        private readonly string _paystackSecretKey;
        private readonly string _paystackBase;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly ILogger<PaystackService> _logger;

        public PaystackService(HttpClient httpClient, IConfiguration config, ILogger<PaystackService> logger)
        {
            _httpClient = httpClient;
            _paystackSecretKey = config["Paystack:SecretKey"] ?? throw new ArgumentNullException("Paystack secret key is not configured.");
            _paystackBase = config["Paystack:BaseUrl"]?.TrimEnd('/') ?? "https://api.paystack.co";
            _logger = logger;
        }

        public async Task<PaystackInitResponse> InitializePayment(decimal amount, string email, string reference, string? callbackUrl = null)
        {
            
            _logger.LogInformation("Initializing Paystack payment for email: {Email}, amount: {Amount}, reference: {Reference}", email, amount, reference);
      
            var requestData = new Dictionary<string, object>
            {
                ["email"] = email,
                ["amount"] = (int)(amount * 100), // Paystack expects amount in kobo
                ["reference"] = reference
            };

            if (!string.IsNullOrWhiteSpace(callbackUrl))
            {
                requestData["callback_url"] = callbackUrl;
            }
            
            _logger.LogDebug("Paystack initialization request data: {@RequestData}", requestData);

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _paystackSecretKey);

            var response = await _httpClient.PostAsJsonAsync($"{_paystackBase}/transaction/initialize", requestData);

            _logger.LogInformation("Received response from Paystack initialization: {StatusCode}", response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Paystack initialization response content: {Content}", content);

            var paystackResponse = JsonSerializer.Deserialize<PaystackInitializeApiResponse>(content, JsonOptions);

            _logger.LogInformation("Parsed Paystack initialization response: {@PaystackResponse}", paystackResponse);

            if (response.IsSuccessStatusCode && paystackResponse?.Status == true && paystackResponse.Data != null)
            {
                _logger.LogInformation("Paystack initialization successful for reference: {Reference}", reference);
                return new PaystackInitResponse
                {
                    Status = true,
                    AuthorizationUrl = paystackResponse.Data.AuthorizationUrl,
                    Reference = paystackResponse.Data.Reference
                };
            }
            _logger.LogError("Paystack initialization failed for reference: {Reference}. Response: {Content}", reference, content);

            throw new Exception($"Paystack initialization failed: {paystackResponse?.Message ?? content}");
        }

        public async Task<PaystackVerifyResponse> VerifyTransaction(string reference)
        {
            _logger.LogInformation("Verifying Paystack transaction for reference: {Reference}", reference);
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _paystackSecretKey);

            _logger.LogDebug("Sending request to Paystack to verify transaction with reference: {Reference}", reference);

            var response = await _httpClient.GetAsync(
                $"{_paystackBase}/transaction/verify/{reference}"
            );
            _logger.LogInformation("Received response from Paystack verification for reference: {Reference}. Status code: {StatusCode}", reference, response.StatusCode);
            response.EnsureSuccessStatusCode();

            var content = await response.Content
                .ReadFromJsonAsync<PaystackVerifyResponse>();

            _logger.LogInformation("Parsed Paystack verification response for reference: {Reference}: {@Content}", reference, content);

            if (content == null)
            {
                _logger.LogError("Failed to parse Paystack verification response for reference: {Reference}", reference);
                throw new Exception("Unable to verify transaction");
            }

            _logger.LogInformation("Paystack transaction verification successful for reference: {Reference}", reference);
            return content;
        }

        public async Task<TransferRecipientResponse> CreateTransferRecipientAsync(WorkerBankAccountDto bankAccount, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Creating transfer recipient for account: {AccountName}, bank code: {BankCode}", bankAccount.AccountName, bankAccount.BankCode);

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _paystackSecretKey);

            var requestData = new
            {
                type = "nuban",
                name = bankAccount.AccountName,
                account_number = bankAccount.AccountNumber,
                bank_code = bankAccount.BankCode,
                currency = bankAccount.Currency
            };

            _logger.LogDebug("Transfer recipient request data: {@RequestData}", requestData);

            var response = await _httpClient.PostAsJsonAsync($"{_paystackBase}/transferrecipient", requestData, cancellationToken);

            _logger.LogInformation("Received response from Paystack transfer recipient creation: {StatusCode}", response.StatusCode);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogDebug("Paystack transfer recipient response content: {Content}", content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Transfer recipient creation failed for account: {AccountName}. Response: {Content}", bankAccount.AccountName, content);
                throw new Exception($"Transfer recipient creation failed: {content}");
            }

            var paystackResponse = JsonSerializer.Deserialize<PaystackTransferRecipientResponse>(content, JsonOptions);

            _logger.LogInformation("Parsed Paystack transfer recipient response: {@PaystackResponse}", paystackResponse);

            if (paystackResponse == null || !paystackResponse.Status || paystackResponse.Data == null)
            {
                _logger.LogError("Invalid transfer recipient response for account: {AccountName}. Response: {Content}", bankAccount.AccountName, content);
                throw new Exception($"Invalid transfer recipient response: {paystackResponse?.Message ?? content}");
            }

            _logger.LogInformation("Transfer recipient created successfully. RecipientCode: {RecipientCode}", paystackResponse.Data.RecipientCode);

            return paystackResponse.Data;
        }

        public async Task<TransferInitiationResponse> InitiateTransferAsync(string recipientCode, decimal amount, string reason, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Initiating transfer to recipient: {RecipientCode}, amount: {Amount}, reason: {Reason}", recipientCode, amount, reason);

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _paystackSecretKey);

            var amountKobo = (int)(amount * 100m);
            var requestData = new
            {
                source = "balance",
                amount = amountKobo,
                recipient = recipientCode,
                reason = reason
            };

            _logger.LogDebug("Transfer initiation request data: {@RequestData}", requestData);

            var response = await _httpClient.PostAsJsonAsync($"{_paystackBase}/transfer", requestData, cancellationToken);

            _logger.LogInformation("Received response from Paystack transfer initiation: {StatusCode}", response.StatusCode);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogDebug("Paystack transfer initiation response content: {Content}", content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Transfer initiation failed for recipient: {RecipientCode}. Response: {Content}", recipientCode, content);
                throw new Exception($"Transfer initiation failed: {content}");
            }

            var paystackResponse = JsonSerializer.Deserialize<PaystackTransferResponse>(content, JsonOptions);

            _logger.LogInformation("Parsed Paystack transfer initiation response: {@PaystackResponse}", paystackResponse);

            if (paystackResponse == null || !paystackResponse.Status || paystackResponse.Data == null)
            {
                _logger.LogError("Invalid transfer initiation response for recipient: {RecipientCode}. Response: {Content}", recipientCode, content);
                throw new Exception($"Invalid transfer response: {paystackResponse?.Message ?? content}");
            }

            _logger.LogInformation("Transfer initiated successfully. TransferCode: {TransferCode}, Status: {Status}", paystackResponse.Data.TransferCode, paystackResponse.Data.Status);

            return paystackResponse.Data;
        }

        public async Task<bool> RefundTransactionAsync(string transactionReference, decimal amount, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Initiating Paystack refund for transaction: {Reference}, amount: {Amount}", transactionReference, amount);

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _paystackSecretKey);

            var amountKobo = (int)(amount * 100m);
            var requestData = new
            {
                transaction = transactionReference,
                amount = amountKobo
            };

            _logger.LogDebug("Paystack refund request data: {@RequestData}", requestData);

            var response = await _httpClient.PostAsJsonAsync($"{_paystackBase}/refund", requestData, cancellationToken);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogDebug("Paystack refund response content: {Content}", content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Paystack refund failed for transaction: {Reference}. Response: {Content}", transactionReference, content);
                return false;
            }

            var paystackResponse = JsonSerializer.Deserialize<PaystackRefundResponse>(content, JsonOptions);

            if (paystackResponse == null || !paystackResponse.Status)
            {
                _logger.LogError("Paystack refund rejected for transaction: {Reference}. Response: {Content}", transactionReference, content);
                return false;
            }

            _logger.LogInformation("Paystack refund initiated for transaction: {Reference}. Status: {Status}", transactionReference, paystackResponse.Data?.Status);
            return true;
        }

        public async Task<List<PaystackBankResponse>> ListBanksAsync(string currency, CancellationToken cancellationToken)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _paystackSecretKey);

            _logger.LogInformation("Fetching list of banks from Paystack for currency: {Currency}", currency);

            var response = await _httpClient.GetAsync($"{_paystackBase}/bank?currency={Uri.EscapeDataString(currency)}&per_page=100", cancellationToken);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch banks from Paystack. Response: {Content}", content);
                throw new Exception($"Failed to fetch banks from Paystack: {content}");
            }

            var paystackResponse = JsonSerializer.Deserialize<PaystackListBanksResponse>(content, JsonOptions);

            if (paystackResponse == null || !paystackResponse.Status || paystackResponse.Data == null)
            {
                _logger.LogError("Invalid bank list response from Paystack. Response: {Content}", content);
                throw new Exception($"Invalid bank list response from Paystack: {paystackResponse?.Message ?? content}");
            }

            _logger.LogInformation("Fetched {Count} banks from Paystack.", paystackResponse.Data.Count);
            return paystackResponse.Data;
        }
    }
}

