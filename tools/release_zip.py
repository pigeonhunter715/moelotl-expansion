"""Assemble and verify the installable mod from its runtime files."""
import hashlib
import re
from pathlib import Path
from zipfile import ZipFile, ZipInfo, ZIP_DEFLATED

ROOT = Path(__file__).resolve().parents[1]
version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version):
    raise ValueError("Invalid release version")
files = {name: ROOT / name for name in (
    "About/About.xml", "LoadFolders.xml", "1.6/Assemblies/MoeLotlExpansion.dll",
    "Languages/ChineseSimplified/Keyed/Medicines.xml", "Languages/English/Keyed/Medicines.xml",
    "VERSION", "CHANGELOG.md", "THIRD_PARTY_NOTICES.md",
)}
files["README.md"] = ROOT / "docs/INSTALL.md"
for pattern in ("1.6/Defs/*/*.xml", "1.6/Textures/Things/Item/MoeLotl/MX_*.png"):
    matches = sorted(ROOT.glob(pattern))
    if not matches:
        raise FileNotFoundError(pattern)
    files.update({p.relative_to(ROOT).as_posix(): p for p in matches})

out = ROOT / "artifacts/releases"
out.mkdir(parents=True, exist_ok=True)
archive = out / f"moelotl-expansion-v{version}-rimworld1.6.zip"
hashes = {}
with ZipFile(archive, "w") as package:
    for name, source in sorted(files.items()):
        data = source.read_bytes()
        entry = ZipInfo("MoeLotlExpansion/" + name)
        entry.external_attr = 0o100644 << 16
        package.writestr(entry, data, compress_type=ZIP_DEFLATED)
        hashes[entry.filename] = hashlib.sha256(data).hexdigest()
with ZipFile(archive) as package:
    if package.testzip() or package.namelist() != list(hashes):
        raise ValueError("Invalid ZIP contents")
    for name, expected in hashes.items():
        if hashlib.sha256(package.read(name)).hexdigest() != expected:
            raise ValueError(f"Hash mismatch: {name}")
digest = hashlib.sha256(archive.read_bytes()).hexdigest()
archive.with_suffix(".zip.sha256").write_bytes(f"{digest}  {archive.name}\n".encode())
archive.with_suffix(".manifest.sha256").write_bytes(
    "".join(f"{h}  {name}\n" for name, h in hashes.items()).encode()
)
print(f"{archive.name}: {len(files)} files, SHA256 {digest}")
