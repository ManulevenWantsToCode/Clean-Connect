using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Query.BookingChatQuery;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Domain.Helper;
using Clean_Connect.Domain.Value_Objects;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Clean_Connect.Application.Tests
{
    public class BookingChatDetailsQueryTests
    {
        [Fact]
        public async Task Handle_WhenCurrentUserIsBookingClient_ReturnsClientChatDetails()
        {
            var fixture = ChatFixture.Create();
            var handler = fixture.CreateHandler(fixture.ClientEmail);

            var result = await handler.Handle(new GetBookingChatDetailsQuery(fixture.Booking.Id), CancellationToken.None);

            Assert.Equal(fixture.Booking.Id, result.BookingId);
            Assert.Equal("Client", result.ParticipantRole);
            Assert.Equal(fixture.Chat.ClientProviderUserId, result.CurrentUserProviderId);
            Assert.Equal(fixture.Chat.WorkerProviderUserId, result.OtherParticipantProviderId);
            Assert.Equal(fixture.Chat.ProviderConversationId, result.ConversationId);
            Assert.Equal("test-app-id", result.AppId);
            Assert.Equal("test-org#test-app", result.AppKey);
            Assert.Equal($"token:{fixture.Chat.ClientProviderUserId}", result.AccessToken);
            Assert.Equal(600, result.AccessTokenTtlSeconds);
            Assert.Equal("singleChat", result.ChatType);
        }

        [Fact]
        public async Task Handle_WhenCurrentUserIsAssignedWorker_ReturnsWorkerChatDetails()
        {
            var fixture = ChatFixture.Create();
            var handler = fixture.CreateHandler(fixture.WorkerEmail);

            var result = await handler.Handle(new GetBookingChatDetailsQuery(fixture.Booking.Id), CancellationToken.None);

            Assert.Equal("Worker", result.ParticipantRole);
            Assert.Equal(fixture.Chat.WorkerProviderUserId, result.CurrentUserProviderId);
            Assert.Equal(fixture.Chat.ClientProviderUserId, result.OtherParticipantProviderId);
            Assert.Equal($"token:{fixture.Chat.WorkerProviderUserId}", result.AccessToken);
        }

        [Fact]
        public async Task Handle_WhenCurrentUserIsNotBookingParticipant_ThrowsUnauthorizedAccessException()
        {
            var fixture = ChatFixture.Create();
            var handler = fixture.CreateHandler("stranger@example.com");

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => handler.Handle(new GetBookingChatDetailsQuery(fixture.Booking.Id), CancellationToken.None));
        }

        [Fact]
        public void Validator_WhenBookingIdIsEmpty_Fails()
        {
            var validator = new GetBookingChatDetailsQueryValidator();

            var result = validator.Validate(new GetBookingChatDetailsQuery(Guid.Empty));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, x => x.PropertyName == nameof(GetBookingChatDetailsQuery.BookingId));
        }

        [Fact]
        public void BookingChat_CreateForBooking_UsesDeterministicProviderIds()
        {
            var bookingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var clientId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var workerId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

            var chat = BookingChat.CreateForBooking(bookingId, clientId, workerId);

            Assert.Equal("Agora", chat.Provider);
            Assert.Equal("booking-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", chat.ProviderConversationId);
            Assert.Equal("client-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", chat.ClientProviderUserId);
            Assert.Equal("worker-cccccccccccccccccccccccccccccccc", chat.WorkerProviderUserId);
        }

        private sealed class ChatFixture
        {
            public string ClientEmail { get; } = "client@example.com";
            public string WorkerEmail { get; } = "worker@example.com";
            public Booking Booking { get; private init; } = default!;
            public BookingChat Chat { get; private init; } = default!;
            public Client Client { get; private init; } = default!;
            public Worker Worker { get; private init; } = default!;

            public static ChatFixture Create()
            {
                var clientId = Guid.NewGuid();
                var workerId = Guid.NewGuid();
                var serviceTypeId = Guid.NewGuid();
                var bookingId = Guid.NewGuid();

                var client = Client.Create(
                    FullName.Create("Ada", "Client"),
                    Address.Create("1 Clean Street"),
                    Email.Create("client@example.com"),
                    Location.Create(6.5, 3.4),
                    Gender.Female,
                    PhoneNumber.Create("08012345678"),
                    "Lagos",
                    DateTime.UtcNow.AddYears(-25),
                    "REF12345");
                client.Id = clientId;

                var worker = Worker.Create(
                    FullName.Create("Ben", "Worker"),
                    Address.Create("2 Clean Street"),
                    PhoneNumber.Create("08087654321"),
                    Location.Create(6.6, 3.5),
                    Gender.Male,
                    serviceTypeId,
                    Email.Create("worker@example.com"),
                    "Lagos",
                    DateTime.UtcNow.AddYears(-30),
                    5000m);
                worker.Id = workerId;

                var booking = Booking.Create(
                    clientId,
                    workerId,
                    Location.Create(6.5, 3.4),
                    DateTime.UtcNow.Date.AddDays(1),
                    DateTime.UtcNow,
                    DateTime.UtcNow.Date.AddDays(1).AddHours(9),
                    DateTime.UtcNow.Date.AddDays(1).AddHours(11),
                    5000m,
                    10000m,
                    10000m,
                    10000m,
                    TimeRange.Morning,
                    Address.Create("1 Clean Street"),
                    BookingStatus.Pending,
                    PaymentStatus.Pending,
                    serviceTypeId);
                booking.Id = bookingId;

                return new ChatFixture
                {
                    Booking = booking,
                    Chat = BookingChat.CreateForBooking(bookingId, clientId, workerId),
                    Client = client,
                    Worker = worker
                };
            }

            public GetBookingChatDetailsQueryHandler CreateHandler(string email)
            {
                var repo = new FakeUnitOfWork(Booking, Chat, Client, Worker);
                var currentUser = new FakeCurrentUser(email);

                return new GetBookingChatDetailsQueryHandler(
                    repo,
                    currentUser,
                    new FakeAgoraChatTokenService(),
                    new FakeAgoraChatProvisioningService(),
                    NullLogger<GetBookingChatDetailsQueryHandler>.Instance);
            }
        }

        private sealed class FakeAgoraChatTokenService : IAgoraChatTokenService
        {
            public AgoraChatTokenResult CreateUserToken(string userId)
            {
                return new AgoraChatTokenResult(
                    "test-app-id",
                    "test-org#test-app",
                    $"token:{userId}",
                    DateTimeOffset.UtcNow.AddMinutes(10),
                    600);
            }

            public string CreateAppToken() => "app-token";
        }

        private sealed class FakeAgoraChatProvisioningService : IAgoraChatProvisioningService
        {
            public List<string> ProvisionedUsers { get; } = [];

            public Task EnsureUsersExistAsync(IEnumerable<string> userIds, CancellationToken cancellationToken)
            {
                ProvisionedUsers.AddRange(userIds);
                return Task.CompletedTask;
            }
        }

        private sealed class FakeCurrentUser(string email) : ICurrentUser
        {
            public string? UserId { get; } = Guid.NewGuid().ToString();
            public string? Email { get; } = email;
            public string? Role { get; } = null;
            public bool IsAuthenticated { get; } = true;
        }

        private sealed class FakeUnitOfWork(
            Booking booking,
            BookingChat bookingChat,
            Client client,
            Worker worker) : IUnitOfWork
        {
            public IWorkerRepository Workers { get; } = new FakeWorkerRepository(worker);
            public IServiceTypeRepository ServiceTypes { get; } = new NotUsedServiceTypeRepository();
            public IClientRepository Clients { get; } = new FakeClientRepository(client);
            public IBookingRepository Bookings { get; } = new FakeBookingRepository(booking);
            public IBookingChatRepository BookingChats { get; } = new FakeBookingChatRepository(bookingChat);
            public IPaymentRepository Payments { get; } = new NotUsedPaymentRepository();
            public IRatingRepository Ratings { get; } = new NotUsedRatingRepository();
            public IWorkerBankDetailRepository WorkerBankDetails { get; } = new NotUsedWorkerBankDetailRepository();
            public IEscrowRepository Escrows { get; } = new NotUsedEscrowRepository();
            public ICouponRepository Coupons { get; } = new NotUsedCouponRepository();
            public INotificationRepository Notifications { get; } = new NotUsedNotificationRepository();

            public Task<int> SaveChangesAsync(CancellationToken cancellation) => Task.FromResult(0);
        }

        private sealed class FakeBookingRepository(Booking booking) : IBookingRepository
        {
            public Task CreateBooking(Booking booking, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task<BookingCreateResult?> GetIdempotentBookingAsync(Guid clientId, string key, string requestHash, CancellationToken cancellationToken) => Task.FromResult<BookingCreateResult?>(null);
            public Task<BookingCreateResult> CreateBookingIdempotentlyAsync(Booking booking, BookingChat bookingChat, CancellationToken cancellationToken) => Task.FromResult(new BookingCreateResult(booking.Id, true));
            public Task DeleteBooking(Booking booking, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task<List<Booking>> GetAllBookings(CancellationToken cancellationToken) => Task.FromResult(new List<Booking>());
            public Task<Booking> GetBookingById(Guid bookingId, CancellationToken cancellationToken) => Task.FromResult(bookingId == booking.Id ? booking : null!);
            public Task<List<Booking>> GetExpiredBookingsAsync(CancellationToken cancellationToken) => Task.FromResult(new List<Booking>());
            public Task UpdateBooking(Booking booking, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class FakeBookingChatRepository(BookingChat bookingChat) : IBookingChatRepository
        {
            public Task CreateBookingChat(BookingChat bookingChat, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task<BookingChat?> GetByBookingId(Guid bookingId, CancellationToken cancellationToken) => Task.FromResult(bookingId == bookingChat.BookingId ? bookingChat : null);
            public Task<int> GetUnreadCountForClientAsync(Guid clientId, CancellationToken cancellationToken) => Task.FromResult(bookingChat.ClientUnreadCount);
            public Task<int> GetUnreadCountForWorkerAsync(Guid workerId, CancellationToken cancellationToken) => Task.FromResult(bookingChat.WorkerUnreadCount);
            public Task<Dictionary<Guid, BookingChat>> GetByBookingIdsAsync(IEnumerable<Guid> bookingIds, CancellationToken cancellationToken) =>
                Task.FromResult(bookingIds.Contains(bookingChat.BookingId)
                    ? new Dictionary<Guid, BookingChat> { [bookingChat.BookingId] = bookingChat }
                    : new Dictionary<Guid, BookingChat>());
        }

        private sealed class FakeClientRepository(Client client) : IClientRepository
        {
            public Task CreateClient(Client client, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task DeleteClient(Client client, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task<IEnumerable<Client>> GetAllClients(CancellationToken cancellationToken) => Task.FromResult(Enumerable.Empty<Client>());
            public Task<Client?> GetByEmail(string email, CancellationToken cancellationToken) => Task.FromResult(string.Equals(email, client.Email.Value, StringComparison.OrdinalIgnoreCase) ? client : null);
            async Task<Client> IClientRepository.GetByEmail(string email, CancellationToken cancellationToken) => (await GetByEmail(email, cancellationToken))!;
            public Task<Client?> GetByReferralCodeAsync(string referralCode, CancellationToken cancellationToken) => Task.FromResult<Client?>(null);
            public Task<Client> GetClientById(Guid clientId, CancellationToken cancellationToken) => Task.FromResult(clientId == client.Id ? client : null!);
            public Task<Client> GetClientByName(string name, CancellationToken cancellationToken) => Task.FromResult<Client>(null!);
            public Task UpdateClient(Client client, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class FakeWorkerRepository(Worker worker) : IWorkerRepository
        {
            public Task CreateWorker(Worker worker, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task DeleteWorker(Worker worker, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task<IEnumerable<Worker>> GetAllWorkers(CancellationToken cancellationToken) => Task.FromResult(Enumerable.Empty<Worker>());
            public Task<List<Worker>> GetAllWorkersWithBookingsAsync(CancellationToken cancellationToken) => Task.FromResult(new List<Worker>());
            public Task<List<WorkerWithDistance>> GetAllWorkersWithDistanceAsync(double latitude, double longitude, CancellationToken cancellationToken) => Task.FromResult(new List<WorkerWithDistance>());
            public Task<List<WorkerWithDistance>> GetAvailableWorkersWithDistanceAsync(double? latitude, double? longitude, double? radiusInMeters, Guid? serviceTypeId, DateTime dateOfService, CancellationToken cancellationToken) => Task.FromResult(new List<WorkerWithDistance>());
            public Task<Worker> GetByEmail(string email, CancellationToken cancellationToken) => Task.FromResult(string.Equals(email, worker.Email.Value, StringComparison.OrdinalIgnoreCase) ? worker : null!);
            public Task<List<WorkerWithDistance>> GetNearByWorkersAsync(double latitude, double longitude, double radiusInMeters, Guid serviceType) => Task.FromResult(new List<WorkerWithDistance>());
            public Task<List<WorkerWithDistance>> GetNearbyWorkersWithBookingsAsync(double latitude, double longitude, double radiusInMeters, CancellationToken cancellationToken) => Task.FromResult(new List<WorkerWithDistance>());
            public Task<Worker> GetWorkerById(Guid workerId, CancellationToken cancellationToken) => Task.FromResult(workerId == worker.Id ? worker : null!);
            public Task<Worker> GetWorkerByName(string name, CancellationToken cancellationToken) => Task.FromResult<Worker>(null!);
            public Task UpdateWorker(Worker worker, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class NotUsedServiceTypeRepository : IServiceTypeRepository
        {
            public Task AddAsync(ServiceType serviceType, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task AddRangeAsync(IEnumerable<ServiceType> serviceTypes, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<bool> CheckExistingByName(string name, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task DeleteAsync(ServiceType serviceType, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<List<ServiceType>> GetAllAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<List<ServiceType>> GetAllIncludingDeletedAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<ServiceType> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task UpdateAsync(ServiceType serviceType, CancellationToken cancellationToken) => throw new NotSupportedException();
        }

        private sealed class NotUsedPaymentRepository : IPaymentRepository
        {
            public Task CreatePayment(Payment payment, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task DeletePayment(Guid paymentId, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<bool> ExistsByReferenceAsync(string reference, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<List<Payment>> GetAllPayments(CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<Payment?> GetByReferenceAsync(string paymentReference, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<List<Payment>> GetByStatusAsync(PaymentStatus status, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<Payment> GetPaymentById(Guid paymentId, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<List<Payment?>> GetPaymentsByBookingId(Guid bookingId, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task UpdatePayment(Payment payment, CancellationToken cancellationToken) => throw new NotSupportedException();
        }

        private sealed class NotUsedRatingRepository : IRatingRepository
        {
            public Task<bool> AddRating(Ratings ratings, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<bool> ExistAsync(Guid bookingId, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<List<Ratings>> GetAllRatingsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<IEnumerable<Ratings>> GetRatingsByWorkerId(Guid workerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        }

        private sealed class NotUsedWorkerBankDetailRepository : IWorkerBankDetailRepository
        {
            public Task CreateAsync(WorkerBankDetail bankDetail, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<List<WorkerBankDetail>> GetAllActiveAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<WorkerBankDetail?> GetActiveByWorkerIdAsync(Guid workerId, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task UpdateAsync(WorkerBankDetail bankDetail, CancellationToken cancellationToken) => throw new NotSupportedException();
        }

        private sealed class NotUsedEscrowRepository : IEscrowRepository
        {
            public Task CreateEscrow(Escrow escrow, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<List<Escrow>> GetAllEscrowsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<Escrow?> GetByBookingId(Guid bookingId, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<Escrow?> GetByPaymentId(Guid paymentId, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<Escrow?> GetByTransferReference(string reference, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task RefreshAsync(Escrow escrow, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<List<Escrow>> GetByStatus(EscrowStatus status, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<List<Escrow>> GetByWorkerId(Guid workerId, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task UpdateEscrow(Escrow escrow, CancellationToken cancellationToken) => throw new NotSupportedException();
        }

        private sealed class NotUsedCouponRepository : ICouponRepository
        {
            public Task<Coupon> CreateCouponAsync(Coupon coupon, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task DeleteCouponAsync(Coupon coupon, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<List<Coupon>> GetAllAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<Coupon?> GetByCodeAsync(string code, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<Coupon?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        }

        private sealed class NotUsedNotificationRepository : INotificationRepository
        {
            public Task CreateAsync(Notification notification, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<List<Notification>> GetByTargetAsync(Guid targetId, NotificationAudience audience, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<int> GetUnreadCountAsync(Guid targetId, NotificationAudience audience, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task MarkAllAsReadAsync(Guid targetId, NotificationAudience audience, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        }
    }
}
