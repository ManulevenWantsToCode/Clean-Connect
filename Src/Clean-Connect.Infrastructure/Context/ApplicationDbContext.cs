using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Events;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Text.Json;

namespace Clean_Connect.Infrastructure.Context
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor? httpContextAccessor = null) : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<Worker> Workers { get; set; }

        public DbSet<Client> Clients { get; set; }

        public DbSet<ServiceType> ServiceTypes { get; set; }    

        public DbSet<Booking> Bookings { get; set; }
        public DbSet<BookingChat> BookingChats { get; set; }
        public DbSet<Ratings> Ratings { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<PaystackWebhookInboxEvent> PaystackWebhookInboxEvents { get; set; }
        public DbSet<WorkerBankDetail> WorkerBankDetails { get; set; }
        public DbSet<Escrow> Escrows { get; set; }
        public DbSet<Coupon> Coupons { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<PlatformSettings> PlatformSettings { get; set; }
        public DbSet<AuditRecord> AuditRecords { get; set; }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            AddAuditRecords();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            AddAuditRecords();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void AddAuditRecords()
        {
            var entries = ChangeTracker.Entries().ToArray();
            if (entries.Any(x => x.Entity is AuditRecord && x.State is EntityState.Modified or EntityState.Deleted))
                throw new InvalidOperationException("Audit history is append-only.");

            var auditable = new HashSet<string>(StringComparer.Ordinal)
            {
                nameof(Booking), nameof(Payment), nameof(Escrow), nameof(WorkerBankDetail),
                nameof(Worker), nameof(Client), nameof(Coupon), nameof(PlatformSettings), nameof(ApplicationUser)
            };
            var http = _httpContextAccessor?.HttpContext;
            var actorId = http?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var correlationId = http?.Items["X-Correlation-ID"] as string;

            foreach (var entry in entries.Where(x => auditable.Contains(x.Metadata.ClrType.Name)
                && x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                var action = entry.State switch
                {
                    EntityState.Added => "Created",
                    EntityState.Deleted => "Deleted",
                    _ => "Updated"
                };
                var changedFields = GetSafeChangedFields(entry);
                var serializedChangedFields = JsonSerializer.Serialize(changedFields);
                var entityId = entry.Property("Id").CurrentValue?.ToString();
                if (string.IsNullOrWhiteSpace(entityId))
                    continue;

                var alreadyPending = ChangeTracker.Entries<AuditRecord>().Any(auditEntry =>
                    auditEntry.State == EntityState.Added
                    && auditEntry.Entity.Action == action
                    && auditEntry.Entity.EntityType == entry.Metadata.ClrType.Name
                    && auditEntry.Entity.EntityId == entityId
                    && auditEntry.Entity.ActorId == actorId
                    && auditEntry.Entity.CorrelationId == correlationId
                    && auditEntry.Entity.ChangedFields == serializedChangedFields);
                if (alreadyPending)
                    continue;

                AuditRecords.Add(AuditRecord.Create(DateTime.UtcNow, actorId, action,
                    entry.Metadata.ClrType.Name, entityId, serializedChangedFields, correlationId));
            }
        }

        private static string[] GetSafeChangedFields(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry) =>
            entry.Properties
                .Where(property => property.IsModified || entry.State == EntityState.Added)
                .Select(property => property.Metadata.Name)
                .Where(name => name is not "PasswordHash" and not "SecurityStamp" and not "ConcurrencyStamp"
                    and not "AccessFailedCount" and not "LockoutEnd" and not "PhoneNumber"
                    and not "Email" and not "NormalizedEmail" and not "UserName" and not "NormalizedUserName")
                .OrderBy(name => name, StringComparer.Ordinal)
                .Distinct()
                .ToArray();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(new UtcDateInterceptor());
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Ignore<DomainEvent>();
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }
    }
}
