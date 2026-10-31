"""Split the already published, hash-checked R1.5.1 ZIPs for unreliable links."""
from hashlib import sha256
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
source = root / 'artifacts/r151-release-original'
destination = root / 'downloads/R1.5.1/split'
destination.mkdir(parents=True, exist_ok=True)
chunk_size = 16 * 1024 * 1024
expected = {
    'FiberRouteR1-Publisher-R1.5.1.zip': 'e0ac7df3c9cad77124599c90774fdffd98af75c7291fe6d967f0d4528223a2b9',
    'FiberRouteR1-R1.5.1-program-source.zip': '1ffbd345c855c544e23bbdec477fb0bcfaecbfbaba6722ea951c96950cad8f92',
}
files = []
for name, digest in expected.items():
    path = source / name
    assert sha256(path.read_bytes()).hexdigest() == digest, f'Published asset changed: {name}'
    parts = []
    with path.open('rb') as stream:
        index = 0
        while block := stream.read(chunk_size):
            index += 1
            part_name = f'{name}.part{index:03d}'
            (destination / part_name).write_bytes(block)
            parts.append({'name': part_name, 'size': len(block), 'sha256': sha256(block).hexdigest()})
    files.append({'name': name, 'size': path.stat().st_size, 'sha256': digest, 'parts': parts})
manifest = destination / 'R1.5.1-download-parts.json'
manifest.write_text(json.dumps({'files': files}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('split assets:', len(files), 'ZIPs,', sum(len(item['parts']) for item in files), 'parts')
