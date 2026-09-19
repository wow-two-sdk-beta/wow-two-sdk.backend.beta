namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Describes how a migration dialect coordinates simultaneous applicants.</summary>
public enum MigrationCoordinationMode
{
    /// <summary>The database serializes applicants for the complete migration loop.</summary>
    DatabaseLock,

    /// <summary>The deployment must ensure that only one applicant runs.</summary>
    SingleApplicantRequired,
}
