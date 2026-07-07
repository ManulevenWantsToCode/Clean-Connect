using AspNetCoreHero.ToastNotification;
using Clean_Connect.Application.AssemblyMarker;
using Clean_Connect.Application.Behaviours;
using Clean_Connect.Application.Command.Services;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Infrastructure.Context;
using Clean_Connect.Persistence.Repositories;
using FluentValidation;
using MediatR;
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
builder.Services.AddSqlServer<ApplicationDbContext>(
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
// Authentication - JWT
// --------------------
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
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

// --------------------
// Authorization
// --------------------
builder.Services.AddAuthorization();
// Add services to the container.
builder.Services.AddControllersWithViews();


// --------------------
// MediatR
// --------------------
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(ApplicationAssemblyMarker).Assembly));

// --------------------
// DI for Services
// --------------------
builder.Services.AddHttpClient<GeocodingService>();
builder.Services.AddScoped<WorkerAvailabilityService>();
builder.Services.AddScoped<BookingRuleService>();
builder.Services.AddScoped<AcceptBookingService>();
builder.Services.AddScoped<MarkAsCompletedService>();
builder.Services.AddScoped<EscrowService>();
builder.Services.AddScoped<PayoutService>();
builder.Services.AddScoped<WalletService>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddHttpClient<IPaystackService, PaystackService>();


// --------------------
// DI for repositories & UnitOfWork
// --------------------
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IWorkerRepository, WorkerRepository>();
builder.Services.AddScoped<IServiceTypeRepository, ServiceTypeRepository>();
builder.Services.AddScoped<IRatingRepository, RatingRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IWalletRepository, WalletRepository>();
builder.Services.AddScoped<IEscrowRepository, EscrowRepository>();
builder.Services.AddScoped<ICouponRepository, CouponRepository>();
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
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
