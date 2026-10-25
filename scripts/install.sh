#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
. scripts/dotnet-env.sh
if ! test -x "$DOTNET_ROOT/dotnet"; then
  archive=$(mktemp /tmp/fiber-sdk.XXXXXX.tar.gz)
  trap 'rm -f "$archive"' EXIT
  curl --fail --location --show-error --silent https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.426/dotnet-sdk-8.0.426-linux-x64.tar.gz -o "$archive"
  printf '%s  %s\n' '8141d01bdc53f604aed3bb661b604707a64209f8a947cd81bf95b4f518951593d77864069191f54ea5e84362a409da6b0f69074bf893b283d7e0cf93e2bf20a8' "$archive" | sha512sum -c -
  mkdir -p "$DOTNET_ROOT"
  tar -xzf "$archive" -C "$DOTNET_ROOT"
fi
dotnet restore FiberRouteR1.sln --locked-mode
dotnet build FiberRouteR1.sln -c Release --no-restore
dotnet run --project tests/FiberRoute.Tests -c Release --no-build
