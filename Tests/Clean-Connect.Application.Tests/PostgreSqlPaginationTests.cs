using System.Data.Common;
using Clean_Connect.Application.DTO.Pagination;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Infrastructure.Context;
using Clean_Connect.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Xunit;

namespace Clean_Connect.Application.Tests;

public sealed class PostgreSqlPaginationTests
{
    [Fact]
    public async Task Payment_pagination_filters_sorts_and_pages_in_PostgreSQL_without_loading_all_rows()
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
            throw new InvalidOperationException("Pagination integration tests require a loopback-only database explicitly named for testing.");

        var schema = $"cc_pagination_test_{Guid.NewGuid():N}";
        await using var setup = new NpgsqlConnection(connectionString);
        await setup.OpenAsync(TestContext.Current.CancellationToken);
        await using (var createSchema = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\";", setup))
            await createSchema.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        try
        {
            await using (var cloneTable = new NpgsqlCommand($"CREATE TABLE \"{schema}\".\"Payments\" (LIKE public.\"Payments\" INCLUDING ALL);", setup))
                await cloneTable.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            for (var amount = 100; amount <= 500; amount += 100)
                await InsertPaymentAsync(setup, schema, amount, "Pending");
            await InsertPaymentAsync(setup, schema, 900, "Successful");

            var queryLog = new PaginationCommandInterceptor();
            var scopedConnection = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema };
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(scopedConnection.ConnectionString, provider => provider.UseNetTopologySuite())
                .AddInterceptors(queryLog)
                .Options;
            await using var db = new ApplicationDbContext(options);
            var repository = new PagedReadRepository(db);

            var first = await repository.GetPaymentsAsync(new PaymentPageRequest
            {
                Status = PaymentStatus.Pending, Page = 1, PageSize = 2,
                SortBy = "amount", SortDirection = "desc"
            }, TestContext.Current.CancellationToken);
            var second = await repository.GetPaymentsAsync(new PaymentPageRequest
            {
                Status = PaymentStatus.Pending, Page = 2, PageSize = 2,
                SortBy = "amount", SortDirection = "desc"
            }, TestContext.Current.CancellationToken);
            var third = await repository.GetPaymentsAsync(new PaymentPageRequest
            {
                Status = PaymentStatus.Pending, Page = 3, PageSize = 2,
                SortBy = "amount", SortDirection = "desc"
            }, TestContext.Current.CancellationToken);

            Assert.Equal(new[] { 500m, 400m }, first.Items.Select(x => x.Amount));
            Assert.Equal(new[] { 300m, 200m }, second.Items.Select(x => x.Amount));
            Assert.Equal(new[] { 100m }, third.Items.Select(x => x.Amount));
            Assert.All(new[] { first, second, third }, result =>
            {
                Assert.Equal(5, result.TotalCount);
                Assert.Equal(3, result.TotalPages);
            });
            Assert.False(first.HasPreviousPage);
            Assert.True(first.HasNextPage);
            Assert.True(second.HasPreviousPage);
            Assert.True(second.HasNextPage);
            Assert.True(third.HasPreviousPage);
            Assert.False(third.HasNextPage);
            Assert.Equal(5, new[] { first, second, third }.SelectMany(x => x.Items).Select(x => x.Id).Distinct().Count());

            var empty = await repository.GetPaymentsAsync(new PaymentPageRequest { Status = PaymentStatus.Failed }, TestContext.Current.CancellationToken);
            Assert.Empty(empty.Items);
            Assert.Equal(0, empty.TotalCount);
            Assert.Equal(0, empty.TotalPages);
            Assert.Contains(queryLog.ReaderCommands, command => command.Contains("LIMIT", StringComparison.OrdinalIgnoreCase)
                && command.Contains("OFFSET", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await using var dropSchema = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;", setup);
            await dropSchema.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }

    private static async Task InsertPaymentAsync(NpgsqlConnection connection, string schema, decimal amount, string status)
    {
        var sql = $"INSERT INTO \"{schema}\".\"Payments\" (\"Id\",\"BookingId\",\"Amount\",\"PaymentMethod\",\"Status\",\"PaymentReference\",\"Provider\",\"DateCreated\",\"IsDeleted\",\"Currency\",\"RefundStatus\") VALUES (@id,@booking,@amount,'Paystack',@status,@reference,'paystack',@created,false,'NGN','NotStarted');";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("booking", Guid.NewGuid());
        command.Parameters.AddWithValue("amount", amount);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("reference", $"page-{Guid.NewGuid():N}");
        command.Parameters.AddWithValue("created", DateTime.UtcNow);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private sealed class PaginationCommandInterceptor : DbCommandInterceptor
    {
        public List<string> ReaderCommands { get; } = new();

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ReaderCommands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
