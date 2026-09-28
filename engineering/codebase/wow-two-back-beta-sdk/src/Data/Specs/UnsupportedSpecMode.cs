namespace WoW.Two.Sdk.Backend.Beta.Data.Specs;

/// <summary>Refers to what a mapper does with a spec feature its backend cannot express.</summary>
public enum UnsupportedSpecMode
{
    /// <summary>Fail while building the mapping, naming every unsupported feature.</summary>
    Throw,

    /// <summary>Leave the feature out and report it to the mapper's callback or log.</summary>
    Skip,
}
