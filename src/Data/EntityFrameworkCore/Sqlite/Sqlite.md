# EntityFrameworkCore — Sqlite

Provider-portability helpers for SQLite-backed tests (production runs Postgres).

- `ApplyDateTimeOffsetToBinaryConversion(this ModelBuilder)` — applies `DateTimeOffsetToBinaryConverter` to all `DateTimeOffset` / `DateTimeOffset?` properties so SQLite ordering and precision match Postgres. Only needed under SQLite — gate the call on `Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"` (Npgsql maps `DateTimeOffset` natively).
