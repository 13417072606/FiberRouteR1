"""Offer smaller replacements for the two remaining R1.5.1 software-source parts."""
from hashlib import sha256
from pathlib import Path

root = Path(__file__).resolve().parents[1]
name = 'FiberRouteR1-R1.5.1-program-source.zip'
original = root / 'artifacts/r151-release-original' / name
assert sha256(original.read_bytes()).hexdigest() == '1ffbd345c855c544e23bbdec477fb0bcfaecbfbaba6722ea951c96950cad8f92'
destination = root / 'downloads/R1.5.1/small-remainder'
destination.mkdir(parents=True, exist_ok=True)
part_size = 16 * 1024 * 1024
slice_size = 4 * 1024 * 1024
part_hashes = {
    4: 'a8d62b61b417c5f657a97c36370d060f5a82bf91d2c78bec8d2ddde73c4bb3b5',
    5: '5a95b1e0f31d99bae19b79de66dc96ac8f84b8b7146a23aaeffc3f4331824996',
}
with original.open('rb') as stream:
    for part_number, digest in part_hashes.items():
        stream.seek((part_number - 1) * part_size)
        part = stream.read(part_size)
        assert sha256(part).hexdigest() == digest
        for offset in range(0, len(part), slice_size):
            slice_number = offset // slice_size + 1
            path = destination / f'{name}.part{part_number:03d}.slice{slice_number:03d}'
            data = part[offset:offset + slice_size]
            path.write_bytes(data)
            print(path.name, len(data), sha256(data).hexdigest())
