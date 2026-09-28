using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.WorkerCommands
{
    public record CreateRatingCommand(Guid WorkerId, Guid ClientId, Guid BookingId, int RatingValue, string? Comment, string? CreatedBy = null) : IRequest<bool>;


    public class CreateRatingCommandValidator : AbstractValidator<CreateRatingCommand>
    {
        public CreateRatingCommandValidator()
        {
            RuleFor(x => x.WorkerId)
                .NotEmpty()
                .WithMessage("Worker Id is required")
                .Must(id => id != Guid.Empty)
                .WithMessage("Invalid Id");

            RuleFor(x => x.ClientId)
                .NotEmpty()
                .WithMessage("Client Id is required")
                .Must(id => id != Guid.Empty)
                .WithMessage("Invalid Id");

            RuleFor(x => x.BookingId)
                .NotEmpty()
                .WithMessage("Booking Id is required")
                .Must(id => id != Guid.Empty)
                .WithMessage("Invalid Id");

            RuleFor(x => x.RatingValue)
                .InclusiveBetween(1, 5)
                .WithMessage("Rating value must be between 1 and 5.");

        }



    }

    public class CreateRatingHandler : IRequestHandler<CreateRatingCommand, bool>
    {
        private readonly IUnitOfWork repo;
        private readonly ILogger<CreateRatingHandler> logger;

        public CreateRatingHandler(IUnitOfWork _repo, ILogger<CreateRatingHandler> _logger)
        {
            repo = _repo;
            logger = _logger;
        }

        public async Task<bool> Handle(CreateRatingCommand request, CancellationToken cancellationToken)
        {
            var checkBooking = await repo.Bookings.GetBookingById(request.BookingId, cancellationToken);

            if (checkBooking == null)
            {
                logger.LogWarning("Rating creation failed. Booking not found: {BookingId}", request.BookingId);
                throw new ValidationException("Booking not found");
            }

            if (checkBooking.WorkerId != request.WorkerId)
            {
                logger.LogWarning("Rating creation failed. WorkerId does not match : {WorkerId}", request.WorkerId);
                throw new ValidationException("WorkerId does not match");
            }
            if (checkBooking.ClientId != request.ClientId)
            {
                logger.LogWarning("Rating creation failed. ClientId does not match : {ClientId}", request.ClientId);
                throw new ValidationException("ClientId does not match");
            }

            if (checkBooking.BookingStatus != BookingStatus.Completed)
            {
                logger.LogWarning("Rating creation failed. Booking {BookingId} is not completed.", request.BookingId);
                throw new ValidationException("Booking must be completed before it can be rated.");
            }

            if (request.RatingValue < 1 || request.RatingValue > 5)
            {
                throw new ArgumentOutOfRangeException(nameof(request.RatingValue), "Rating value must be between 1 and 5.");
            }

            var checkExistingRating = await repo.Ratings.ExistAsync(checkBooking.Id, cancellationToken);
            if (checkExistingRating == true)
            {
                logger.LogWarning("Booking already rated");
                throw new Exception("Already rated");
            }

            var rating = Ratings.Create(
                checkBooking.WorkerId,
                checkBooking.ClientId,
                checkBooking.Id,
                request.RatingValue,
                request.Comment,
                request.CreatedBy
                );


             repo.Ratings.AddRating(rating, cancellationToken);

            await repo.SaveChangesAsync(cancellationToken);

            var worker = await repo.Workers.GetWorkerById(request.WorkerId, cancellationToken);
            if(worker == null)
            {
                throw new ValidationException("Worker not found");
            }

            var oldAverage = worker.AverageRating;

            var totalRatings = worker.TotalRating;

            var newTotalRating = worker.TotalRating + 1;

            var newAverageRating = ((oldAverage * totalRatings) + request.RatingValue) / (double)newTotalRating;

            worker.UpdateRating(newTotalRating, newAverageRating);

            repo.Workers.UpdateWorker(worker, cancellationToken);

            await repo.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Rating created successfully for Worker {WorkerId}", request.WorkerId);

            return true;




        }
    }
}
