using System.Security.Claims;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Infrastructure.Context;
using Clean_Connect.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Clean_Connect.Application.Tests;

public sealed class PostgreSqlAuditTrailTests
{
    [Fact]
    public async Task Business_change_and_audit_record_commit_or_rollback_together()
    {
        var connectionString = Environment.GetEnvironmentVariable("CLEAN_CONNECT_POSTGRES_TEST");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Set CLEAN_CONNECT_POSTGRES_TEST to a disposable, migrated PostgreSQL test database.");
            return;
        }

        var root = new NpgsqlConnectionStringBuilder(connectionString);
        if (root.Host is not ("localhost" or "127.0.0.1" or "::1")
            || string.IsNullOrWhiteSpace(root.Database)
            || !root.Database.Contains("test", StringComparison.OrdinalIgnoreCase)
            || root.Database.Contains("prod", StringComparison.OrdinalIgnoreCase)
            || root.Database.Contains("live", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Audit integration tests require a loopback-only test database.");

        var schema = $"cc_audit_test_{Guid.NewGuid():N}";
        await using var setup = new NpgsqlConnection(connectionString);
        await setup.OpenAsync(TestContext.Current.CancellationToken);
        await using (var createSchema = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\";", setup))
            await createSchema.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        try
        {
            await using (var clonePayments = new NpgsqlCommand($"CREATE TABLE \"{schema}\".\"Payments\" (LIKE public.\"Payments\" INCLUDING ALL);", setup))
                await clonePayments.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            await using (var cloneAudit = new NpgsqlCommand($"CREATE TABLE \"{schema}\".\"AuditRecords\" (LIKE public.\"AuditRecords\" INCLUDING ALL);", setup))
                await cloneAudit.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            await AssertMigrationsAppliedAsync(setup);

            var scoped = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema };
            var http = new DefaultHttpContext();
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "actor-42")], "test"));
            http.Items["X-Correlation-ID"] = "request-42";
            var accessor = new HttpContextAccessor { HttpContext = http };
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(scoped.ConnectionString, provider => provider.UseNetTopologySuite())
                .Options;

            string reference;
            await using (var db = new ApplicationDbContext(options, accessor))
            {
                var payment = CreatePayment();
                reference = payment.PaymentReference;
                db.Payments.Add(payment);
                db.Payments.Add(CreatePayment());
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);

                var audits = await db.AuditRecords.Where(x => x.EntityType == "Payment").ToListAsync(TestContext.Current.CancellationToken);
                Assert.Equal(2, audits.Count);
                var audit = audits.Single(x => x.EntityId == payment.Id.ToString());
                Assert.Equal("actor-42", audit.ActorId);
                Assert.Equal("User", audit.ActorKind);
                Assert.Equal("Payment", audit.EntityType);
                Assert.Equal("Created", audit.Action);
                Assert.Equal("request-42", audit.CorrelationId);
                Assert.DoesNotContain(reference, audit.ChangedFields, StringComparison.Ordinal);
                Assert.Contains("PaymentReference", audit.ChangedFields, StringComparison.Ordinal);

                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
                Assert.Equal(2, await db.AuditRecords.CountAsync(x => x.EntityType == "Payment", TestContext.Current.CancellationToken));
            }

            var loginWriter = new AuditTrailWriter(options, accessor);
            var accountId = Guid.NewGuid();
            await loginWriter.RecordLoginAsync(accountId, true, TestContext.Current.CancellationToken);
            await loginWriter.RecordLoginAsync(null, false, TestContext.Current.CancellationToken);
            await using (var db = new ApplicationDbContext(options, accessor))
            {
                var loginAudits = await db.AuditRecords.Where(x => x.EntityType == "Authentication").ToListAsync(TestContext.Current.CancellationToken);
                Assert.Equal(2, loginAudits.Count);
                Assert.Contains(loginAudits, x => x.Action == "LoginSucceeded" && x.ActorId == accountId.ToString() && x.ActorKind == "User");
                Assert.Contains(loginAudits, x => x.Action == "LoginFailed" && x.ActorId == null && x.ActorKind == "Anonymous");
                Assert.All(loginAudits, x => Assert.Equal("request-42", x.CorrelationId));
            }

            await using (var db = new ApplicationDbContext(options, accessor))
            {
                var persistedAudit = await db.AuditRecords.FirstAsync(x => x.EntityType == "Payment", TestContext.Current.CancellationToken);
                db.AuditRecords.Remove(persistedAudit);
                Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
            }

            await using (var db = new ApplicationDbContext(options, accessor))
            {
                var persistedAudit = await db.AuditRecords.FirstAsync(x => x.EntityType == "Payment", TestContext.Current.CancellationToken);
                db.Entry(persistedAudit).Property(x => x.Action).CurrentValue = "Tampered";
                db.Entry(persistedAudit).Property(x => x.Action).IsModified = true;
                Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
            }

            await using (var db = new ApplicationDbContext(options, accessor))
            {
                db.Payments.Add(Payment.Create(Guid.NewGuid(), 100m, reference, "Paystack", string.Empty, DateTime.UtcNow));
                await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
            }

            await AssertAuditFailureRollsBackPaymentAsync(options, scoped.ConnectionString, setup, schema, accessor);

            await using var verify = new NpgsqlConnection(scoped.ConnectionString);
            await verify.OpenAsync(TestContext.Current.CancellationToken);
            await using var count = new NpgsqlCommand("SELECT (SELECT count(*) FROM \"Payments\"), (SELECT count(*) FROM \"AuditRecords\");", verify);
            await using var reader = await count.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
            // Two initial payments plus the payment whose first audit insert failed and was retried.
            Assert.Equal(3L, reader.GetInt64(0));
            Assert.Equal(5L, reader.GetInt64(1));

            await using var auditDb = new ApplicationDbContext(options, accessor);
            var paymentAudits = await auditDb.AuditRecords
                .Where(x => x.EntityType == "Payment")
                .OrderBy(x => x.EntityId)
                .ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(3, paymentAudits.Count);
            Assert.Equal(3, paymentAudits.Select(x => x.EntityId).Distinct(StringComparer.Ordinal).Count());
            Assert.All(paymentAudits, x =>
            {
                Assert.Equal("Created", x.Action);
                Assert.Equal("request-42", x.CorrelationId);
            });
        }
        finally
        {
            await using var dropSchema = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;", setup);
            await dropSchema.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }

    private static Payment CreatePayment() =>
        Payment.Create(Guid.NewGuid(), 100m, $"audit-{Guid.NewGuid():N}", "Paystack", string.Empty, DateTime.UtcNow);

    private static async Task AssertMigrationsAppliedAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("""
            SELECT
                (SELECT count(*) FROM public."__EFMigrationsHistory"
                    WHERE "MigrationId" IN ('20261114182918_OperationalAuditTrail', '20261114185045_WebhookCorrelationId')),
                (SELECT count(*) FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'AuditRecords'
                    AND indexname IN ('IX_AuditRecords_OccurredAtUtc', 'IX_AuditRecords_EntityType_EntityId_OccurredAtUtc')),
                (SELECT count(*) FROM information_schema.columns WHERE table_schema = 'public'
                    AND table_name = 'PaystackWebhookInboxEvents' AND column_name = 'CorrelationId'),
                (SELECT count(*) FROM information_schema.table_constraints WHERE table_schema = 'public'
                    AND table_name = 'AuditRecords' AND constraint_type = 'PRIMARY KEY');
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2L, reader.GetInt64(0));
        Assert.Equal(2L, reader.GetInt64(1));
        Assert.Equal(1L, reader.GetInt64(2));
        Assert.Equal(1L, reader.GetInt64(3));
    }

    private static async Task AssertAuditFailureRollsBackPaymentAsync(DbContextOptions<ApplicationDbContext> options,
        string scopedConnectionString, NpgsqlConnection setup, string schema, IHttpContextAccessor accessor)
    {
        var createTriggerSql = $"""
            CREATE FUNCTION "{schema}"."reject_payment_audit"() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW."EntityType" = 'Payment' THEN RAISE EXCEPTION 'simulated audit persistence failure'; END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER "reject_payment_audit" BEFORE INSERT ON "{schema}"."AuditRecords"
            FOR EACH ROW EXECUTE FUNCTION "{schema}"."reject_payment_audit"();
            """;
        await using (var createTrigger = new NpgsqlCommand(createTriggerSql, setup))
            await createTrigger.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        try
        {
            await using (var db = new ApplicationDbContext(options, accessor))
            {
                db.Payments.Add(CreatePayment());
                await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));

                await using (var dropTrigger = new NpgsqlCommand($"DROP TRIGGER IF EXISTS \"reject_payment_audit\" ON \"{schema}\".\"AuditRecords\"; DROP FUNCTION IF EXISTS \"{schema}\".\"reject_payment_audit\"();", setup))
                    await dropTrigger.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

                // Retry the same tracked changes after the audit store recovers.
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var verify = new NpgsqlConnection(scopedConnectionString);
            await verify.OpenAsync(TestContext.Current.CancellationToken);
            await using var count = new NpgsqlCommand("SELECT count(*) FROM \"Payments\";", verify);
            Assert.Equal(3L, (long)(await count.ExecuteScalarAsync(TestContext.Current.CancellationToken))!);
            await using var auditCount = new NpgsqlCommand("SELECT count(*) FROM \"AuditRecords\" WHERE \"EntityType\" = 'Payment';", verify);
            Assert.Equal(3L, (long)(await auditCount.ExecuteScalarAsync(TestContext.Current.CancellationToken))!);
        }
        finally
        {
            await using var dropTrigger = new NpgsqlCommand($"DROP TRIGGER IF EXISTS \"reject_payment_audit\" ON \"{schema}\".\"AuditRecords\"; DROP FUNCTION IF EXISTS \"{schema}\".\"reject_payment_audit\"();", setup);
            await dropTrigger.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }
}
