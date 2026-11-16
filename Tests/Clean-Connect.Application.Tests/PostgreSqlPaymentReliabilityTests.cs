using Clean_Connect.Application.Command.WebhookCommand;
using Clean_Connect.Application.Command.PaymentCommand;
using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Value_Objects;
using Clean_Connect.Infrastructure.Context;
using Clean_Connect.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MediatR;

namespace Clean_Connect.Application.Tests;

public sealed class PostgreSqlPaymentReliabilityTests
{
    [Fact]
    public async Task PostgreSQL_enforces_payment_idempotency_webhook_deduplication_and_single_claim()
    {
        var connectionString = Environment.GetEnvironmentVariable("CLEAN_CONNECT_POSTGRES_TEST");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Set CLEAN_CONNECT_POSTGRES_TEST to a disposable, migrated PostgreSQL test database.");
            return;
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (builder.Host is not ("localhost" or "127.0.0.1" or "::1")
            || string.IsNullOrWhiteSpace(builder.Database)
            || !builder.Database.Contains("test", StringComparison.OrdinalIgnoreCase)
            || builder.Database.Contains("prod", StringComparison.OrdinalIgnoreCase)
            || builder.Database.Contains("live", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Payment integration tests require a loopback-only database explicitly named for testing.");

        var schema = $"cc_payment_test_{Guid.NewGuid():N}";
        await using var setup = new NpgsqlConnection(connectionString);
        await setup.OpenAsync(TestContext.Current.CancellationToken);
        await using (var createSchema = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\";", setup))
            await createSchema.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        try
        {
            await using (var createPayments = new NpgsqlCommand($"CREATE TABLE \"{schema}\".\"Payments\" (LIKE public.\"Payments\" INCLUDING ALL);", setup))
                await createPayments.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            await using (var createInbox = new NpgsqlCommand($"CREATE TABLE \"{schema}\".\"PaystackWebhookInboxEvents\" (LIKE public.\"PaystackWebhookInboxEvents\" INCLUDING ALL);", setup))
                await createInbox.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            await using (var createAudit = new NpgsqlCommand($"CREATE TABLE \"{schema}\".\"AuditRecords\" (LIKE public.\"AuditRecords\" INCLUDING ALL);", setup))
                await createAudit.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            await using (var createEscrows = new NpgsqlCommand($"CREATE TABLE \"{schema}\".\"Escrows\" (LIKE public.\"Escrows\" INCLUDING ALL);", setup))
                await createEscrows.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

            await VerifyMigrationAsync(setup);
            await VerifyConcurrentPaymentInitializationAsync(connectionString, schema);
            await VerifyRepositoryIdempotentReplayAsync(connectionString, schema);
            await VerifyConcurrentWebhookDeliveryAndClaimAsync(connectionString, schema);
            await VerifyWebhookBackoffQuarantineRequeueAndInterruptionAsync(connectionString, schema);
            await VerifyAmbiguousInitializationRecoveryAsync(connectionString, schema);
            await VerifyAmbiguousInitializationRecoveryAsync(connectionString, schema, transactionVisible: false);
            await VerifyConcurrentRefundAndPayoutReferenceRecoveryAsync(connectionString, schema);
        }
        finally
        {
            await using var dropSchema = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;", setup);
            await dropSchema.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }

    private static async Task VerifyMigrationAsync(NpgsqlConnection connection)
    {
        const string sql = @"
SELECT
 (SELECT count(*) FROM pg_indexes WHERE indexname = 'IX_Payments_BookingId_IdempotencyKey'),
 (SELECT count(*) FROM pg_indexes WHERE indexname = 'IX_Payments_RefundReference'),
 (SELECT count(*) FROM pg_indexes WHERE indexname = 'IX_PaystackWebhookInboxEvents_EventKey');";
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1L, reader.GetInt64(0));
        Assert.Equal(1L, reader.GetInt64(1));
        Assert.Equal(1L, reader.GetInt64(2));
    }

    private static async Task VerifyConcurrentPaymentInitializationAsync(string connectionString, string schema)
    {
        var bookingId = Guid.NewGuid();
        var hash = new string('a', 64);
        var outcomes = await RunTogetherAsync(connectionString,
            connection => TryInsertPaymentAsync(connection, schema, bookingId, "same-request-key", hash),
            connection => TryInsertPaymentAsync(connection, schema, bookingId, "same-request-key", hash));
        Assert.Single(outcomes, inserted => inserted);

        var changedPayloadHash = new string('b', 64);
        var stored = await ReadRequestHashAsync(connectionString, schema, bookingId, "same-request-key");
        Assert.Equal(hash, stored);
        Assert.NotEqual(changedPayloadHash, stored);

        Assert.True(await TryInsertPaymentAsync(connectionString, schema, Guid.NewGuid(), "same-request-key", changedPayloadHash));
        var secondActiveAttempt = await TryInsertPaymentAsync(connectionString, schema, bookingId, "different-key", changedPayloadHash);
        Assert.False(secondActiveAttempt);
    }

    private static async Task VerifyConcurrentWebhookDeliveryAndClaimAsync(string connectionString, string schema)
    {
        var eventKey = new string('c', 64);
        var results = await Task.WhenAll(
            RecordWebhookAsync(connectionString, schema, eventKey),
            RecordWebhookAsync(connectionString, schema, eventKey));
        Assert.Single(results, inserted => inserted);
        Assert.Single(results, inserted => !inserted);

        var scopedBuilder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(scopedBuilder.ConnectionString, provider => provider.UseNetTopologySuite())
            .Options;
        var id = await ReadInboxIdAsync(connectionString, schema, eventKey);
        await using var firstContext = new ApplicationDbContext(options);
        await using var secondContext = new ApplicationDbContext(options);
        var firstRepository = new PaystackWebhookInboxRepository(firstContext);
        var secondRepository = new PaystackWebhookInboxRepository(secondContext);
        var now = DateTime.UtcNow;
        var claims = await Task.WhenAll(
            firstRepository.TryClaimAsync(id, now, now.AddMinutes(-2), 8, TestContext.Current.CancellationToken),
            secondRepository.TryClaimAsync(id, now, now.AddMinutes(-2), 8, TestContext.Current.CancellationToken));
        Assert.Single(claims, claimed => claimed);

        await firstRepository.MarkFailedAsync(id, "ProviderUnavailable", now, now.AddSeconds(1), false, TestContext.Current.CancellationToken);
        var retryIds = await firstRepository.GetRetryableIdsAsync(now, now.AddMinutes(-2), 8, 10, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(id, retryIds);
        Assert.Contains(id, await firstRepository.GetRetryableIdsAsync(now.AddSeconds(1), now.AddMinutes(-2), 8, 10, TestContext.Current.CancellationToken));
        Assert.True(await firstRepository.TryClaimAsync(id, now.AddSeconds(2), now.AddMinutes(-2), 8, TestContext.Current.CancellationToken));
        await firstRepository.MarkProcessedAsync(id, DateTime.UtcNow, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(id, await firstRepository.GetRetryableIdsAsync(DateTime.UtcNow, DateTime.UtcNow.AddMinutes(-2), 8, 10, TestContext.Current.CancellationToken));

        await using (var makeStale = new NpgsqlConnection(scopedBuilder.ConnectionString))
        {
            await makeStale.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = new NpgsqlCommand($"UPDATE \"{schema}\".\"PaystackWebhookInboxEvents\" SET \"ProcessingStatus\"='Processing', \"ProcessingStartedAtUtc\"=@started WHERE \"Id\"=@id;", makeStale);
            command.Parameters.AddWithValue("started", DateTime.UtcNow.AddMinutes(-3));
            command.Parameters.AddWithValue("id", id);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
        Assert.True(await firstRepository.TryClaimAsync(id, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(-2), 8, TestContext.Current.CancellationToken));
    }

    private static async Task VerifyWebhookBackoffQuarantineRequeueAndInterruptionAsync(string connectionString, string schema)
    {
        var scopedBuilder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(scopedBuilder.ConnectionString, provider => provider.UseNetTopologySuite())
            .Options;
        var now = DateTime.UtcNow;
        var recovery = new Clean_Connect.Application.Configuration.PaystackWebhookRecoveryOptions
        {
            InitialDelaySeconds = 5,
            MaximumDelaySeconds = 20,
            MaximumAttempts = 3
        };
        var entity = PaystackWebhookInboxEvent.Receive(Guid.NewGuid().ToString("N"), "charge.success", "9876", "cc-backoff", "{}", now);
        Guid id;
        await using (var db = new ApplicationDbContext(options))
        {
            var repo = new PaystackWebhookInboxRepository(db);
            id = (await repo.RecordAsync(entity, TestContext.Current.CancellationToken)).Event.Id;
            Assert.True(await repo.TryClaimAsync(id, now, now.AddMinutes(-1), recovery.MaximumAttempts, TestContext.Current.CancellationToken));
            await repo.MarkFailedAsync(id, "ProviderUnavailable", now, now + recovery.RetryDelay(1), false, TestContext.Current.CancellationToken);
            Assert.DoesNotContain(id, await repo.GetRetryableIdsAsync(now + TimeSpan.FromSeconds(4), now.AddMinutes(-1), recovery.MaximumAttempts, 10, TestContext.Current.CancellationToken));
            Assert.Contains(id, await repo.GetRetryableIdsAsync(now + recovery.RetryDelay(1), now.AddMinutes(-1), recovery.MaximumAttempts, 10, TestContext.Current.CancellationToken));
            var secondTime = now + recovery.RetryDelay(1);
            Assert.True(await repo.TryClaimAsync(id, secondTime, now.AddMinutes(-1), recovery.MaximumAttempts, TestContext.Current.CancellationToken));
            await repo.MarkFailedAsync(id, "ProviderUnavailable", secondTime, secondTime + recovery.RetryDelay(2), false, TestContext.Current.CancellationToken);
            var thirdTime = secondTime + recovery.RetryDelay(2);
            Assert.True(await repo.TryClaimAsync(id, thirdTime, now.AddMinutes(-1), recovery.MaximumAttempts, TestContext.Current.CancellationToken));
            await repo.MarkFailedAsync(id, "ProviderUnavailable", thirdTime, thirdTime + recovery.RetryDelay(3), true, TestContext.Current.CancellationToken);
            Assert.DoesNotContain(id, await repo.GetRetryableIdsAsync(thirdTime.AddDays(1), now.AddMinutes(-1), recovery.MaximumAttempts, 10, TestContext.Current.CancellationToken));
            var quarantined = Assert.Single(await repo.GetQuarantinedAsync(10, TestContext.Current.CancellationToken));
            Assert.Equal("Quarantined", quarantined.ProcessingStatus);
            Assert.Equal(3, quarantined.Attempts);
            Assert.Equal("ProviderUnavailable", quarantined.LastErrorCategory);
            Assert.True(await repo.RequeueQuarantinedAsync(id, "admin-user-id", thirdTime.AddDays(1), TestContext.Current.CancellationToken));
            var requeued = await repo.GetByIdAsync(id, TestContext.Current.CancellationToken);
            Assert.Equal(0, requeued!.RetryCount);
            Assert.Equal(3, requeued.Attempts);
            Assert.Equal(1, requeued.RequeueCount);
            Assert.Equal("admin-user-id", requeued.LastRequeuedBy);
            var requeuedAt = thirdTime.AddDays(1);
            Assert.Contains(id, await repo.GetRetryableIdsAsync(requeuedAt, requeuedAt.AddMinutes(-1), recovery.MaximumAttempts, 10, TestContext.Current.CancellationToken));
            Assert.True(await repo.TryClaimAsync(id, requeuedAt, requeuedAt.AddMinutes(-1), recovery.MaximumAttempts, TestContext.Current.CancellationToken));
        }

        // Simulate a process stopping after claim: a stale lease must become eligible again.
        var interrupted = PaystackWebhookInboxEvent.Receive(Guid.NewGuid().ToString("N"), "charge.success", "9877", "cc-interrupted", "{}", now);
        Guid interruptedId;
        await using (var db = new ApplicationDbContext(options))
        {
            var repo = new PaystackWebhookInboxRepository(db);
            interruptedId = (await repo.RecordAsync(interrupted, TestContext.Current.CancellationToken)).Event.Id;
            Assert.True(await repo.TryClaimAsync(interruptedId, now, now.AddMinutes(-1), recovery.MaximumAttempts, TestContext.Current.CancellationToken));
        }
        var recoveredAt = now.AddMinutes(4);
        await using var workerOneDb = new ApplicationDbContext(options);
        await using var workerTwoDb = new ApplicationDbContext(options);
        var workerOne = new PaystackWebhookInboxRepository(workerOneDb);
        var workerTwo = new PaystackWebhookInboxRepository(workerTwoDb);
        var retryable = await workerOne.GetRetryableIdsAsync(recoveredAt, recoveredAt.AddMinutes(-3), recovery.MaximumAttempts, 10, TestContext.Current.CancellationToken);
        Assert.Contains(interruptedId, retryable);
        var workers = await Task.WhenAll(
            workerOne.TryClaimAsync(interruptedId, recoveredAt, recoveredAt.AddMinutes(-3), recovery.MaximumAttempts, TestContext.Current.CancellationToken),
            workerTwo.TryClaimAsync(interruptedId, recoveredAt, recoveredAt.AddMinutes(-3), recovery.MaximumAttempts, TestContext.Current.CancellationToken));
        Assert.Single(workers, claimed => claimed);
    }

    private static async Task VerifyAmbiguousInitializationRecoveryAsync(string connectionString, string schema, bool transactionVisible = true)
    {
        var scopedBuilder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(scopedBuilder.ConnectionString, provider => provider.UseNetTopologySuite())
            .Options;
        var booking = Booking.Create(Guid.NewGuid(), Guid.NewGuid(), Location.Create(6.5244, 3.3792), DateTime.UtcNow.AddDays(1), DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1).AddHours(9), DateTime.UtcNow.AddDays(1).AddHours(11), 1000m, 2000m, 2000m, 2000m,
            TimeRange.Morning, Address.Create("Test address"), BookingStatus.AcceptedAwaitingPayment, PaymentStatus.Pending, Guid.NewGuid());
        var providerState = new InitializationProviderState();
        providerState.TransactionVisible = transactionVisible;
        var key = $"ambiguous-{Guid.NewGuid():N}";

        async Task<PaymentInitializationConflictException> RunAttemptAsync()
        {
            await using var db = new ApplicationDbContext(options);
            var paymentRepo = new PaymentRepository(db);
            var bookingRepo = ApiOwnershipAuthorizationTests.InterfaceStub<IBookingRepository>.Create((method, _) =>
                method.Name == nameof(IBookingRepository.GetBookingById) ? Task.FromResult(booking) : null);
            var unitOfWork = ApiOwnershipAuthorizationTests.InterfaceStub<IUnitOfWork>.Create((method, _) => method.Name switch
            {
                "get_Payments" => paymentRepo,
                "get_Bookings" => bookingRepo,
                _ => null
            });
            var paystack = ApiOwnershipAuthorizationTests.InterfaceStub<IPaystackService>.Create((method, args) =>
            {
                if (method.Name == nameof(IPaystackService.InitializePayment))
                {
                    providerState.InitializeCalls++;
                    providerState.Reference = (string)args![2]!;
                    providerState.ProviderCreatedTransaction = true;
                    throw new TimeoutException("simulated timeout after provider accepted request");
                }
                if (method.Name == nameof(IPaystackService.VerifyTransaction))
                {
                    providerState.VerifyCalls++;
                    Assert.Equal(providerState.Reference, args![0]);
                    if (!providerState.TransactionVisible)
                        throw new PaystackTransactionNotFoundException();
                    return Task.FromResult(new PaystackVerifyResponse
                    {
                        Status = providerState.ProviderCreatedTransaction,
                        Data = new VerifyData { Reference = providerState.Reference!, Amount = 200_000, Currency = "NGN", Status = "pending" }
                    });
                }
                throw new NotSupportedException(method.Name);
            });
            var mediator = ApiOwnershipAuthorizationTests.InterfaceStub<IMediator>.Create((_, _) => throw new InvalidOperationException("Unexpected payment confirmation."));
            var handler = new PayForAcceptedBookingHandler(unitOfWork, paystack, NullLogger<PayForAcceptedBookingHandler>.Instance,
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Paystack:Currency"] = "NGN" }).Build(), mediator);
            return await Assert.ThrowsAsync<PaymentInitializationConflictException>(() => handler.Handle(
                new PayForAcceptedBookingCommand(booking.Id, booking.ClientId, "client@example.test", "Paystack", "test-user", null, key), TestContext.Current.CancellationToken));
        }

        var firstError = await RunAttemptAsync();
        Assert.Contains("reconciled", firstError.Message, StringComparison.OrdinalIgnoreCase);
        var secondError = await RunAttemptAsync();
        Assert.Contains("reconciliation", secondError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, providerState.InitializeCalls);
        Assert.Equal(1, providerState.VerifyCalls);
        Assert.True(providerState.ProviderCreatedTransaction);
        await using var verificationDb = new ApplicationDbContext(options);
        var stored = await new PaymentRepository(verificationDb).GetByBookingAndIdempotencyKeyAsync(booking.Id, key, TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Equal(PaymentInitializationStatus.ReconciliationRequired, stored!.InitializationStatus);
        Assert.Equal(providerState.Reference, stored.PaymentReference);
        Assert.Null(stored.AuthorizationUrl);
    }

    private sealed class InitializationProviderState
    {
        public int InitializeCalls { get; set; }
        public int VerifyCalls { get; set; }
        public bool ProviderCreatedTransaction { get; set; }
        public bool TransactionVisible { get; set; }
        public string? Reference { get; set; }
    }

    private static async Task VerifyConcurrentRefundAndPayoutReferenceRecoveryAsync(string connectionString, string schema)
    {
        var scopedBuilder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(scopedBuilder.ConnectionString, provider => provider.UseNetTopologySuite())
            .Options;
        var payment = Payment.Create(Guid.NewGuid(), 2500m, $"refund-source-{Guid.NewGuid():N}", "Paystack", "", DateTime.UtcNow, currency: "NGN");
        payment.MarkAsPaid("auth-test", "transaction-test");
        await using (var seedDb = new ApplicationDbContext(options))
        {
            seedDb.Payments.Add(payment);
            await seedDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var refundReference = $"refund-{payment.Id:N}";
        var refundBarrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task ReserveRefundAsync()
        {
            await using var db = new ApplicationDbContext(options);
            var repository = new PaymentRepository(db);
            var loaded = await repository.GetPaymentById(payment.Id, TestContext.Current.CancellationToken)
                ?? throw new InvalidOperationException("Seeded payment was not found.");
            await refundBarrier.Task;
            loaded.BeginRefund(refundReference, loaded.Amount);
            loaded.MarkRefundPending("Pending");
            await repository.UpdatePayment(loaded, TestContext.Current.CancellationToken);
        }
        var firstRefund = ReserveRefundAsync();
        var secondRefund = ReserveRefundAsync();
        refundBarrier.TrySetResult();
        await Task.WhenAll(firstRefund, secondRefund);

        await using (var verifyDb = new ApplicationDbContext(options))
        {
            var stored = await new PaymentRepository(verifyDb).GetPaymentById(payment.Id, TestContext.Current.CancellationToken);
            Assert.NotNull(stored);
            Assert.Equal(refundReference, stored!.RefundReference);
            Assert.Equal(payment.Amount, stored.RefundAmount);
            Assert.InRange(stored.RefundAmount, 0.01m, stored.Amount - stored.RefundedAmount);
            Assert.Throws<InvalidOperationException>(() => stored.BeginRefund($"different-{Guid.NewGuid():N}", stored.Amount));
        }

        var escrow = Escrow.Create(Guid.NewGuid(), payment.Id, Guid.NewGuid(), 2500m, 10m, 250m);
        var payoutReference = escrow.BeginPayoutAttempt();
        escrow.MarkPayoutUnknown();
        await using (var seedDb = new ApplicationDbContext(options))
        {
            seedDb.Escrows.Add(escrow);
            await seedDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var payoutBarrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<string> RetryPayoutAsync()
        {
            await using var db = new ApplicationDbContext(options);
            var loaded = await db.Escrows.SingleAsync(x => x.Id == escrow.Id, TestContext.Current.CancellationToken);
            await payoutBarrier.Task;
            var reference = loaded.BeginPayoutAttempt();
            try
            {
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
                return reference;
            }
            catch (DbUpdateConcurrencyException)
            {
                await using var retryDb = new ApplicationDbContext(options);
                var persisted = await retryDb.Escrows.SingleAsync(x => x.Id == escrow.Id, TestContext.Current.CancellationToken);
                Assert.Equal(reference, persisted.PaystackTransferReference);
                return persisted.PaystackTransferReference!;
            }
        }
        var firstPayout = RetryPayoutAsync();
        var secondPayout = RetryPayoutAsync();
        payoutBarrier.TrySetResult();
        var payoutReferences = await Task.WhenAll(firstPayout, secondPayout);
        Assert.All(payoutReferences, reference => Assert.Equal(payoutReference, reference));
        await using var payoutVerifyDb = new ApplicationDbContext(options);
        var storedEscrow = await payoutVerifyDb.Escrows.SingleAsync(x => x.Id == escrow.Id, TestContext.Current.CancellationToken);
        Assert.Equal(payoutReference, storedEscrow.PaystackTransferReference);
        Assert.Equal(1, storedEscrow.PayoutAttemptCount);
    }

    private static async Task VerifyRepositoryIdempotentReplayAsync(string connectionString, string schema)
    {
        var bookingId = Guid.NewGuid();
        var key = $"repo-replay-{Guid.NewGuid():N}";
        var hash = PaymentIdempotency.ComputeRequestHash(bookingId, 98m, "client@example.com", "Paystack", "NGN");
        var tasks = new[]
        {
            CreatePaymentWithRepositoryAsync(connectionString, schema, bookingId, key, hash),
            CreatePaymentWithRepositoryAsync(connectionString, schema, bookingId, key, hash)
        };
        var results = await Task.WhenAll(tasks);
        Assert.Single(results, result => result.Created);
        Assert.Single(results, result => !result.Created);
        Assert.Equal(results[0].Payment.Id, results[1].Payment.Id);
        PaymentIdempotency.EnsureSameRequest(results[0].Payment, hash);
        Assert.Throws<Clean_Connect.Application.Interface.Repositories.PaymentInitializationConflictException>(
            () => PaymentIdempotency.EnsureSameRequest(results[0].Payment, new string('f', 64)));

        var otherBooking = await CreatePaymentWithRepositoryAsync(connectionString, schema, Guid.NewGuid(), key, hash);
        Assert.True(otherBooking.Created);
    }

    private static async Task<(Payment Payment, bool Created)> CreatePaymentWithRepositoryAsync(
        string connectionString, string schema, Guid bookingId, string key, string hash)
    {
        var scopedBuilder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(scopedBuilder.ConnectionString, provider => provider.UseNetTopologySuite())
            .Options;
        await using var db = new ApplicationDbContext(options);
        var payment = Payment.Create(bookingId, 98m, $"cc-{Guid.NewGuid():N}", "Paystack", "", DateTime.UtcNow,
            idempotencyKey: key, requestHash: hash, currency: "NGN");
        return await new Clean_Connect.Persistence.Repositories.PaymentRepository(db)
            .CreatePaymentIdempotentlyAsync(payment, TestContext.Current.CancellationToken);
    }

    private static async Task<bool[]> RunTogetherAsync(string connectionString, Func<NpgsqlConnection, Task<bool>> first, Func<NpgsqlConnection, Task<bool>> second)
    {
        await using var firstConnection = new NpgsqlConnection(connectionString);
        await using var secondConnection = new NpgsqlConnection(connectionString);
        await Task.WhenAll(firstConnection.OpenAsync(TestContext.Current.CancellationToken), secondConnection.OpenAsync(TestContext.Current.CancellationToken));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Start(NpgsqlConnection connection, Func<NpgsqlConnection, Task<bool>> action)
        {
            await start.Task;
            return await action(connection);
        }
        var firstTask = Start(firstConnection, first);
        var secondTask = Start(secondConnection, second);
        start.TrySetResult();
        return await Task.WhenAll(firstTask, secondTask);
    }

    private static async Task<bool> TryInsertPaymentAsync(string connectionString, string schema, Guid bookingId, string key, string hash)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return await TryInsertPaymentAsync(connection, schema, bookingId, key, hash);
    }

    private static async Task<bool> TryInsertPaymentAsync(NpgsqlConnection connection, string schema, Guid bookingId, string key, string hash)
    {
        try
        {
            const string sql = @"
INSERT INTO {0}.""Payments""
 (""Id"", ""BookingId"", ""Amount"", ""PaymentMethod"", ""Status"", ""PaymentReference"", ""Provider"", ""DateCreated"", ""IsDeleted"", ""Currency"", ""IdempotencyKey"", ""RequestHash"", ""RefundStatus"")
VALUES (@id,@booking,2500,'Paystack','Pending',@reference,'paystack',@created,false,'NGN',@key,@hash,'NotStarted');";
            await using var command = new NpgsqlCommand(string.Format(System.Globalization.CultureInfo.InvariantCulture, sql, $"\"{schema}\""), connection);
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("booking", bookingId);
            command.Parameters.AddWithValue("reference", $"cc-{Guid.NewGuid():N}");
            command.Parameters.AddWithValue("created", DateTime.UtcNow);
            command.Parameters.AddWithValue("key", key);
            command.Parameters.AddWithValue("hash", hash);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            return true;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return false;
        }
    }

    private static async Task<string> ReadRequestHashAsync(string connectionString, string schema, Guid bookingId, string key)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand($"SELECT \"RequestHash\" FROM \"{schema}\".\"Payments\" WHERE \"BookingId\"=@booking AND \"IdempotencyKey\"=@key;", connection);
        command.Parameters.AddWithValue("booking", bookingId);
        command.Parameters.AddWithValue("key", key);
        return (string)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) ?? throw new InvalidOperationException("Payment row was not persisted."));
    }

    private static async Task<bool> RecordWebhookAsync(string connectionString, string schema, string eventKey)
    {
        var scopedBuilder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(scopedBuilder.ConnectionString, provider => provider.UseNetTopologySuite())
            .Options;
        await using var db = new ApplicationDbContext(options);
        var repository = new PaystackWebhookInboxRepository(db);
        var entity = PaystackWebhookInboxEvent.Receive(eventKey, "charge.success", "12345", "cc-test", "{}", DateTime.UtcNow);
        var result = await repository.RecordAsync(entity, TestContext.Current.CancellationToken);
        return result.Inserted;
    }

    private static async Task<Guid> ReadInboxIdAsync(string connectionString, string schema, string eventKey)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand($"SELECT \"Id\" FROM \"{schema}\".\"PaystackWebhookInboxEvents\" WHERE \"EventKey\"=@key;", connection);
        command.Parameters.AddWithValue("key", eventKey);
        return (Guid)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) ?? throw new InvalidOperationException("Webhook event was not persisted."));
    }
}
