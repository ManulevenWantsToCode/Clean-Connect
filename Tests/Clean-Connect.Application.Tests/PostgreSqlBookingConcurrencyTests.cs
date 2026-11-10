using Clean_Connect.Application.Interface.Repositories;
using Npgsql;
using Xunit;

namespace Clean_Connect.Application.Tests;

public class PostgreSqlBookingConcurrencyTests
{
    [Fact]
    public async Task PostgreSQL_enforces_active_reservations_and_client_scoped_idempotency_under_concurrency()
    {
        var connectionString = Environment.GetEnvironmentVariable("CLEAN_CONNECT_POSTGRES_TEST");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Set CLEAN_CONNECT_POSTGRES_TEST to a disposable PostgreSQL database whose migrations have been applied.");
            return;
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (builder.Host is not ("localhost" or "127.0.0.1" or "::1") ||
            !builder.Database.Contains("test", StringComparison.OrdinalIgnoreCase) ||
            builder.Database.Contains("prod", StringComparison.OrdinalIgnoreCase) ||
            builder.Database.Contains("live", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("PostgreSQL integration tests require an explicitly named, loopback-only test database.");

        await VerifyAppliedMigrationAsync(connectionString);

        var schema = $"cc_booking_test_{Guid.NewGuid():N}";
        await using var setup = new NpgsqlConnection(connectionString);
        await setup.OpenAsync(TestContext.Current.CancellationToken);
        await using (var createSchema = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\";", setup))
            await createSchema.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        var table = $"\"{schema}\".\"Bookings\"";
        try
        {
            await using (var cloneMigratedTable = new NpgsqlCommand($"CREATE TABLE {table} (LIKE public.\"Bookings\" INCLUDING ALL);", setup))
                await cloneMigratedTable.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

            await VerifySimultaneousClientRequestsAsync(connectionString, table);
            await VerifySimultaneousAssignmentsAsync(connectionString, table);
            await VerifyAdjacentBookingsAsync(connectionString, table);
            await VerifyReleasedAndCompletedBookingsAsync(connectionString, table);
            await VerifyIdempotencyAsync(connectionString, setup, table);
        }
        finally
        {
            await using var dropSchema = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;", setup);
            await dropSchema.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }

    private static async Task VerifyAppliedMigrationAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        const string sql = @"
SELECT
    (SELECT count(*) FROM pg_constraint WHERE conname = 'EX_Bookings_Worker_Time_Active'),
    (SELECT count(*) FROM pg_constraint WHERE conname = 'CK_Bookings_ValidInterval'),
    (SELECT count(*) FROM pg_indexes WHERE indexname = 'IX_Bookings_ClientId_IdempotencyKey'),
    (SELECT count(*) FROM pg_extension WHERE extname = 'btree_gist');";
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1L, reader.GetInt64(0));
        Assert.Equal(1L, reader.GetInt64(1));
        Assert.Equal(1L, reader.GetInt64(2));
        Assert.Equal(1L, reader.GetInt64(3));
    }

    private static async Task VerifySimultaneousClientRequestsAsync(string connectionString, string table)
    {
        var workerId = Guid.NewGuid();
        var clientA = Guid.NewGuid();
        var clientB = Guid.NewGuid();
        var start = Utc(9);
        var end = Utc(11);
        var outcomes = await RunTogetherAsync(connectionString,
            connection => TryInsertBookingAsync(connection, table, clientA, workerId, start, end, "Pending"),
            connection => TryInsertBookingAsync(connection, table, clientB, workerId, Utc(10), Utc(12), "Pending"));

        Assert.Single(outcomes, created => created);
    }

    private static async Task VerifySimultaneousAssignmentsAsync(string connectionString, string table)
    {
        var initialWorkerA = Guid.NewGuid();
        var initialWorkerB = Guid.NewGuid();
        var targetWorker = Guid.NewGuid();
        var bookingA = await InsertBookingAsync(connectionString, table, Guid.NewGuid(), initialWorkerA, Utc(13), Utc(15), "Pending");
        var bookingB = await InsertBookingAsync(connectionString, table, Guid.NewGuid(), initialWorkerB, Utc(14), Utc(16), "Pending");

        var outcomes = await RunTogetherAsync(connectionString,
            connection => TryAssignWorkerAsync(connection, table, bookingA, targetWorker),
            connection => TryAssignWorkerAsync(connection, table, bookingB, targetWorker));

        Assert.Single(outcomes, assigned => assigned);
    }

    private static async Task VerifyAdjacentBookingsAsync(string connectionString, string table)
    {
        var worker = Guid.NewGuid();
        Assert.NotEqual(Guid.Empty, await InsertBookingAsync(connectionString, table, Guid.NewGuid(), worker, Utc(9), Utc(11), "Pending"));
        Assert.NotEqual(Guid.Empty, await InsertBookingAsync(connectionString, table, Guid.NewGuid(), worker, Utc(11), Utc(12), "Pending"));
    }

    private static async Task VerifyReleasedAndCompletedBookingsAsync(string connectionString, string table)
    {
        var cancelledWorker = Guid.NewGuid();
        var cancelled = await InsertBookingAsync(connectionString, table, Guid.NewGuid(), cancelledWorker, Utc(9), Utc(11), "Pending");
        await SetStatusAsync(connectionString, table, cancelled, "Rejected");
        Assert.NotEqual(Guid.Empty, await InsertBookingAsync(connectionString, table, Guid.NewGuid(), cancelledWorker, Utc(9), Utc(11), "Pending"));

        var completedWorker = Guid.NewGuid();
        Assert.NotEqual(Guid.Empty, await InsertBookingAsync(connectionString, table, Guid.NewGuid(), completedWorker, Utc(9), Utc(11), "Completed"));
        Assert.NotEqual(Guid.Empty, await InsertBookingAsync(connectionString, table, Guid.NewGuid(), completedWorker, Utc(10), Utc(12), "Pending"));
    }

    private static async Task VerifyIdempotencyAsync(string connectionString, NpgsqlConnection setup, string table)
    {
        var clientId = Guid.NewGuid();
        const string key = "concurrent-retry-key";
        const string payloadHashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string payloadHashB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var outcomes = await RunTogetherAsync(connectionString,
            connection => TryInsertIdempotencyAsync(connection, table, clientId, key, payloadHashA),
            connection => TryInsertIdempotencyAsync(connection, table, clientId, key, payloadHashA));

        Assert.Single(outcomes, created => created);
        Assert.False(await TryInsertIdempotencyAsync(connectionString, table, clientId, key, payloadHashA));

        var storedHash = await ReadRequestHashAsync(setup, table, clientId, key);
        BookingIdempotency.EnsureSameRequest(storedHash, payloadHashA);
        Assert.Throws<IdempotencyConflictException>(() => BookingIdempotency.EnsureSameRequest(storedHash, payloadHashB));

        Assert.True(await TryInsertIdempotencyAsync(connectionString, table, Guid.NewGuid(), key, payloadHashB));
        Assert.Equal(2L, await CountIdempotencyKeyAsync(setup, table, key));
    }

    private static async Task<bool[]> RunTogetherAsync(string connectionString, Func<NpgsqlConnection, Task<bool>> first, Func<NpgsqlConnection, Task<bool>> second)
    {
        await using var firstConnection = new NpgsqlConnection(connectionString);
        await using var secondConnection = new NpgsqlConnection(connectionString);
        await Task.WhenAll(
            firstConnection.OpenAsync(TestContext.Current.CancellationToken),
            secondConnection.OpenAsync(TestContext.Current.CancellationToken));

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Start(NpgsqlConnection connection, Func<NpgsqlConnection, Task<bool>> operation)
        {
            await start.Task;
            return await operation(connection);
        }

        var firstTask = Start(firstConnection, first);
        var secondTask = Start(secondConnection, second);
        start.TrySetResult();
        return await Task.WhenAll(firstTask, secondTask);
    }

    private static async Task<Guid> InsertBookingAsync(string connectionString, string table, Guid clientId, Guid workerId, DateTime start, DateTime end, string status)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var id = Guid.NewGuid();
        var sql = $@"INSERT INTO {table}
(""Id"",""PaymentStatus"",""Address"",""ClientId"",""WorkerId"",""Latitude"",""Longitude"",""LocationPoint"",""DateOfBooking"",""TimeRange"",""Amount"",""OriginalAmount"",""DateOfService"",""ServiceTypeId"",""BookingStatus"",""DateCreated"",""IsDeleted"",""EndTime"",""HourlyRate"",""StartTime"",""TotalAmount"")
VALUES (@id,'Pending','Test address',@client,@worker,6.5,3.3,ST_GeogFromText('SRID=4326;POINT(3.3 6.5)'),@bookingDate,'Morning',200,200,@serviceDate,@serviceType,@status,@dateCreated,false,@end,100,@start,200);";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("client", clientId);
        command.Parameters.AddWithValue("worker", workerId);
        command.Parameters.AddWithValue("start", start);
        command.Parameters.AddWithValue("end", end);
        command.Parameters.AddWithValue("bookingDate", Utc(8));
        command.Parameters.AddWithValue("serviceDate", start.Date);
        command.Parameters.AddWithValue("serviceType", Guid.NewGuid());
        command.Parameters.AddWithValue("dateCreated", Utc(8));
        command.Parameters.AddWithValue("status", status);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return id;
    }

    private static async Task<bool> TryInsertBookingAsync(string connectionString, string table, Guid clientId, Guid workerId, DateTime start, DateTime end, string status)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return await TryInsertBookingAsync(connection, table, clientId, workerId, start, end, status);
    }

    private static async Task<bool> TryInsertBookingAsync(NpgsqlConnection connection, string table, Guid clientId, Guid workerId, DateTime start, DateTime end, string status)
    {
        try
        {
            var id = Guid.NewGuid();
            var sql = $@"INSERT INTO {table}
(""Id"",""PaymentStatus"",""Address"",""ClientId"",""WorkerId"",""Latitude"",""Longitude"",""LocationPoint"",""DateOfBooking"",""TimeRange"",""Amount"",""OriginalAmount"",""DateOfService"",""ServiceTypeId"",""BookingStatus"",""DateCreated"",""IsDeleted"",""EndTime"",""HourlyRate"",""StartTime"",""TotalAmount"")
VALUES (@id,'Pending','Test address',@client,@worker,6.5,3.3,ST_GeogFromText('SRID=4326;POINT(3.3 6.5)'),@bookingDate,'Morning',200,200,@serviceDate,@serviceType,@status,@dateCreated,false,@end,100,@start,200);";
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("client", clientId);
            command.Parameters.AddWithValue("worker", workerId);
            command.Parameters.AddWithValue("start", start);
            command.Parameters.AddWithValue("end", end);
            command.Parameters.AddWithValue("bookingDate", Utc(8));
            command.Parameters.AddWithValue("serviceDate", start.Date);
            command.Parameters.AddWithValue("serviceType", Guid.NewGuid());
            command.Parameters.AddWithValue("dateCreated", Utc(8));
            command.Parameters.AddWithValue("status", status);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            return true;
        }
        catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.ExclusionViolation or PostgresErrorCodes.DeadlockDetected)
        {
            return false;
        }
    }

    private static async Task<bool> TryAssignWorkerAsync(string connectionString, string table, Guid bookingId, Guid workerId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return await TryAssignWorkerAsync(connection, table, bookingId, workerId);
    }

    private static async Task<bool> TryAssignWorkerAsync(NpgsqlConnection connection, string table, Guid bookingId, Guid workerId)
    {
        try
        {
            await using var command = new NpgsqlCommand($"UPDATE {table} SET \"WorkerId\"=@worker WHERE \"Id\"=@id;", connection);
            command.Parameters.AddWithValue("worker", workerId);
            command.Parameters.AddWithValue("id", bookingId);
            return await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken) == 1;
        }
        catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.ExclusionViolation or PostgresErrorCodes.DeadlockDetected)
        {
            return false;
        }
    }

    private static async Task SetStatusAsync(string connectionString, string table, Guid bookingId, string status)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand($"UPDATE {table} SET \"BookingStatus\"=@status WHERE \"Id\"=@id;", connection);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("id", bookingId);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<bool> TryInsertIdempotencyAsync(string connectionString, string table, Guid clientId, string key, string requestHash)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return await TryInsertIdempotencyAsync(connection, table, clientId, key, requestHash);
    }

    private static async Task<bool> TryInsertIdempotencyAsync(NpgsqlConnection connection, string table, Guid clientId, string key, string requestHash)
    {
        var sql = $@"INSERT INTO {table}
(""Id"",""PaymentStatus"",""Address"",""ClientId"",""WorkerId"",""Latitude"",""Longitude"",""LocationPoint"",""DateOfBooking"",""TimeRange"",""Amount"",""OriginalAmount"",""DateOfService"",""ServiceTypeId"",""BookingStatus"",""DateCreated"",""IsDeleted"",""EndTime"",""HourlyRate"",""StartTime"",""TotalAmount"",""IdempotencyKey"",""RequestHash"")
VALUES (@id,'Pending','Test address',@client,@worker,6.5,3.3,ST_GeogFromText('SRID=4326;POINT(3.3 6.5)'),@bookingDate,'Morning',200,200,@serviceDate,@serviceType,'Completed',@dateCreated,false,@end,100,@start,200,@key,@hash)
ON CONFLICT (""ClientId"",""IdempotencyKey"") WHERE ""IdempotencyKey"" IS NOT NULL DO NOTHING RETURNING ""Id"";";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("client", clientId);
        command.Parameters.AddWithValue("worker", Guid.NewGuid());
        command.Parameters.AddWithValue("start", Utc(17));
        command.Parameters.AddWithValue("end", Utc(18));
        command.Parameters.AddWithValue("bookingDate", Utc(8));
        command.Parameters.AddWithValue("serviceDate", Utc(17).Date);
        command.Parameters.AddWithValue("serviceType", Guid.NewGuid());
        command.Parameters.AddWithValue("dateCreated", Utc(8));
        command.Parameters.AddWithValue("key", key);
        command.Parameters.AddWithValue("hash", requestHash);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) is not null;
    }

    private static async Task<string> ReadRequestHashAsync(NpgsqlConnection connection, string table, Guid clientId, string key)
    {
        await using var command = new NpgsqlCommand($"SELECT \"RequestHash\" FROM {table} WHERE \"ClientId\"=@client AND \"IdempotencyKey\"=@key;", connection);
        command.Parameters.AddWithValue("client", clientId);
        command.Parameters.AddWithValue("key", key);
        return (string)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task<long> CountIdempotencyKeyAsync(NpgsqlConnection connection, string table, string key)
    {
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM {table} WHERE \"IdempotencyKey\"=@key;", connection);
        command.Parameters.AddWithValue("key", key);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static DateTime Utc(int hour) => new(2030, 5, 1, hour, 0, 0, DateTimeKind.Utc);
}
