#!/usr/bin/env bash
# Pack Strata libraries into ./local-feed as NuGet packages for local consumption
# by sibling repos (e.g. ../ps-bash). Not for publishing — see release process for that.
#
# Usage: scripts/pack-local.sh
#
# Versioning: MinVer derives the package version from this repo's git tags/height,
# so there is NO version argument — whatever MinVer computes is what the feed gets.
# Consumers auto-detect that version from the feed (ps-bash reads it off the packed
# .nupkg filename), so a bump here needs no edit on their side.
#
# The feed is wiped before packing so it holds exactly ONE version of each package.
# That keeps consumer auto-detection deterministic (no stale older .nupkg lingering
# for a "newest" picker to get wrong).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FEED="$ROOT/local-feed"

echo "Cleaning $FEED"
mkdir -p "$FEED"
rm -f "$FEED"/*.nupkg "$FEED"/*.snupkg

echo "Packing Strata (MinVer-stamped) -> $FEED"

# Pack every IsPackable project in the solution. -c Release for parity with consumers.
dotnet pack "$ROOT/Strata.sln" \
  -c Release \
  -p:ContinuousIntegrationBuild=true \
  -o "$FEED"

echo "Done. Packages in $FEED:"
ls -1 "$FEED"/*.nupkg
