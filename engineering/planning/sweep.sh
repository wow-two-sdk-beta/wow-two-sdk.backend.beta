#!/usr/bin/env bash
# BE SDK conventions sweep — mechanical checks against conventions/development/backend/dotnet/
SCRIPT_DIR=$(cd "$(dirname "$0")" && pwd) || exit 1
SRC="$SCRIPT_DIR/../codebase/wow-two-back-beta-sdk/src"
cd "$SRC" || exit 1
T=$(mktemp)
grep -rhoE '^\s*(public|internal)\s+(sealed\s+|abstract\s+|static\s+|partial\s+|readonly\s+|ref\s+|record\s+)*(class|interface|record|struct|enum)\s+[A-Za-z0-9_]+' --include='*.cs' --exclude-dir=bin --exclude-dir=obj . \
  | grep -oE '[A-Za-z0-9_]+$' | sort -u > "$T"
# enums carry no role, so the fold and banned tables never reach them (constructs.md § Folds, enums.md § Type name) —
# checks 1 and 2 run over $R = every declared type minus the enums
E=$(mktemp); R=$(mktemp)
grep -rhoE '^\s*(public|internal)\s+enum\s+[A-Za-z0-9_]+' --include='*.cs' --exclude-dir=bin --exclude-dir=obj . | grep -oE '[A-Za-z0-9_]+$' | sort -u > "$E"
comm -23 "$T" "$E" > "$R"

echo "types declared: $(wc -l < "$T" | tr -d ' ')  (enums excluded from 1-2: $(wc -l < "$E" | tr -d ' '))"
echo
echo "## 1 folds  (constructs.md § Folds)"
for s in Store Gateway Provider Node Encryptor Mapping Profile Normalizer Map Resolver Emitter Source Observer Scheduler HostedService Keeper; do
  h=$(grep -E "${s}s?$" "$R"); n=$(printf '%s' "$h" | grep -c . )
  [ "$n" -gt 0 ] && printf '  %-14s %2d  %s\n' "$s" "$n" "$(echo $h)"
done
echo
echo "## 2 banned  (constructs.md § Banned)"
for s in Manager Helper Util Utils Accessor Engine Strategy; do
  h=$(grep -E "${s}s?$" "$R"); n=$(printf '%s' "$h" | grep -c .)
  [ "$n" -gt 0 ] && printf '  %-14s %2d  %s\n' "$s" "$n" "$(echo $h)"
done
echo
echo "## 3 static form  (constructs.md § Static or instance — review Factory exceptions)"
# internal counts too, and three carve-outs never appear here:
#   *Factory passing both gates (constructs.md:210) · a non-generic companion of a same-named generic type
#   (constructs.md § The non-generic companion) · types whose name already ends in an allowed form.
GENERIC=$(grep -rhoE 'class [A-Za-z0-9_]+<' --include='*.cs' --exclude-dir=bin --exclude-dir=obj . | sed -E 's/class ([A-Za-z0-9_]+)</\1/' | sort -u)
grep -rhoE '(public|internal) static (partial )?class [A-Za-z0-9_]+' --include='*.cs' --exclude-dir=bin --exclude-dir=obj . | awk '{print $NF}' | sort -u \
  | grep -vE '(Constants|Extensions|Mapper)$' \
  | while read -r t; do printf '%s\n' "$GENERIC" | grep -qx "$t" || echo "  $t"; done
echo
echo "## 4 bare IEntity on a concrete type  (entity-contracts.md:24)"
grep -rlE ':\s*.*\bIEntity\b' --include='*.cs' --exclude-dir=bin --exclude-dir=obj . | while read -r f; do
  grep -qE '(class|record)\s+[A-Za-z0-9_<>,\s]+:\s*[^{]*\bIEntity\b' "$f" && echo "  ${f#./}"
done
echo
echo "## 5 <para> in doc comments  (remarks.md)"
printf '  files: %s   occurrences: %s\n' \
  "$(grep -rlE '<para>' --include='*.cs' --exclude-dir=bin --exclude-dir=obj . | wc -l | tr -d ' ')" \
  "$(grep -rhoE '<para>' --include='*.cs' --exclude-dir=bin --exclude-dir=obj . | wc -l | tr -d ' ')"
echo
echo "## 6 severity glyphs in doc comments  (remarks.md)"
grep -rnE '^\s*///.*(⚠|❗|NOTE:|WARNING:|IMPORTANT:)' --include='*.cs' --exclude-dir=bin --exclude-dir=obj . | sed 's/^\.\///' | head -20
printf '  total: %s\n' "$(grep -rhcE '^\s*///.*(⚠|❗|NOTE:|WARNING:|IMPORTANT:)' --include='*.cs' --exclude-dir=bin --exclude-dir=obj . | paste -sd+ - | bc)"
echo
echo "## 7 Options bound from configuration  (constructs.md § Settings vs Options)"
grep -rhoE 'Configure<[A-Za-z0-9_]+Options>' --include='*.cs' --exclude-dir=bin --exclude-dir=obj . | sort -u | sed 's/^/  /'
echo
echo "## 8 Mapper types not returning Result  (informational only — R3/N11 refuted by N69: the failure mode decides, not the role)"
grep -rlE '(class|interface|record)\s+I?[A-Za-z0-9_]*Mapper' --include='*.cs' --exclude-dir=bin --exclude-dir=obj . | sed 's/^/  /'
echo
echo "## 9 Default-prefixed types  (constructs.md § Role and shape — a lone implementation takes the bare role name; each listed type needs a shipped sibling)"
grep -rhoE '(class|record) Default[A-Za-z0-9_]+' --include='*.cs' --exclude-dir=bin --exclude-dir=obj . | awk '{print $2}' | sort -u | sed 's/^/  /'
echo
echo "## 10 positional records  (lla constructs.md — a data carrier is a sealed record with body properties, never positional)"
grep -rnoE '(public|internal) (sealed |readonly )*record (struct )?[A-Za-z0-9_]+(<[^>]+>)?\(' --include='*.cs' --exclude-dir=bin --exclude-dir=obj . | sed -E 's/:(public|internal) (sealed |readonly )*record (struct )?/  /; s/\($//' | sort | sed 's/^\.\///' | sed 's/^/  /'
rm -f "$T" "$E" "$R"
