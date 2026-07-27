# EntityFrameworkCore — Naming

Model-wide naming conventions for EF Core, applied from `OnModelCreating`.

- `ApplyEnumStringConversions(this ModelBuilder, CaseStyle = Snake)` — applies `EnumCaseConverter<T>` to every enum property in the model (including nullable enums). The bulk form of the per-property `HasEnumStringConversion()`; one call replaces a hand-rolled per-enum loop.
