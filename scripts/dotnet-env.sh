FIBERROUTE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export DOTNET_ROOT="$FIBERROUTE_ROOT/.cache/dotnet"
export DOTNET_CLI_HOME="$FIBERROUTE_ROOT/.cache/dotnet-home"
export NUGET_PACKAGES="$FIBERROUTE_ROOT/.cache/nuget/packages"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export PATH="$DOTNET_ROOT:$PATH"
export XDG_RUNTIME_DIR="$FIBERROUTE_ROOT/.cache/dotnet-runtime"
mkdir -p "$XDG_RUNTIME_DIR"
chmod 700 "$XDG_RUNTIME_DIR"
