#!/usr/bin/env bash
set -euo pipefail
repo=$(cd "$(dirname "$0")/.." && pwd)
prefix=${FIBER_MINGW_ROOT:-/tmp/fiber-mingw/root/usr}
compiler="$prefix/bin/x86_64-w64-mingw32-gcc-posix"
export PATH="$prefix/bin:$PATH"
gccdir=$(dirname -- "$("$compiler" -print-file-name=cc1)")
mkdir -p "$repo/artifacts/native-r141"
icon=${1:-"$repo/src/FiberRoute.App/Brand/app.ico"}
# Use a Windows-native resource for proportional multi-size app icons.
resource="$repo/artifacts/native-r141/brand.rc"
python3 - "$icon" "$resource" <<'PY'
import sys
from pathlib import Path
Path(sys.argv[2]).write_text('1 ICON "'+str(Path(sys.argv[1]).resolve()).replace('\\','/')+'"\n')
PY
"$prefix/bin/x86_64-w64-mingw32-windres" --preprocessor "$compiler" --preprocessor-arg=-E --preprocessor-arg=-xc --preprocessor-arg=-DRC_INVOKED -I "$prefix/x86_64-w64-mingw32/include" "$resource" "$repo/artifacts/native-r141/brand.o"
for kind in app publisher brand; do
  flags=()
  if [ "$kind" = publisher ]; then flags=(-DPUBLISHER); fi
  if [ "$kind" = brand ]; then flags=(-DBRAND); fi
  "$compiler" -B "$gccdir/" -B "$prefix/bin/" -isystem "$prefix/x86_64-w64-mingw32/include" -L "$prefix/x86_64-w64-mingw32/lib" -nostdlib -fno-builtin -fno-stack-protector -Os -s -Wl,--entry,FiberEntry -Wl,--subsystem,windows "${flags[@]}" "$repo/src/FiberRoute.NativeLauncher/launcher.c" "$repo/artifacts/native-r141/brand.o" -lkernel32 -luser32 -o "$repo/artifacts/native-r141/$kind.exe"
done
