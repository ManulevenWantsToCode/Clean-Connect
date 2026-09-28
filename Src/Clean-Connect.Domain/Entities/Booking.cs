using Clean_Connect.Domain.Enums;
using Clean_Connect.Domain.Events;
using Clean_Connect.Domain.Utilities;
using Clean_Connect.Domain.Value_Objects;

namespace Clean_Connect.Domain.Entities
{
    public class Booking : BaseEntity
    {
        private Booking() { }

        private Booking(Guid clientId, Guid workerId, DateTime dateOfBooking,Location location, DateTime dateOfService, DateTime startTime, DateTime endTime, decimal hourlyRate, decimal totalAmount, decimal amount, decimal originalAmount, TimeRange timeRange, PaymentStatus paymentStatus, Address address, Guid serviceTypeId, BookingStatus bookingStatus, Guid? couponId = null, string? createdBy = null)
        {
            ClientId = clientId;
            WorkerId = workerId;
            ServiceTypeId = serviceTypeId;
            Amount = amount;
            OriginalAmount = originalAmount;
            CouponId = couponId;
            PaymentStatus = paymentStatus;
            Location = location;
            Address = address;
            DateOfBooking = dateOfBooking;
            DateOfService = dateOfService;
            StartTime = startTime;
            EndTime = endTime;
            HourlyRate = hourlyRate;
            TotalAmount = totalAmount;
            BookingStatus = bookingStatus;
        }

        public Client Client { get; private set; }

        public Worker Worker { get; private set; }

        public PaymentStatus PaymentStatus { get; private set; }
        public Address Address { get; private set; }
        public Guid ClientId { get; private set; } = default!;

        public Guid WorkerId { get; private set; } = default!;

        public Location Location { get; private set; }

        public DateTime DateOfBooking { get; private set; } = default!;
        
        public Ratings Ratings { get; private set; }
        public TimeRange TimeRange { get; private set; } = default!;

        public decimal Amount { get; private set; } = default!;
        public decimal OriginalAmount { get; private set; } = default!;
        public decimal HourlyRate { get; private set; } = default!;
        public decimal TotalAmount { get; private set; } = default!;
        public Guid? CouponId { get; private set; }
        public Coupon? Coupon { get; private set; }
        public DateTime DateOfService { get; private set; } = default!;
        public DateTime StartTime { get; private set; } = default!;
        public DateTime EndTime { get; private set; } = default!;
        public ServiceType ServiceType { get; private set; }
        public Guid ServiceTypeId { get; private set; } = default!;

        public BookingStatus BookingStatus { get; private set; } = default!;



        public static Booking Create(Guid clientId, Guid workerId, Location location, DateTime dateOfService, DateTime dateOfBooking, DateTime startTime, DateTime endTime, decimal hourlyRate, decimal totalAmount, decimal amount, decimal originalAmount, TimeRange timeRange, Address address, BookingStatus bookingStatus, PaymentStatus paymentStatus, Guid serviceTypeId, Guid? couponId = null, string? createdBy = null)
        {
            var booking = new Booking(clientId, workerId, dateOfBooking,location, dateOfService, startTime, endTime, hourlyRate, totalAmount, amount, originalAmount, timeRange, paymentStatus, address, serviceTypeId, bookingStatus, couponId, createdBy);
            booking.AddDomainEvent(new BookingCreatedEvent(booking.Id));
            booking.UpdateMetadata(createdBy);
            return booking;

        }

        public void Accept()
        {
            if (BookingStatus != BookingStatus.Pending)
            {
                throw new InvalidOperationException("Only pending bookings can be accepted.");
            }
            BookingStatus = BookingStatus.AcceptedAwaitingPayment;
            PaymentStatus = PaymentStatus.Pending;

            AddDomainEvent(new BookingAcceptedEvent(Id));
        }

        public void Reject()
        {
            if (BookingStatus != BookingStatus.Pending)
            {
                throw new InvalidOperationException("Only pending bookings can be rejected.");
            }
            BookingStatus = BookingStatus.Rejected;
            PaymentStatus = PaymentStatus.Canceled;
            

        }

        public void RequestStartJob()
        {
            if (BookingStatus != BookingStatus.MarkAsPaid)
            {
                throw new InvalidOperationException("Only paid bookings can be requested to start.");
            }

            BookingStatus = BookingStatus.AwaitingClientStartConfirmation;
        }

        public void StartJob()
        {
            if (BookingStatus != BookingStatus.AwaitingClientStartConfirmation)
            {
                throw new InvalidOperationException("Only bookings confirmed by the client can be started.");
            }

            BookingStatus = BookingStatus.InProgress;
        }

        public void MarkAsPaid()
        {
            if (BookingStatus != BookingStatus.AcceptedAwaitingPayment)
            {
                throw new InvalidOperationException("Only accepted bookings can be marked as paid.");
            }
            BookingStatus = BookingStatus.MarkAsPaid;
            PaymentStatus = PaymentStatus.Successful;
        }   

        public void MarkAsAwaitingClientConfirmation()
        {
            if (BookingStatus != BookingStatus.InProgress)
            {
                throw new InvalidOperationException("Only in-progress bookings can be marked as awaiting client confirmation.");
            }
            BookingStatus = BookingStatus.AwaitingClientConfirmation;
          
        }

        public void MarkAsCompleted()
        {
            if (BookingStatus != BookingStatus.AwaitingClientConfirmation &&
                BookingStatus != BookingStatus.InProgress &&
                BookingStatus != BookingStatus.MarkAsPaid)
            {
                throw new InvalidOperationException("Only paid, in-progress, or awaiting confirmation bookings can be marked as completed.");
            }
            BookingStatus = BookingStatus.Completed;
        }

        public bool CanExpire => BookingStatus is
            BookingStatus.Pending or
            BookingStatus.AcceptedAwaitingPayment or
            BookingStatus.MarkAsPaid or
            BookingStatus.AwaitingClientStartConfirmation or
            BookingStatus.InProgress or
            BookingStatus.AwaitingClientConfirmation;

        public void MarkAsExpired()
        {
            if (!CanExpire)
            {
                throw new InvalidOperationException("Only active bookings that have passed their service date can be marked as expired.");
            }
            BookingStatus = BookingStatus.Expired;
        }

        public void RefundPayment()
        {
            PaymentStatus = PaymentStatus.Refunded;
        }

        public void AssignWorker(Guid workerId, string? modifiedBy = null)
        {
            if (workerId == Guid.Empty)
                throw new ArgumentException("Worker id cannot be empty.", nameof(workerId));

            if (BookingStatus != BookingStatus.Pending)
                throw new InvalidOperationException("Only pending bookings can be assigned a cleaner.");

            WorkerId = workerId;
            UpdateMetadata(modifiedBy);
        }
    }
}
