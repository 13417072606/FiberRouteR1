#!/usr/bin/env bash
set -euo pipefail
cd /workspace/FiberRouteR1
. scripts/dotnet-env.sh
mkdir -p artifacts/validation
dotnet run --project tests/FiberRoute.Tests -c Release > artifacts/validation/tests.txt
# Each publish output is separate; combine the two self-contained applications only after success.
dotnet publish src/FiberRoute.App -c Release -r win-x64 --self-contained true -o artifacts/app -p:RestoreLockedMode=true -p:DebugType=None -p:DebugSymbols=false
dotnet publish src/FiberRoute.Updater -c Release -r win-x64 --self-contained true -o artifacts/updater -p:RestoreLockedMode=true -p:DebugType=None -p:DebugSymbols=false
dotnet publish src/FiberRoute.Launcher -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:RestoreLockedMode=true -p:DebugType=None -p:DebugSymbols=false -o artifacts/launcher
python3 scripts/package.py
dotnet run --project tests/FiberRoute.Tests -c Release -- --validate-package artifacts/FiberRouteR1-win-x64-R1.zip
