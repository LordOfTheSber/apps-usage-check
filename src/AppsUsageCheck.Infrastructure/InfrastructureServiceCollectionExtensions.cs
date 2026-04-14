using AppsUsageCheck.Core.Interfaces;
using AppsUsageCheck.Infrastructure.Data;
using AppsUsageCheck.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppsUsageCheck.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
        }

        services.AddDbContextFactory<AppDbContext>(
            options =>
            {
                options.UseNpgsql(
                    connectionString,
                    npgsqlOptions => npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
                options.UseSnakeCaseNamingConvention();
            });

        services.AddSingleton<IUsageRepository, UsageRepository>();
        services.AddSingleton<IProcessDetector, Win32ProcessDetector>();
        services.AddSingleton<IForegroundDetector, Win32ForegroundDetector>();

        return services;
    }
}
