namespace DocMind.Core.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddDocMindData(this IServiceCollection services, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string must not be null or empty.", nameof(connectionString));
        }

        return services.AddDbContext<DocMindDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));
    }
}