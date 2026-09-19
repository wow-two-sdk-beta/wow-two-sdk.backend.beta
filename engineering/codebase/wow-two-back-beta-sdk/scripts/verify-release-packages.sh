#!/usr/bin/env bash

set -euo pipefail

artifact_dir=${1:?Pass the artifact directory}
version=${2:?Pass the evaluated package version}
revision=${3:?Pass the release revision}

packages=(
  "WoW2.Sdk.Backend.Beta|lib/net10.0/WoW.Two.Sdk.Backend.Beta.dll"
  "WoW2.Sdk.Backend.Beta.Data.Abstractions|lib/net10.0/WoW.Two.Sdk.Backend.Beta.Data.Abstractions.dll"
  "WoW2.Sdk.Backend.Beta.Data.Migrations.Cli|tools/net10.0/any/wow-migrate.dll"
  "WoW2.Sdk.Backend.Beta.Testing|lib/net10.0/WoW.Two.Sdk.Backend.Beta.Testing.dll"
  "WoW2.Sdk.Backend.Beta.Testing.Data|lib/net10.0/WoW.Two.Sdk.Backend.Beta.Testing.Data.dll"
  "WoW2.Sdk.Backend.Beta.Testing.Integrations|lib/net10.0/WoW.Two.Sdk.Backend.Beta.Testing.Integrations.dll"
  "WoW2.Sdk.Backend.Beta.Testing.Messaging|lib/net10.0/WoW.Two.Sdk.Backend.Beta.Testing.Messaging.dll"
)

shopt -s nullglob
nupkgs=("$artifact_dir"/*.nupkg)
snupkgs=("$artifact_dir"/*.snupkg)

[[ ${#nupkgs[@]} -eq ${#packages[@]} ]]
[[ ${#snupkgs[@]} -eq ${#packages[@]} ]]

for package in "${packages[@]}"; do
  IFS='|' read -r id required_asset <<< "$package"
  nupkg="$artifact_dir/$id.$version.nupkg"
  snupkg="$artifact_dir/$id.$version.snupkg"
  [[ -f "$nupkg" ]]
  [[ -f "$snupkg" ]]

  nuspec=$(unzip -p "$nupkg" '*.nuspec')
  assets=$(unzip -Z1 "$nupkg")
  grep -Fq "<id>$id</id>" <<< "$nuspec"
  grep -Fq "<version>$version</version>" <<< "$nuspec"
  grep -Fq '<license type="expression">MIT</license>' <<< "$nuspec"
  grep -Fq 'repository type="git"' <<< "$nuspec"
  grep -Fq "commit=\"$revision\"" <<< "$nuspec"
  grep -Fxq "$required_asset" <<< "$assets"
done

grep -Fq 'README.md' <<< "$(unzip -Z1 "$artifact_dir/WoW2.Sdk.Backend.Beta.$version.nupkg")"
grep -Fq 'tools/net10.0/any/WoW.Two.Sdk.Backend.Beta.Data.Abstractions.dll' \
  <<< "$(unzip -Z1 "$artifact_dir/WoW2.Sdk.Backend.Beta.Data.Migrations.Cli.$version.nupkg")"

production_ids=(
  "WoW2.Sdk.Backend.Beta"
  "WoW2.Sdk.Backend.Beta.Data.Abstractions"
  "WoW2.Sdk.Backend.Beta.Data.Migrations.Cli"
)
for id in "${production_ids[@]}"; do
  nuspec=$(unzip -p "$artifact_dir/$id.$version.nupkg" '*.nuspec')
  if grep -Eq 'dependency id="(Microsoft.NET.Test.Sdk|xunit|xunit.runner.visualstudio|AwesomeAssertions|Shouldly|Verify\.|WireMock.Net|Testcontainers)' <<< "$nuspec"; then
    echo "$id contains a test-only dependency" >&2
    exit 1
  fi
done

require_family_dependency() {
  local id=$1
  local dependency=$2
  local nuspec
  nuspec=$(unzip -p "$artifact_dir/$id.$version.nupkg" '*.nuspec')
  grep -Fq "dependency id=\"$dependency\" version=\"$version\"" <<< "$nuspec"
}

require_family_dependency "WoW2.Sdk.Backend.Beta" "WoW2.Sdk.Backend.Beta.Data.Abstractions"
require_family_dependency "WoW2.Sdk.Backend.Beta.Testing.Data" "WoW2.Sdk.Backend.Beta.Testing"
require_family_dependency "WoW2.Sdk.Backend.Beta.Testing.Data" "WoW2.Sdk.Backend.Beta"
require_family_dependency "WoW2.Sdk.Backend.Beta.Testing.Integrations" "WoW2.Sdk.Backend.Beta"
require_family_dependency "WoW2.Sdk.Backend.Beta.Testing.Messaging" "WoW2.Sdk.Backend.Beta"

testing_nuspec=$(unzip -p "$artifact_dir/WoW2.Sdk.Backend.Beta.Testing.$version.nupkg" '*.nuspec')
if grep -Fq 'dependency id="WoW2.Sdk.Backend.Beta"' <<< "$testing_nuspec"; then
  echo "The base Testing package must remain independent of the production mono library" >&2
  exit 1
fi

manifest="$artifact_dir/release-manifest.txt"
{
  echo "revision=$revision"
  echo "version=$version"
  for package in "${packages[@]}"; do
    IFS='|' read -r id _ <<< "$package"
    echo "package=$id.$version.nupkg"
    echo "symbols=$id.$version.snupkg"
  done
} > "$manifest"
