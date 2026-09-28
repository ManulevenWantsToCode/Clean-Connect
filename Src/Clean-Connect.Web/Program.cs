using AspNetCoreHero.ToastNotification;
using Clean_Connect.Application.AssemblyMarker;
using Clean_Connect.Application.Behaviours;
using Clean_Connect.Application.Command.Services;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Web.Hubs;
using Clean_Connect.Web.Services;
using Clean_Connect.Infrastructure.Context;
using Clean_Connect.Persistence.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);


// --------------------
// Serilog configuration
// --------------------
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// --------------------
// JWT settings
// --------------------
var jwtSettings = builder.Configuration.GetSection("Jwt");

// --------------------
// Add DbContext first
// --------------------
builder.Services.AddNpgsql<ApplicationDbContext>(
    builder.Configuration.GetConnectionString("DefaultConnection"),
    b => b.UseNetTopologySuite()
          .MigrationsAssembly("Clean-Connect.Persistence"));

// --------------------
// Identity Core for API
// --------------------
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();


// --------------------
// Authentication
// --------------------
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Login";
    options.AccessDeniedPath = "/Login";
});

// Keep Identity's application cookie as the default for MVC pages.
// JWT remains available explicitly as the Bearer scheme.
builder.Services.AddAuthentication()
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]))
    };
});

var googleSection = builder.Configuration.GetSection("Authentication:Google");
if (!string.IsNullOrWhiteSpace(googleSection["ClientId"]) && !string.IsNullOrWhiteSpace(googleSection["ClientSecret"]))
{
    builder.Services.AddAuthentication().AddGoogle(options =>
    {
        options.ClientId = googleSection["ClientId"];
        options.ClientSecret = googleSection["ClientSecret"];
        options.SaveTokens = false;
        options.Scope.Add("email");
        options.Scope.Add("profile");
    });
}

// --------------------
// Authorization
// --------------------
builder.Services.AddAuthorization();
// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddSingleton<Clean_Connect.Web.Services.IWorkerPresenceService, Clean_Connect.Web.Services.WorkerPresenceService>();


// --------------------
// MediatR
// --------------------
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(ApplicationAssemblyMarker).Assembly));

// --------------------
// DI for Services
// --------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<GeocodingService>();
builder.Services.AddScoped<WorkerAvailabilityService>();
builder.Services.AddScoped<BookingRuleService>();
builder.Services.AddScoped<AcceptBookingService>();
builder.Services.AddScoped<MarkAsCompletedService>();
builder.Services.AddScoped<EscrowService>();
builder.Services.AddScoped<PayoutService>();
builder.Services.AddScoped<RefundService>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IRealtimeNotificationService, RealtimeNotificationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddHttpClient<IPaystackService, PaystackService>();

// --------------------
// Background services
// --------------------
builder.Services.AddHostedService<BookingExpirationService>();


// --------------------
// DI for repositories & UnitOfWork
// --------------------
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IWorkerRepository, WorkerRepository>();
builder.Services.AddScoped<IServiceTypeRepository, ServiceTypeRepository>();
builder.Services.AddScoped<IRatingRepository, RatingRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IWorkerBankDetailRepository, WorkerBankDetailRepository>();
builder.Services.AddScoped<IEscrowRepository, EscrowRepository>();
builder.Services.AddScoped<ICouponRepository, CouponRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();


// --------------------
// FluentValidation
// --------------------
builder.Services.AddValidatorsFromAssembly(typeof(ApplicationAssemblyMarker).Assembly);

// --------------------
// MediatR Pipeline Behaviors
// --------------------
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehaviour<,>));

builder.Services.AddNotyf(config =>
{
    config.DurationInSeconds = 10;
    config.IsDismissable = true;
    config.Position = NotyfPosition.TopRight;
});





var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<NotificationHub>("/hubs/notifications");

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (!context.ServiceTypes.Any())
    {
        var serviceTypes = new[]
        {
            ServiceType.Create("Residential Cleaning", "Professional cleaning for homes and apartments"),
            ServiceType.Create("Commercial Cleaning", "Office and commercial space cleaning services"),
            ServiceType.Create("Deep Cleaning", "Thorough deep cleaning for all living spaces"),
            ServiceType.Create("Carpet Cleaning", "Professional carpet and upholstery cleaning"),
            ServiceType.Create("Move In/Out Cleaning", "Complete cleaning for move-in and move-out"),
        };

        foreach (var st in serviceTypes)
            context.ServiceTypes.Add(st);

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} service types", serviceTypes.Length);
    }
}

app.Run();
