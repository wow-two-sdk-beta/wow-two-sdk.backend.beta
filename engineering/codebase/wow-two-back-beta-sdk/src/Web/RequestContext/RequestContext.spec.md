# Request context primitives

- UserAgentDeviceMapper classifies the supplied text using ordered, case-insensitive markers: bot, iOS, Android, desktop, unknown.
  This is a heuristic hint, never authentication or proof of a device. Ambiguous modern tablet/desktop UAs remain ambiguous.
- AcceptLanguageParser parses basic language ranges plus q-values. It retains wildcard and q=0 exclusions and sorts descending
  by quality, preserving input order for ties. It does not choose a supported culture or apply fallback/translation policy.
- Null/blank language input is a valid empty list. Malformed syntax, ranges, excessive size (>8192 chars) or count (>64) fail
  the whole parse through Result. Duplicate ranges are rejected rather than silently discarding an exclusion.
- Language tags are normalized to lowercase. Wildcard negotiation and prefix matching belong to the consumer's policy.

Reference: [HTTP language preferences](https://www.rfc-editor.org/rfc/rfc9110#section-12.5.4).

Register the parser with `services.AddSingleton<IAcceptLanguageParser, AcceptLanguageParser>()` where consumed.
`UserAgentDeviceMapper.Map` is a pure static mapping. Neither primitive changes request culture automatically.
