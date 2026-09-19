#!/usr/bin/env bash
# NuGet propagation has one family-wide deadline; confirmed packages leave the poll.
set -euo pipefail

VERSION=${1:?usage: verify-published-packages.sh VERSION [TIMEOUT_SECONDS] [POLL_SECONDS]}
TIMEOUT_SECONDS=${2:-900}
POLL_SECONDS=${3:-15}
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+-beta$ ]] ||
   [[ ! "$TIMEOUT_SECONDS" =~ ^[1-9][0-9]*$ ]] ||
   [[ ! "$POLL_SECONDS" =~ ^[1-9][0-9]*$ ]]; then
  echo 'Expected a beta version and positive integer timeout/poll seconds.' >&2
  exit 2
fi

PENDING=(
  wow2.sdk.backend.beta
  wow2.sdk.backend.beta.data.abstractions
  wow2.sdk.backend.beta.data.migrations.cli
  wow2.sdk.backend.beta.testing
  wow2.sdk.backend.beta.testing.data
  wow2.sdk.backend.beta.testing.integrations
  wow2.sdk.backend.beta.testing.messaging
)
DEADLINE=$((SECONDS + TIMEOUT_SECONDS))

while ((${#PENDING[@]} > 0 && SECONDS < DEADLINE)); do
  MISSING=()
  for ID in "${PENDING[@]}"; do
    REMAINING=$((DEADLINE - SECONDS))
    if ((REMAINING <= 0)); then
      MISSING+=("$ID")
      continue
    fi
    REQUEST_SECONDS=$((REMAINING < 20 ? REMAINING : 20))
    URL="https://api.nuget.org/v3-flatcontainer/$ID/$VERSION/$ID.$VERSION.nupkg"
    CURL_STATUS=0
    HTTP_STATUS=$(curl --silent --show-error --head --output /dev/null \
      --write-out '%{http_code}' --connect-timeout 5 \
      --max-time "$REQUEST_SECONDS" "$URL") || CURL_STATUS=$?
    if ((CURL_STATUS == 0)) && [[ "$HTTP_STATUS" == 200 ]]; then
      printf 'Available: %s %s\n' "$ID" "$VERSION"
    else
      printf 'Unavailable: %s %s (HTTP %s, curl %s)\n' \
        "$ID" "$VERSION" "$HTTP_STATUS" "$CURL_STATUS"
      MISSING+=("$ID")
    fi
  done

  # Bash 3 treats an empty array expansion as unset with nounset enabled.
  if ((${#MISSING[@]} == 0)); then
    printf 'Verified all seven published packages at %s.\n' "$VERSION"
    exit 0
  fi
  PENDING=("${MISSING[@]}")
  printf 'Pending packages: %s\n' "${PENDING[*]}"
  REMAINING=$((DEADLINE - SECONDS))
  if ((REMAINING > 0)); then
    sleep "$((REMAINING < POLL_SECONDS ? REMAINING : POLL_SECONDS))"
  fi
done

printf 'Publication verification timed out after %ss for %s.\n' "$TIMEOUT_SECONDS" "$VERSION" >&2
printf 'Unverified: %s\n' "${PENDING[@]}" >&2
echo 'Packages may already be published; rerun this verifier before triggering another release.' >&2
exit 1
