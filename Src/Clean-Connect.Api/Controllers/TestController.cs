using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Clean_Connect.Infrastructure.Context;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Value_Objects;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Application.Command.PaymentCommand;
using MediatR;

namespace Clean_Connect.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [AllowAnonymous]
    public class TestController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly IMediator _mediator;

        public TestController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<Guid>> roleManager,
            IMediator mediator)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _mediator = mediator;
        }

        [HttpPost("seed-and-pay")]
        public async Task<IActionResult> SeedAndPay(CancellationToken cancellationToken)
        {
            try
            {
                // 1. Ensure Roles exist
                var roles = new[] { "Worker", "Client" };
                foreach (var r in roles)
                {
                    if (!await _roleManager.RoleExistsAsync(r))
                    {
                        await _roleManager.CreateAsync(new IdentityRole<Guid>(r));
                    }
                }

                // 2. Create and confirm Identity User for Client
                var clientEmailStr = $"client_{Guid.NewGuid().ToString().Substring(0, 8)}@example.com";
                var clientUser = ApplicationUser.Create(clientEmailStr);
                var clientUserResult = await _userManager.CreateAsync(clientUser, "Password123!");
                if (!clientUserResult.Succeeded)
                {
                    return BadRequest(new { message = "Failed to create client user", errors = clientUserResult.Errors });
                }
                clientUser.EmailConfirmed = true;
                await _userManager.UpdateAsync(clientUser);
                await _userManager.AddToRoleAsync(clientUser, "Client");

                // 3. Create and confirm Identity User for Worker
                var workerEmailStr = $"worker_{Guid.NewGuid().ToString().Substring(0, 8)}@example.com";
                var workerUser = ApplicationUser.Create(workerEmailStr);
                var workerUserResult = await _userManager.CreateAsync(workerUser, "Password123!");
                if (!workerUserResult.Succeeded)
                {
                    return BadRequest(new { message = "Failed to create worker user", errors = workerUserResult.Errors });
                }
                workerUser.EmailConfirmed = true;
                await _userManager.UpdateAsync(workerUser);
                await _userManager.AddToRoleAsync(workerUser, "Worker");

                // 4. Create ServiceType
                var serviceType = ServiceType.Create(
                    name: $"House Cleaning Service {Guid.NewGuid().ToString().Substring(0, 8)}",
                    description: "High quality premium deep house cleaning service with experienced cleaners."
                );
                _context.ServiceTypes.Add(serviceType);

                // 5. Create Client entity
                var client = Client.Create(
                    name: FullName.Create("John", "Doe"),
                    address: Address.Create("123 Lagos Way, Lagos, Nigeria"),
                    email: Email.Create(clientEmailStr),
                    location: Location.Create(6.5244, 3.3792), // Lagos (Nigeria) coordinates
                    gender: Gender.Male,
                    contact: PhoneNumber.Create("08012345678"),
                    state: "Lagos",
                    dob: DateTime.Today.AddYears(-25),
                    referralCode: $"REF_{Guid.NewGuid().ToString().Substring(0, 5).ToUpper()}"
                );
                client.Id = clientUser.Id; // map client entity to user id
                _context.Clients.Add(client);

                // 6. Create Worker entity
                var worker = Worker.Create(
                    name: FullName.Create("Jane", "Smith"),
                    address: Address.Create("456 Ikeja Road, Lagos, Nigeria"),
                    contact: PhoneNumber.Create("08187654321"),
                    location: Location.Create(6.5244, 3.3792),
                    gender: Gender.Female,
                    serviceTypeId: serviceType.Id,
                    email: Email.Create(workerEmailStr),
                    state: "Lagos",
                    dob: DateTime.Today.AddYears(-24),
                    hourlyRate: 2000.00m
                );
                worker.Id = workerUser.Id; // map worker entity to user id
                _context.Workers.Add(worker);

                // 7. Save Client, Worker, ServiceType changes to DB
                await _context.SaveChangesAsync(cancellationToken);

                var hourlyRate = 2000.00m;
                var startTime = DateTime.Today.AddDays(2).AddHours(9);
                var endTime = DateTime.Today.AddDays(2).AddHours(12);
                var totalAmount = Math.Round(hourlyRate * (decimal)(endTime - startTime).TotalHours, 2);

                // 8. Create Booking in AcceptedAwaitingPayment state
                var booking = Booking.Create(
                    clientId: client.Id,
                    workerId: worker.Id,
                    location: Location.Create(6.5244, 3.3792),
                    dateOfService: DateTime.Today.AddDays(2),
                    dateOfBooking: DateTime.UtcNow,
                    startTime: startTime,
                    endTime: endTime,
                    hourlyRate: hourlyRate,
                    totalAmount: totalAmount,
                    amount: totalAmount,
                    originalAmount: totalAmount,
                    timeRange: TimeRange.Morning,
                    address: Address.Create("123 Lagos Way, Lagos, Nigeria"),
                    bookingStatus: BookingStatus.AcceptedAwaitingPayment,
                    paymentStatus: PaymentStatus.Pending,
                    serviceTypeId: serviceType.Id
                );
                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync(cancellationToken);

                // 9. Call PayForAcceptedBookingCommand to initialize payment
                var payCommand = new PayForAcceptedBookingCommand(
                    BookingId: booking.Id,
                    ClientId: client.Id,
                    Email: clientEmailStr,
                    PaymentMethod: "Card"
                );

                var paymentResponse = await _mediator.Send(payCommand, cancellationToken);

                return Ok(new
                {
                    message = "Seed and payment initialization successful",
                    clientId = client.Id,
                    workerId = worker.Id,
                    bookingId = booking.Id,
                    amount = totalAmount,
                    paymentResponse = paymentResponse
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error during seed and pay test", error = ex.Message, details = ex.ToString() });
            }
        }
    }
}
