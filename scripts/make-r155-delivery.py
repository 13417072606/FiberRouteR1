"""Build the two R1.5.5 Windows deliverables from published runtimes."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile, ZipInfo

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "downloads/R1.5.5"
OUT.mkdir(parents=True, exist_ok=True)
STAMP = (2026, 11, 3, 0, 0, 0)


def sha(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def runtime(directory: str) -> dict[str, Path]:
    base = ROOT / "artifacts" / directory
    assert base.is_dir(), base
    return {
        "程序/" + path.relative_to(base).as_posix(): path
        for path in base.rglob("*")
        if path.is_file() and path.suffix.lower() != ".pdb" and path.name != "createdump.exe"
    }


def add_bytes(archive: ZipFile, name: str, payload: bytes) -> None:
    entry = ZipInfo(name, STAMP)
    entry.compress_type = ZIP_DEFLATED
    archive.writestr(entry, payload, compress_type=ZIP_DEFLATED, compresslevel=6)


def package(folder: str, archive_name: str, files: dict[str, Path], empty: tuple[str, ...] = ()) -> Path:
    forbidden = ("使用说明.txt", ".db", ".pem", ".xlsx", ".pdb")
    assert not any(name.endswith(forbidden) for name in files)
    checks = {name: sha(path) for name, path in sorted(files.items())}
    target = OUT / archive_name
    with ZipFile(target, "w", compression=ZIP_DEFLATED, compresslevel=6, allowZip64=True) as archive:
        for name, path in sorted(files.items()):
            archive.write(path, folder + "/" + name)
        for name in empty:
            add_bytes(archive, folder + "/" + name, b"")
        add_bytes(archive, folder + "/程序/补丁校验.json", json.dumps(checks, ensure_ascii=False, indent=2).encode())
    with ZipFile(target) as archive:
        assert archive.testzip() is None
        assert not any(name.endswith("使用说明.txt") for name in archive.namelist())
    print(target.name, target.stat().st_size, sha(target))
    return target


client = runtime("app-r155")
for name in ("FiberRoute.Updater.exe", "FiberRoute.Updater.dll", "FiberRoute.Updater.deps.json", "FiberRoute.Updater.runtimeconfig.json"):
    client["程序/" + name] = ROOT / "artifacts/updater-r155" / name
client.update({
    "光路调度路由分析.exe": ROOT / "artifacts/launcher-r155/光路调度路由分析.exe",
    "资源/品牌原图.png": ROOT / "src/FiberRoute.App/Brand/brand.png",
    "资源/update-service.json": ROOT / "assets/update-service.json",
})
assert sha(client["资源/品牌原图.png"]).lower() == "0c8b107d75834a019da79a9bc3b0d02b61ec70fa49c185c2ed9f7e13e2cf89d7"
assert json.loads(client["资源/update-service.json"].read_text())["ManifestUrl"] == "https://guangludiaodu.pages.dev/manifest.json"
package("光路调度路由分析R1.5.5", "光路调度路由分析R1.5.5.zip", client, ("数据/",))

publisher = runtime("publisher-r155")
publisher.update({
    "光路调度更新工具.exe": ROOT / "artifacts/publisher-launcher-r155/光路调度更新工具.exe",
    "资源/品牌原图.png": ROOT / "src/FiberRoute.App/Brand/brand.png",
    "资源/update-service.json": ROOT / "assets/update-service.json",
})
package("光路调度更新工具R1.5.5", "光路调度更新工具R1.5.5.zip", publisher)
