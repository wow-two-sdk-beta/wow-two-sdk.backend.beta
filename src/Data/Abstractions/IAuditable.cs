namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>Defines a change-audited entity.</summary>
public interface IAuditable : ICreationAuditable, IModificationAuditable;
