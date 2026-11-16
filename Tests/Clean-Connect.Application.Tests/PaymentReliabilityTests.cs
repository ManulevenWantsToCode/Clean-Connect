using Clean_Connect.Application.Command.PaymentCommand;
using Clean_Connect.Application.Command.WebhookCommand;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Command.Services;
using Clean_Connect.Application.Configuration;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Domain.Value_Objects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Clean_Connect.Application.Tests;

public sealed class PaymentReliabilityTests
{
    [Fact]
    public void Payment_request_hash_is_canonical_and_conflicts_on_different_payload()
    {
        var bookingId = Guid.NewGuid();
        var hash = PaymentIdempotency.ComputeRequestHash(bookingId, 1234.50m, "CLIENT@example.com", "Paystack", "ngn");
        Assert.Equal(hash, PaymentIdempotency.ComputeRequestHash(bookingId, 1234.50m, "client@example.com", "paystack", "NGN"));

        var payment = Payment.Create(bookingId, 1234.50m, "cc-test-reference", "Paystack", "", DateTime.UtcNow,
            idempotencyKey: "client-request-1", requestHash: hash);
        PaymentIdempotency.EnsureSameRequest(payment, hash);
        Assert.Throws<PaymentInitializationConflictException>(() => PaymentIdempotency.EnsureSameRequest(payment, new string('a', 64)));
    }

    [Fact]
    public void Successful_payment_cannot_be_reversed_by_a_late_failure()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "cc-test-reference", "Paystack", "", DateTime.UtcNow);
        payment.MarkAsPaid("", "12345");

        Assert.Throws<InvalidOperationException>(() => payment.MarkAsFailed("late failure", "12345"));
        Assert.Equal(PaymentStatus.Successful, payment.Status);
    }

    [Fact]
    public void Provider_verification_must_match_reference_amount_and_currency()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "cc-test-reference", "Paystack", "", DateTime.UtcNow,
            currency: "NGN");
        var verified = new VerifyData { Reference = "cc-test-reference", Amount = 10_000, Currency = "NGN" };

        PaymentVerification.EnsureMatches(payment, verified);
        Assert.Throws<InvalidOperationException>(() => PaymentVerification.EnsureMatches(payment, verified with { Reference = "other" }));
        Assert.Throws<InvalidOperationException>(() => PaymentVerification.EnsureMatches(payment, verified with { Amount = 9_999 }));
        Assert.Throws<InvalidOperationException>(() => PaymentVerification.EnsureMatches(payment, verified with { Currency = "USD" }));
    }

    [Fact]
    public void Refund_amount_is_bounded_and_payout_retries_reuse_unknown_reference()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "cc-refund-reference", "Paystack", "123", DateTime.UtcNow);
        payment.MarkAsPaid("", "123");
        Assert.Throws<InvalidOperationException>(() => payment.BeginRefund("refund-12345678", 100.01m));
        payment.BeginRefund("refund-12345678", 75m);
        Assert.Throws<InvalidOperationException>(() => payment.BeginRefund("refund-different", 75m));

        var escrow = Escrow.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, 10m, 10m);
        var firstReference = escrow.BeginPayoutAttempt();
        escrow.MarkPayoutUnknown();
        var retryReference = escrow.BeginPayoutAttempt();
        Assert.Equal(firstReference, retryReference);
        Assert.Equal(1, escrow.PayoutAttemptCount);
    }

    [Fact]
    public async Task Invalid_webhook_signature_is_rejected_before_database_access()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Paystack:WebhookSecret"] = "test-webhook-secret" })
            .Build();
        var handler = new ProcessPaystackWebhookCommandHandler(
            NullLogger<ProcessPaystackWebhookCommandHandler>.Instance,
            null!, null!, null!, null!, null!, null!, configuration, Options.Create(new PaystackWebhookRecoveryOptions()));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(
            new ProcessPaystackWebhookCommand(System.Text.Encoding.UTF8.GetBytes("{}"), "not-a-signature"),
            CancellationToken.None));
    }

    [Fact]
    public async Task Refund_retries_reuse_the_persisted_provider_reference()
    {
        var handler = new ConflictResponseHandler();
        var service = new PaystackService(
            new HttpClient(handler),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Paystack:SecretKey"] = "test-secret",
                ["Paystack:BaseUrl"] = "https://paystack.invalid"
            }).Build(),
            NullLogger<PaystackService>.Instance);

        var acceptedExistingAttempt = await service.RefundTransactionAsync("cc-payment-reference", 100m,
            CancellationToken.None, "refund-12345678", "NGN");

        Assert.True(acceptedExistingAttempt);
        using var request = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("refund-12345678", request.RootElement.GetProperty("reference").GetString());
        Assert.Equal("cc-payment-reference", request.RootElement.GetProperty("transaction").GetString());
        Assert.Equal("/refund?reference=refund-12345678", handler.LookupPath);
    }

    private sealed class ConflictResponseHandler : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }
        public string? LookupPath { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.Conflict);
            }
            LookupPath = request.RequestUri!.PathAndQuery;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    status = true,
                    data = new[] { new { reference = "refund-12345678", amount = 10_000, currency = "NGN", status = "pending" } }
                })
            };
        }
    }
}
