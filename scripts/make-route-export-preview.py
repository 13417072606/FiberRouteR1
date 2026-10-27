"""Generate the Excel sample with the same writer used by the application."""
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
if len(sys.argv) != 3:
    raise SystemExit("usage: make-route-export-preview.py input.csv output.xlsx")
subprocess.run([
    "dotnet", "run", "--project", str(root / "tests/FiberRoute.Tests"),
    "-c", "Release", "--no-restore", "--", "--export-preview",
    str(Path(sys.argv[1]).resolve()), str(Path(sys.argv[2]).resolve()),
], cwd=root, check=True)
