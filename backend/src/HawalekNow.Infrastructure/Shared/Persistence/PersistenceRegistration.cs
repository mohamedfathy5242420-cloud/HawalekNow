using HawalekNow.Application.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace HawalekNow.Infrastructure.Shared.Persistence;

public static class PersistenceRegistration
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<HawalekDbContext>(options => options.UseSqlServer(
            configuration.GetConnectionString("Application") ?? throw new InvalidOperationException("Configure the application database."),
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "operations")));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<HawalekDbContext>());
        return services;
    }

    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddDbContext<HawalekDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "operations")));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<HawalekDbContext>());
        return services;
    }
}
