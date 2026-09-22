namespace Clean_Connect.Domain.Events
{
    public sealed class BookingAcceptedEvent : DomainEvent
    {
        public Guid BookingId { get; }

        public BookingAcceptedEvent(Guid bookingId)
        {
            BookingId = bookingId;
        }
    }
}
