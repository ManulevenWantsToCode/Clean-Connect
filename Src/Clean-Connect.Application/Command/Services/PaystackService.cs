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
            _paystackSecretKey = config["Paystack:SecretKey"] ?? throw new InvalidOperationException("Paystack is not configured.");
            if (string.IsNullOrWhiteSpace(_paystackSecretKey))
                throw new InvalidOperationException("Paystack is not configured.");
            _paystackBase = config["Paystack:BaseUrl"]?.TrimEnd('/') ?? "https://api.paystack.co";
            _logger = logger;
        }

        public async Task<PaystackInitResponse> InitializePayment(decimal amount, string email, string reference, string? callbackUrl = null)
        {
            
            _logger.LogInformation("Initializing Paystack payment.");
      
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
            
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _paystackSecretKey);

            var response = await _httpClient.PostAsJsonAsync($"{_paystackBase}/transaction/initialize", requestData);

            _logger.LogInformation("Received response from Paystack initialization: {StatusCode}", response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();

            var paystackResponse = JsonSerializer.Deserialize<PaystackInitializeApiResponse>(content, JsonOptions);

            if (response.IsSuccessStatusCode && paystackResponse?.Status == true && paystackResponse.Data != null)
            {
                _logger.LogInformation("Paystack payment initialization completed.");
                return new PaystackInitResponse
                {
                    Status = true,
                    AuthorizationUrl = paystackResponse.Data.AuthorizationUrl,
                    Reference = paystackResponse.Data.Reference
                };
            }
            _logger.LogError("Paystack payment initialization failed with status {StatusCode}.", (int)response.StatusCode);
            throw new HttpRequestException("Paystack payment initialization failed.");
        }

        public async Task<PaystackVerifyResponse> VerifyTransaction(string reference)
        {
            _logger.LogInformation("Verifying Paystack transaction.");
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _paystackSecretKey);

            var response = await _httpClient.GetAsync(
                $"{_paystackBase}/transaction/verify/{Uri.EscapeDataString(reference)}"
            );
            _logger.LogInformation("Paystack transaction verification returned status {StatusCode}.", (int)response.StatusCode);
            response.EnsureSuccessStatusCode();

            var content = await response.Content
                .ReadFromJsonAsync<PaystackVerifyResponse>();

            if (content == null)
            {
                _logger.LogError("Paystack transaction verification returned an empty response.");
                throw new InvalidOperationException("Unable to verify transaction.");
            }

            _logger.LogInformation("Paystack transaction verification completed.");
            return content;
        }

        public async Task<TransferRecipientResponse> CreateTransferRecipientAsync(WorkerBankAccountDto bankAccount, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Creating Paystack transfer recipient.");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _paystackSecretKey);

            var requestData = new
            {
                type = "nuban",
                name = bankAccount.AccountName,
                account_number = bankAccount.AccountNumber,
                bank_code = bankAccount.BankCode,
                currency = bankAccount.Currency
            };

            var response = await _httpClient.PostAsJsonAsync($"{_paystackBase}/transferrecipient", requestData, cancellationToken);

            _logger.LogInformation("Received response from Paystack transfer recipient creation: {StatusCode}", response.StatusCode);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Paystack transfer recipient creation failed with status {StatusCode}.", (int)response.StatusCode);
                throw new HttpRequestException("Paystack transfer recipient creation failed.");
            }

            var paystackResponse = JsonSerializer.Deserialize<PaystackTransferRecipientResponse>(content, JsonOptions);

            if (paystackResponse == null || !paystackResponse.Status || paystackResponse.Data == null)
            {
                _logger.LogError("Paystack returned an invalid transfer recipient response.");
                throw new InvalidOperationException("Paystack returned an invalid transfer recipient response.");
            }

            _logger.LogInformation("Paystack transfer recipient creation completed.");

            return paystackResponse.Data;
        }

        public async Task<PaystackResolveAccountResponse?> ResolveAccountAsync(string accountNumber, string bankCode, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Resolving a bank account through Paystack.");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _paystackSecretKey);

            var response = await _httpClient.GetAsync(
                $"{_paystackBase}/bank/resolve?account_number={Uri.EscapeDataString(accountNumber)}&bank_code={Uri.EscapeDataString(bankCode)}",
                cancellationToken);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Paystack account resolution returned status {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            var paystackResponse = JsonSerializer.Deserialize<PaystackResolveAccountResponse>(content, JsonOptions);

            _logger.LogInformation("Paystack account resolution completed with status {Success}.", paystackResponse?.Status == true);

            return paystackResponse;
        }

        public async Task<TransferInitiationResponse> InitiateTransferAsync(string recipientCode, decimal amount, string reason, string reference, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Initiating Paystack transfer.");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _paystackSecretKey);

            var amountKobo = ToMinorUnits(amount);
            var requestData = new
            {
                source = "balance",
                amount = amountKobo,
                recipient = recipientCode,
                reason = reason,
                reference
            };

            var response = await _httpClient.PostAsJsonAsync($"{_paystackBase}/transfer", requestData, cancellationToken);

            _logger.LogInformation("Received response from Paystack transfer initiation: {StatusCode}", response.StatusCode);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Paystack transfer initiation failed with status {StatusCode}.", (int)response.StatusCode);
                throw new HttpRequestException("Paystack transfer initiation failed.");
            }

            var paystackResponse = JsonSerializer.Deserialize<PaystackTransferResponse>(content, JsonOptions);

            if (paystackResponse == null || !paystackResponse.Status || paystackResponse.Data == null)
            {
                _logger.LogError("Paystack returned an invalid transfer initiation response.");
                throw new InvalidOperationException("Paystack returned an invalid transfer response.");
            }

            _logger.LogInformation("Paystack transfer initiation completed.");

            return paystackResponse.Data;
        }

        public async Task<TransferInitiationResponse?> VerifyTransferAsync(string reference, CancellationToken cancellationToken)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _paystackSecretKey);
            using var response = await _httpClient.GetAsync(
                $"{_paystackBase}/transfer/verify/{Uri.EscapeDataString(reference)}",
                cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound
                || content.Contains("transfer not found", StringComparison.OrdinalIgnoreCase))
                return null;
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Paystack transfer verification failed ({(int)response.StatusCode}).");

            var result = JsonSerializer.Deserialize<PaystackTransferResponse>(content, JsonOptions);
            if (result == null || !result.Status || result.Data == null)
                throw new InvalidOperationException("Paystack returned an invalid transfer verification response.");
            return result.Data;
        }

        public async Task<TransferInitiationResponse?> FetchTransferAsync(string transferCode, CancellationToken cancellationToken)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _paystackSecretKey);
            using var response = await _httpClient.GetAsync(
                $"{_paystackBase}/transfer/{Uri.EscapeDataString(transferCode)}",
                cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound
                || content.Contains("transfer not found", StringComparison.OrdinalIgnoreCase))
                return null;
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Paystack transfer lookup failed ({(int)response.StatusCode}).");

            var result = JsonSerializer.Deserialize<PaystackTransferResponse>(content, JsonOptions);
            if (result == null || !result.Status || result.Data == null)
                throw new InvalidOperationException("Paystack returned an invalid transfer lookup response.");
            return result.Data;
        }

        private static int ToMinorUnits(decimal amount)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Transfer amount must be positive.");

            var minorUnits = amount * 100m;
            if (minorUnits != decimal.Truncate(minorUnits))
                throw new ArgumentException("Transfer amount has more than two decimal places.", nameof(amount));
            return checked((int)minorUnits);
        }

        public async Task<bool> RefundTransactionAsync(string transactionReference, decimal amount, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Initiating Paystack refund.");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _paystackSecretKey);

            var amountKobo = (int)(amount * 100m);
            var requestData = new
            {
                transaction = transactionReference,
                amount = amountKobo
            };

            var response = await _httpClient.PostAsJsonAsync($"{_paystackBase}/refund", requestData, cancellationToken);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Paystack refund failed with status {StatusCode}.", (int)response.StatusCode);
                return false;
            }

            var paystackResponse = JsonSerializer.Deserialize<PaystackRefundResponse>(content, JsonOptions);

            if (paystackResponse == null || !paystackResponse.Status)
            {
                _logger.LogError("Paystack rejected the refund request.");
                return false;
            }

            _logger.LogInformation("Paystack refund request completed with status {Status}.", paystackResponse.Data?.Status);
            return true;
        }

        public async Task<List<PaystackBankResponse>> ListBanksAsync(string currency, CancellationToken cancellationToken)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _paystackSecretKey);

            _logger.LogInformation("Fetching the bank list from Paystack.");

            var response = await _httpClient.GetAsync($"{_paystackBase}/bank?currency={Uri.EscapeDataString(currency)}&per_page=100", cancellationToken);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Paystack bank-list request failed with status {StatusCode}.", (int)response.StatusCode);
                throw new HttpRequestException("Paystack bank-list request failed.");
            }

            var paystackResponse = JsonSerializer.Deserialize<PaystackListBanksResponse>(content, JsonOptions);

            if (paystackResponse == null || !paystackResponse.Status || paystackResponse.Data == null)
            {
                _logger.LogError("Paystack returned an invalid bank-list response.");
                throw new InvalidOperationException("Paystack returned an invalid bank-list response.");
            }

            _logger.LogInformation("Fetched {Count} banks from Paystack.", paystackResponse.Data.Count);
            return paystackResponse.Data;
        }
    }
}

