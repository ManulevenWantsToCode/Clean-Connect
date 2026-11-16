using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Persistence.Repositories
{
    

    public class PaymentRepository(ApplicationDbContext dbContext) : IPaymentRepository
    {
        public async Task<(Payment Payment, bool Created)> CreatePaymentIdempotentlyAsync(Payment payment, CancellationToken cancellationToken)
        {
            dbContext.Payments.Add(payment);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return (payment, true);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                dbContext.Entry(payment).State = EntityState.Detached;
                var existingByKey = await GetByBookingAndIdempotencyKeyAsync(payment.BookingId, payment.IdempotencyKey!, cancellationToken);
                if (existingByKey is not null)
                    return (existingByKey, false);

                var activeAttempt = await GetPendingByBookingAsync(payment.BookingId, cancellationToken);
                if (activeAttempt is not null)
                    return (activeAttempt, false);
                throw;
            }
        }

        public Task<Payment?> GetByBookingAndIdempotencyKeyAsync(Guid bookingId, string idempotencyKey, CancellationToken cancellationToken) =>
            dbContext.Payments.SingleOrDefaultAsync(p => p.BookingId == bookingId && p.IdempotencyKey == idempotencyKey, cancellationToken);

        public Task<Payment?> GetPendingByBookingAsync(Guid bookingId, CancellationToken cancellationToken) =>
            dbContext.Payments.SingleOrDefaultAsync(p => p.BookingId == bookingId && p.Status == PaymentStatus.Pending, cancellationToken);

        public async Task CreatePayment(Payment payment, CancellationToken cancellationToken)
        {
            await dbContext.AddAsync(payment, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<Payment?> GetPaymentById(Guid paymentId, CancellationToken cancellationToken)
        {
            return await dbContext.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        }

        public async Task UpdatePayment(Payment payment, CancellationToken cancellationToken)
        {
            dbContext.Payments.Update(payment);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<Payment?> GetByReferenceAsync(string paymentReference, CancellationToken cancellationToken)
        {
            return await dbContext.Payments.FirstOrDefaultAsync(p => p.PaymentReference == paymentReference, cancellationToken);
        }

        public async Task<Payment?> GetFreshByReferenceAsync(string paymentReference, CancellationToken cancellationToken)
        {
            var payment = await dbContext.Payments.FirstOrDefaultAsync(p => p.PaymentReference == paymentReference, cancellationToken);
            if (payment is not null)
                await dbContext.Entry(payment).ReloadAsync(cancellationToken);
            return payment;
        }

        public Task<Payment?> GetByRefundReferenceAsync(string refundReference, CancellationToken cancellationToken) =>
            dbContext.Payments.SingleOrDefaultAsync(p => p.RefundReference == refundReference, cancellationToken);

        public async Task<List<Payment>> GetByStatusAsync(PaymentStatus status, CancellationToken cancellationToken)
        {
            return await dbContext.Payments.Where(p => p.Status == status).ToListAsync(cancellationToken);
        }

        public async Task<bool> ExistsByReferenceAsync(string reference, CancellationToken cancellationToken)
        {
            return await dbContext.Payments.AnyAsync(p => p.PaymentReference == reference, cancellationToken);
        }

        public async Task<List<Payment>> GetAllPayments(CancellationToken cancellationToken)
        {
            return await dbContext.Payments.ToListAsync(cancellationToken);
        }
        public async Task<List<Payment>> GetPaymentsByBookingId(Guid bookingId, CancellationToken cancellationToken)
        {
            return await dbContext.Payments.Where(p => p.BookingId == bookingId).ToListAsync(cancellationToken);
        }
        public async Task DeletePayment(Guid paymentId, CancellationToken cancellationToken)
        {
            var payment = await GetPaymentById(paymentId, cancellationToken);
            if (payment != null)
            {
                dbContext.Payments.Remove(payment);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }

}
