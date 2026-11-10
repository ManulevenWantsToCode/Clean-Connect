using Clean_Connect.Application.Command.BookingChatCommand;
using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Application.Query.BookingChatQuery;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Domain.Helper;
using Clean_Connect.Domain.Value_Objects;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Clean_Connect.Application.Tests
{
    public class MessageNotificationTests
    {
        // ──────────────────────────────────────────────────
        //  RecordMessageSentCommand
        // ──────────────────────────────────────────────────

        [Fact]
        public async Task RecordMessageSent_ClientSendsMessage_WorkerGetsNotification()
        {
            var f = MessageFixture.Create();
            var notifier = new SpyNotificationService();
            var handler = f.CreateRecordMessageSentHandler(f.ClientEmail, notifier);

            var result = await handler.Handle(
                new RecordMessageSentCommand(f.Booking.Id, "Hello worker!"),
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(f.Booking.Id, result.BookingId);
            Assert.Equal("Client", result.SenderRole);
            Assert.Equal(f.WorkerEmail, result.RecipientEmail);
            Assert.Contains("Hello worker!", result.MessagePreview);
            Assert.True(result.RecipientUnreadCount >= 1);

            // SignalR notification was sent to the worker's email
            Assert.Single(notifier.MessageNotifications);
            var sent = notifier.MessageNotifications[0];
            Assert.Equal(f.WorkerEmail, sent.RecipientEmail);
            Assert.Equal(f.Booking.Id, sent.BookingId);
            Assert.Contains("Hello worker!", sent.MessagePreview);
        }

        [Fact]
        public async Task RecordMessageSent_WorkerSendsMessage_ClientGetsNotification()
        {
            var f = MessageFixture.Create();
            var notifier = new SpyNotificationService();
            var handler = f.CreateRecordMessageSentHandler(f.WorkerEmail, notifier);

            var result = await handler.Handle(
                new RecordMessageSentCommand(f.Booking.Id, "Hello client!"),
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("Worker", result.SenderRole);
            Assert.Equal(f.ClientEmail, result.RecipientEmail);

            Assert.Single(notifier.MessageNotifications);
            Assert.Equal(f.ClientEmail, notifier.MessageNotifications[0].RecipientEmail);
        }

        [Fact]
        public async Task RecordMessageSent_UnauthorizedUser_ThrowsUnauthorizedAccess()
        {
            var f = MessageFixture.Create();
            var handler = f.CreateRecordMessageSentHandler("stranger@example.com", new SpyNotificationService());

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                handler.Handle(
                    new RecordMessageSentCommand(f.Booking.Id, "Sneaky message"),
                    CancellationToken.None));
        }

        [Fact]
        public async Task RecordMessageSent_UnauthenticatedUser_ThrowsUnauthorizedAccess()
        {
            var f = MessageFixture.Create();
            var handler = f.CreateRecordMessageSentHandler(null, new SpyNotificationService(), isAuthenticated: false);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                handler.Handle(
                    new RecordMessageSentCommand(f.Booking.Id, "No auth"),
                    CancellationToken.None));
        }

        [Fact]
        public void RecordMessageSentValidator_EmptyBookingId_Fails()
        {
            var validator = new RecordMessageSentCommandValidator();

            var result = validator.Validate(new RecordMessageSentCommand(Guid.Empty, "Hello"));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(RecordMessageSentCommand.BookingId));
        }

        [Fact]
        public void RecordMessageSentValidator_EmptyMessageText_Fails()
        {
            var validator = new RecordMessageSentCommandValidator();

            var result = validator.Validate(new RecordMessageSentCommand(Guid.NewGuid(), ""));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(RecordMessageSentCommand.MessageText));
        }

        [Fact]
        public void RecordMessageSentValidator_MessageTextExceedsMaxLength_Fails()
        {
            var validator = new RecordMessageSentCommandValidator();
            var longText = new string('A', 1001);

            var result = validator.Validate(new RecordMessageSentCommand(Guid.NewGuid(), longText));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(RecordMessageSentCommand.MessageText));
        }

        [Fact]
        public async Task RecordMessageSent_LongMessage_TruncatesPreview()
        {
            var f = MessageFixture.Create();
            var notifier = new SpyNotificationService();
            var handler = f.CreateRecordMessageSentHandler(f.ClientEmail, notifier);
            var longMsg = new string('X', 200);

            var result = await handler.Handle(
                new RecordMessageSentCommand(f.Booking.Id, longMsg),
                CancellationToken.None);

            Assert.True(result.MessagePreview.Length <= 120);
            Assert.EndsWith("...", result.MessagePreview);
        }

        [Fact]
        public async Task RecordMessageSent_IncrementsWorkerUnreadCount_WhenClientSends()
        {
            var f = MessageFixture.Create();
            var handler = f.CreateRecordMessageSentHandler(f.ClientEmail, new SpyNotificationService());

            await handler.Handle(new RecordMessageSentCommand(f.Booking.Id, "msg1"), CancellationToken.None);
            await handler.Handle(new RecordMessageSentCommand(f.Booking.Id, "msg2"), CancellationToken.None);

            Assert.Equal(2, f.Chat.WorkerUnreadCount);
            Assert.Equal(0, f.Chat.ClientUnreadCount);
        }

        [Fact]
        public async Task RecordMessageSent_IncrementsClientUnreadCount_WhenWorkerSends()
        {
            var f = MessageFixture.Create();
            var handler = f.CreateRecordMessageSentHandler(f.WorkerEmail, new SpyNotificationService());

            await handler.Handle(new RecordMessageSentCommand(f.Booking.Id, "msg1"), CancellationToken.None);

            Assert.Equal(1, f.Chat.ClientUnreadCount);
            Assert.Equal(0, f.Chat.WorkerUnreadCount);
        }

        // ──────────────────────────────────────────────────
        //  MarkConversationReadCommand
        // ──────────────────────────────────────────────────

        [Fact]
        public async Task MarkConversationRead_Client_ResetsClientUnreadCount()
        {
            var f = MessageFixture.Create();
            var notifier = new SpyNotificationService();

            // Simulate worker sent 3 messages to client
            f.Chat.RecordMessageFromWorker("m1", DateTime.UtcNow);
            f.Chat.RecordMessageFromWorker("m2", DateTime.UtcNow);
            f.Chat.RecordMessageFromWorker("m3", DateTime.UtcNow);
            Assert.Equal(3, f.Chat.ClientUnreadCount);

            var handler = f.CreateMarkConversationReadHandler(f.ClientEmail, notifier);
            var remaining = await handler.Handle(new MarkConversationReadCommand(f.Booking.Id), CancellationToken.None);

            Assert.Equal(0, f.Chat.ClientUnreadCount);
            Assert.Equal(0, remaining);
            Assert.Single(notifier.UnreadCountUpdates);
            Assert.Equal(f.ClientEmail, notifier.UnreadCountUpdates[0].Email);
            Assert.Equal(0, notifier.UnreadCountUpdates[0].Count);
        }

        [Fact]
        public async Task MarkConversationRead_Worker_ResetsWorkerUnreadCount()
        {
            var f = MessageFixture.Create();
            var notifier = new SpyNotificationService();

            f.Chat.RecordMessageFromClient("m1", DateTime.UtcNow);
            f.Chat.RecordMessageFromClient("m2", DateTime.UtcNow);
            Assert.Equal(2, f.Chat.WorkerUnreadCount);

            var handler = f.CreateMarkConversationReadHandler(f.WorkerEmail, notifier);
            var remaining = await handler.Handle(new MarkConversationReadCommand(f.Booking.Id), CancellationToken.None);

            Assert.Equal(0, f.Chat.WorkerUnreadCount);
            Assert.Equal(0, remaining);
        }

        [Fact]
        public async Task MarkConversationRead_UnauthorizedUser_ThrowsUnauthorizedAccess()
        {
            var f = MessageFixture.Create();
            var handler = f.CreateMarkConversationReadHandler("stranger@example.com", new SpyNotificationService());

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                handler.Handle(new MarkConversationReadCommand(f.Booking.Id), CancellationToken.None));
        }

        [Fact]
        public void MarkConversationReadValidator_EmptyBookingId_Fails()
        {
            var validator = new MarkConversationReadCommandValidator();

            var result = validator.Validate(new MarkConversationReadCommand(Guid.Empty));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(MarkConversationReadCommand.BookingId));
        }

        // ──────────────────────────────────────────────────
        //  GetUnreadMessageCountQuery
        // ──────────────────────────────────────────────────

        [Fact]
        public async Task GetUnreadMessageCount_ClientWithUnread_ReturnsCount()
        {
            var f = MessageFixture.Create();
            f.Chat.RecordMessageFromWorker("m1", DateTime.UtcNow);
            f.Chat.RecordMessageFromWorker("m2", DateTime.UtcNow);

            var handler = f.CreateGetUnreadCountHandler(f.ClientEmail);
            var count = await handler.Handle(new GetUnreadMessageCountQuery(), CancellationToken.None);

            Assert.Equal(2, count);
        }

        [Fact]
        public async Task GetUnreadMessageCount_WorkerWithUnread_ReturnsCount()
        {
            var f = MessageFixture.Create();
            f.Chat.RecordMessageFromClient("m1", DateTime.UtcNow);
            f.Chat.RecordMessageFromClient("m2", DateTime.UtcNow);
            f.Chat.RecordMessageFromClient("m3", DateTime.UtcNow);

            var handler = f.CreateGetUnreadCountHandler(f.WorkerEmail);
            var count = await handler.Handle(new GetUnreadMessageCountQuery(), CancellationToken.None);

            Assert.Equal(3, count);
        }

        [Fact]
        public async Task GetUnreadMessageCount_NoUnread_ReturnsZero()
        {
            var f = MessageFixture.Create();

            var handler = f.CreateGetUnreadCountHandler(f.ClientEmail);
            var count = await handler.Handle(new GetUnreadMessageCountQuery(), CancellationToken.None);

            Assert.Equal(0, count);
        }

        [Fact]
        public async Task GetUnreadMessageCount_UnauthenticatedUser_ReturnsZero()
        {
            var f = MessageFixture.Create();
            f.Chat.RecordMessageFromWorker("m1", DateTime.UtcNow);

            var handler = f.CreateGetUnreadCountHandler(null, isAuthenticated: false);
            var count = await handler.Handle(new GetUnreadMessageCountQuery(), CancellationToken.None);

            Assert.Equal(0, count);
        }

        // ──────────────────────────────────────────────────
        //  Domain Entity Tests
        // ──────────────────────────────────────────────────

        [Fact]
        public void BookingChat_RecordMessageFromClient_IncrementsWorkerUnread()
        {
            var chat = BookingChat.CreateForBooking(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            var now = DateTime.UtcNow;

            chat.RecordMessageFromClient("Hello", now);
            chat.RecordMessageFromClient("How are you?", now);

            Assert.Equal(2, chat.WorkerUnreadCount);
            Assert.Equal(0, chat.ClientUnreadCount);
            Assert.Equal("How are you?", chat.LastMessageText);
            Assert.Equal("Client", chat.LastMessageSender);
        }

        [Fact]
        public void BookingChat_RecordMessageFromWorker_IncrementsClientUnread()
        {
            var chat = BookingChat.CreateForBooking(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            var now = DateTime.UtcNow;

            chat.RecordMessageFromWorker("I'm here", now);

            Assert.Equal(0, chat.WorkerUnreadCount);
            Assert.Equal(1, chat.ClientUnreadCount);
            Assert.Equal("I'm here", chat.LastMessageText);
            Assert.Equal("Worker", chat.LastMessageSender);
        }

        [Fact]
        public void BookingChat_MarkAsReadByClient_ResetsClientUnread()
        {
            var chat = BookingChat.CreateForBooking(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

            chat.RecordMessageFromWorker("m1", DateTime.UtcNow);
            chat.RecordMessageFromWorker("m2", DateTime.UtcNow);
            Assert.Equal(2, chat.ClientUnreadCount);

            chat.MarkAsReadByClient();

            Assert.Equal(0, chat.ClientUnreadCount);
            Assert.NotNull(chat.ClientLastReadAt);
        }

        [Fact]
        public void BookingChat_MarkAsReadByWorker_ResetsWorkerUnread()
        {
            var chat = BookingChat.CreateForBooking(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

            chat.RecordMessageFromClient("m1", DateTime.UtcNow);
            chat.RecordMessageFromClient("m2", DateTime.UtcNow);
            chat.RecordMessageFromClient("m3", DateTime.UtcNow);
            Assert.Equal(3, chat.WorkerUnreadCount);

            chat.MarkAsReadByWorker();

            Assert.Equal(0, chat.WorkerUnreadCount);
            Assert.NotNull(chat.WorkerLastReadAt);
        }

        [Fact]
        public void BookingChat_ClientSendsThenWorkerMarksRead_DoesNotAffectClientUnread()
        {
            var chat = BookingChat.CreateForBooking(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            var now = DateTime.UtcNow;

            chat.RecordMessageFromClient("m1", now);   // worker unread +1
            chat.RecordMessageFromWorker("m2", now);    // client unread +1
            Assert.Equal(1, chat.WorkerUnreadCount);
            Assert.Equal(1, chat.ClientUnreadCount);

            chat.MarkAsReadByWorker();  // resets only worker unread

            Assert.Equal(0, chat.WorkerUnreadCount);
            Assert.Equal(1, chat.ClientUnreadCount);    // client unread unchanged
        }

        // ──────────────────────────────────────────────────
        //  Test Infrastructure
        // ──────────────────────────────────────────────────

        private sealed class MessageFixture
        {
            public string ClientEmail { get; } = "client@example.com";
            public string WorkerEmail { get; } = "worker@example.com";
            public Booking Booking { get; private init; } = default!;
            public BookingChat Chat { get; private init; } = default!;
            public Client Client { get; private init; } = default!;
            public Worker Worker { get; private init; } = default!;

            public static MessageFixture Create()
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

                return new MessageFixture
                {
                    Booking = booking,
                    Chat = BookingChat.CreateForBooking(bookingId, clientId, workerId),
                    Client = client,
                    Worker = worker
                };
            }

            public RecordMessageSentCommandHandler CreateRecordMessageSentHandler(
                string? email,
                IMessageRealtimeNotificationService notifier,
                bool isAuthenticated = true)
            {
                var uow = new FakeUnitOfWork(Booking, Chat, Client, Worker);
                var currentUser = new FakeCurrentUser(email, isAuthenticated);

                return new RecordMessageSentCommandHandler(
                    uow,
                    currentUser,
                    notifier,
                    NullLogger<RecordMessageSentCommandHandler>.Instance);
            }

            public MarkConversationReadCommandHandler CreateMarkConversationReadHandler(
                string? email,
                IMessageRealtimeNotificationService notifier,
                bool isAuthenticated = true)
            {
                var uow = new FakeUnitOfWork(Booking, Chat, Client, Worker);
                var currentUser = new FakeCurrentUser(email, isAuthenticated);

                return new MarkConversationReadCommandHandler(
                    uow,
                    currentUser,
                    notifier,
                    NullLogger<MarkConversationReadCommandHandler>.Instance);
            }

            public GetUnreadMessageCountQueryHandler CreateGetUnreadCountHandler(
                string? email,
                bool isAuthenticated = true)
            {
                var uow = new FakeUnitOfWork(Booking, Chat, Client, Worker);
                var currentUser = new FakeCurrentUser(email, isAuthenticated);

                return new GetUnreadMessageCountQueryHandler(
                    uow,
                    currentUser,
                    NullLogger<GetUnreadMessageCountQueryHandler>.Instance);
            }
        }

        private sealed class SpyNotificationService : IMessageRealtimeNotificationService
        {
            public List<(string? RecipientEmail, Guid BookingId, string SenderName, string MessagePreview, int UnreadCount, string MessageId)> MessageNotifications { get; } = [];
            public List<(string? Email, int Count)> UnreadCountUpdates { get; } = [];
            public List<(string? RecipientEmail, Guid BookingId, string ReaderRole, DateTime ReadAtUtc)> ReadNotifications { get; } = [];

            public Task SendMessageNotificationAsync(string? recipientEmail, Guid bookingId, string senderName, string messagePreview, int unreadCount, string messageId)
            {
                MessageNotifications.Add((recipientEmail, bookingId, senderName, messagePreview, unreadCount, messageId));
                return Task.CompletedTask;
            }

            public Task UpdateUnreadMessageCountAsync(string? userEmail, int unreadCount)
            {
                UnreadCountUpdates.Add((userEmail, unreadCount));
                return Task.CompletedTask;
            }

            public Task NotifyConversationReadAsync(string? recipientEmail, Guid bookingId, string readerRole, DateTime readAtUtc)
            {
                ReadNotifications.Add((recipientEmail, bookingId, readerRole, readAtUtc));
                return Task.CompletedTask;
            }
        }

        private sealed class FakeCurrentUser(string? email, bool isAuthenticated = true) : ICurrentUser
        {
            public string? UserId { get; } = Guid.NewGuid().ToString();
            public string? Email { get; } = email;
            public string? Role { get; } = null;
            public bool IsAuthenticated { get; } = isAuthenticated;
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
