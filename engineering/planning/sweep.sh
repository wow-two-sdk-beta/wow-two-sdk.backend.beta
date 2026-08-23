#!/usr/bin/env bash
# BE SDK conventions sweep — mechanical checks against conventions/development/backend/dotnet/
cd "$(dirname "$0")" 2>/dev/null
SRC=/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/workbench/wow-two-sdk-beta/wow-two-sdk.backend.beta/engineering/codebase/wow-two-back-beta-sdk/src
cd "$SRC" || exit 1
T=$(mktemp)
grep -rhoE '^\s*(public|internal)\s+(sealed\s+|abstract\s+|static\s+|partial\s+|readonly\s+|ref\s+|record\s+)*(class|interface|record|struct|enum)\s+[A-Za-z0-9_]+' --include='*.cs' . \
  | grep -oE '[A-Za-z0-9_]+$' | sort -u > "$T"

echo "types declared: $(wc -l < "$T" | tr -d ' ')"
echo
echo "## 1 folds  (constructs.md § Folds)"
for s in Store Gateway Provider Node Encryptor Mapping Profile Normalizer Map Resolver Emitter Source Observer Scheduler HostedService Keeper; do
  h=$(grep -E "${s}s?$" "$T"); n=$(printf '%s' "$h" | grep -c . )
  [ "$n" -gt 0 ] && printf '  %-14s %2d  %s\n' "$s" "$n" "$(echo $h)"
done
echo
echo "## 2 banned  (constructs.md § Banned)"
for s in Manager Helper Util Utils Accessor Engine Strategy; do
  h=$(grep -E "${s}s?$" "$T"); n=$(printf '%s' "$h" | grep -c .)
  [ "$n" -gt 0 ] && printf '  %-14s %2d  %s\n' "$s" "$n" "$(echo $h)"
done
echo
echo "## 3 static form  (constructs.md:145 — Constants | Extensions | Mapper only)"
grep -rhoE 'public static class [A-Za-z0-9_]+' --include='*.cs' . | awk '{print $4}' | sort -u \
  | grep -vE '(Constants|Extensions|Mapper)$' | sed 's/^/  /'
echo
echo "## 4 bare IEntity on a concrete type  (entity-contracts.md:24)"
grep -rlE ':\s*.*\bIEntity\b' --include='*.cs' . | while read -r f; do
  grep -qE '(class|record)\s+[A-Za-z0-9_<>,\s]+:\s*[^{]*\bIEntity\b' "$f" && echo "  ${f#./}"
done
echo
echo "## 5 <para> in doc comments  (remarks.md)"
printf '  files: %s   occurrences: %s\n' \
  "$(grep -rlE '<para>' --include='*.cs' . | wc -l | tr -d ' ')" \
  "$(grep -rhoE '<para>' --include='*.cs' . | wc -l | tr -d ' ')"
echo
echo "## 6 severity glyphs in doc comments  (remarks.md)"
grep -rnE '^\s*///.*(⚠|❗|NOTE:|WARNING:|IMPORTANT:)' --include='*.cs' . | sed 's/^\.\///' | head -20
printf '  total: %s\n' "$(grep -rhcE '^\s*///.*(⚠|❗|NOTE:|WARNING:|IMPORTANT:)' --include='*.cs' . | paste -sd+ - | bc)"
echo
echo "## 7 Options bound from configuration  (constructs.md § Settings vs Options)"
grep -rhoE 'Configure<[A-Za-z0-9_]+Options>' --include='*.cs' . | sort -u | sed 's/^/  /'
echo
echo "## 8 Mapper types not returning Result  (informational only — R3/N11 refuted by N69: the failure mode decides, not the role)"
grep -rlE '(class|interface|record)\s+I?[A-Za-z0-9_]*Mapper' --include='*.cs' . | sed 's/^/  /'
rm -f "$T"
