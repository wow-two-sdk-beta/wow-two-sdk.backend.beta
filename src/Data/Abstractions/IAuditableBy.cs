namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>Defines a user-attributed change-audited entity.</summary>
/// <typeparam name="TUserId">The user-id type.</typeparam>
public interface IAuditableBy<TUserId> : ICreationAuditableBy<TUserId>, IModificationAuditableBy<TUserId> where TUserId : struct;
