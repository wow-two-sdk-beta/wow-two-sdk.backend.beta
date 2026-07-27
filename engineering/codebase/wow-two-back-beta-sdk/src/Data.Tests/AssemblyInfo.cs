using Xunit;

// Determinism hazard D5 — this suite touches process-global state (Dapper's DefaultTypeMap.MatchNamesWithUnderscores
// and SqlNaming's static casing) and shares one Postgres container across every class. xUnit parallelises across
// collections, which turns that into a race rather than a mere order dependency, so the whole assembly runs serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
