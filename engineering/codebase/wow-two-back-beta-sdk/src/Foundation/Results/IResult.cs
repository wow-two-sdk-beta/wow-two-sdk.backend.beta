namespace WoW.Two.Sdk.Backend.Beta.Foundation.Results;

/// <summary>Defines the success discriminant shared by SDK result carriers.</summary>
public interface IResult
{
    /// <summary>Gets whether the operation succeeded.</summary>
    bool IsSuccess { get; }
}
