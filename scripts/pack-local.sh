#!/usr/bin/env bash
# Pack Strata libraries into ./local-feed as NuGet packages for local consumption
# by sibling repos (e.g. ../ps-bash). Not for publishing — see release process for that.
#
# Usage: scripts/pack-local.sh [version]
#   version  package version to stamp (default: 0.1.0-dev)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${1:-0.1.0-dev}"
FEED="$ROOT/local-feed"

echo "Packing Strata $VERSION -> $FEED"
mkdir -p "$FEED"

# Pack every IsPackable project in the solution. -c Release for parity with consumers.
dotnet pack "$ROOT/Strata.sln" \
  -c Release \
  -p:Version="$VERSION" \
  -p:ContinuousIntegrationBuild=true \
  -o "$FEED"

echo "Done. Packages in $FEED:"
ls -1 "$FEED"/*.nupkg
