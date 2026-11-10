using Clean_Connect.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Clean_Connect.Persistence.Context;

/// <summary>Provides EF tooling a context without booting either web host or requiring application secrets.</summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Database=clean_connect_design_time;Username=design_time;Timeout=1";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString, provider => provider
                .UseNetTopologySuite()
                .MigrationsAssembly("Clean-Connect.Persistence"))
            .Options;

        return new ApplicationDbContext(options);
    }
}
