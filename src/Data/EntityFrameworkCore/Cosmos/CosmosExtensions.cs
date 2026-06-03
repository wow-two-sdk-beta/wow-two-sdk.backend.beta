using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Cosmos;

/// <summary>
/// SDK-conventional Cosmos DB provider helpers.
/// </summary>
public static class CosmosExtensions
{
    /// <summary>
    /// Configures Cosmos DB with SDK defaults.
    /// </summary>
    public static DbContextOptionsBuilder UseCosmosConventional(
        this DbContextOptionsBuilder builder,
        string connectionString,
        string databaseName,
        Action<CosmosDbContextOptionsBuilder>? extra = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        return builder.UseCosmos(connectionString, databaseName, cosmos =>
        {
            extra?.Invoke(cosmos);
        });
    }

    /// <summary>
    /// Configures Cosmos DB using account endpoint + token credential.
    /// </summary>
    public static DbContextOptionsBuilder UseCosmosConventional(
        this DbContextOptionsBuilder builder,
        string accountEndpoint,
        string accountKey,
        string databaseName,
        Action<CosmosDbContextOptionsBuilder>? extra = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountEndpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        return builder.UseCosmos(accountEndpoint, accountKey, databaseName, cosmos =>
        {
            extra?.Invoke(cosmos);
        });
    }
}
