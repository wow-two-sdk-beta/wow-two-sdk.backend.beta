namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Defines the contract for classifying an <see cref="AppErrorType"/> into its <see cref="ErrorNature"/>.</summary>
public interface IErrorNatureClassifier
{
    /// <summary>Classifies <paramref name="type"/> into its descriptive nature.</summary>
    /// <param name="type">The failure kind.</param>
    ErrorNature Classify(AppErrorType type);
}
