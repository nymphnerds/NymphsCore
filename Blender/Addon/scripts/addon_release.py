#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import subprocess
import sys
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
import tomllib
import zipfile


PACKAGE_FILES = (
    "Nymphs3D2.py",
    "__init__.py",
    "blender_manifest.toml",
)


@dataclass
class Manifest:
    schema_version: str
    addon_id: str
    version: str
    name: str
    tagline: str
    maintainer: str
    addon_type: str
    website: str
    blender_version_min: str
    license: list[str]
    permissions: dict[str, str]
    platforms: list[str]


def repo_root() -> Path:
    return Path(__file__).resolve().parents[1]


def default_extensions_repo() -> Path:
    return repo_root().parent / "Nymphs3D2-Extensions"


def load_manifest(root: Path) -> Manifest:
    data = tomllib.loads((root / "blender_manifest.toml").read_text(encoding="utf-8"))
    return Manifest(
        schema_version=data["schema_version"],
        addon_id=data["id"],
        version=data["version"],
        name=data["name"],
        tagline=data["tagline"],
        maintainer=data["maintainer"],
        addon_type=data["type"],
        website=data["website"],
        blender_version_min=data["blender_version_min"],
        license=list(data["license"]),
        permissions=dict(data["permissions"]),
        platforms=list(data.get("platforms", [])),
    )


def slugify(value: str) -> str:
    slug = re.sub(r"[^a-z0-9.]+", "-", value.lower()).strip("-")
    return re.sub(r"-{2,}", "-", slug)


def archive_name(manifest: Manifest) -> str:
    return f"{slugify(manifest.name)}-{manifest.version}.zip"


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def git(*args: str, cwd: Path, check: bool = True) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["git", *args],
        cwd=cwd,
        text=True,
        capture_output=True,
        check=check,
    )


def ensure_clean(cwd: Path, label: str) -> None:
    result = git("status", "--short", cwd=cwd)
    if result.stdout.strip():
        raise SystemExit(f"{label} is not clean. Commit or stash changes first.\n{result.stdout}")


def current_head(cwd: Path) -> str:
    return git("rev-parse", "--short", "HEAD", cwd=cwd).stdout.strip()


def tag_exists(cwd: Path, tag_name: str) -> bool:
    result = git("tag", "-l", tag_name, cwd=cwd)
    return bool(result.stdout.strip())


def create_tag(cwd: Path, tag_name: str, message: str) -> None:
    git("tag", "-a", tag_name, "-m", message, cwd=cwd)


def backup_tag_name(manifest: Manifest, prefix: str) -> str:
    stamp = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    return f"{prefix}-{manifest.version}-{stamp}"


def build_archive(root: Path, output_dir: Path) -> tuple[Path, int, str]:
    manifest = load_manifest(root)
    output_dir.mkdir(parents=True, exist_ok=True)
    archive_path = output_dir / archive_name(manifest)

    with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED) as bundle:
        for relative_name in PACKAGE_FILES:
            source_path = root / relative_name
            if not source_path.exists():
                raise SystemExit(f"Missing required package file: {source_path}")
            bundle.write(source_path, arcname=relative_name)

    return archive_path, archive_path.stat().st_size, sha256_file(archive_path)


def update_extensions_index(index_path: Path, manifest: Manifest, archive_file: Path, archive_size: int, archive_hash: str) -> None:
    payload = json.loads(index_path.read_text(encoding="utf-8"))
    entries = payload.setdefault("data", [])
    entry = next((item for item in entries if item.get("id") == manifest.addon_id), None)
    if entry is None:
        entry = {}
        entries.append(entry)

    entry.clear()
    entry.update(
        {
            "schema_version": manifest.schema_version,
            "id": manifest.addon_id,
            "name": manifest.name,
            "tagline": manifest.tagline,
            "version": manifest.version,
            "type": manifest.addon_type,
            "maintainer": manifest.maintainer,
            "license": manifest.license,
            "blender_version_min": manifest.blender_version_min,
            "website": manifest.website,
            "permissions": manifest.permissions,
            "platforms": manifest.platforms,
            "archive_url": f"./{archive_file.name}",
            "archive_size": archive_size,
            "archive_hash": f"sha256:{archive_hash}",
        }
    )

    index_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def command_backup_tag(args: argparse.Namespace) -> int:
    root = repo_root()
    manifest = load_manifest(root)
    ensure_clean(root, "Addon repo")

    tag_name = args.name or backup_tag_name(manifest, args.prefix)
    if tag_exists(root, tag_name):
        raise SystemExit(f"Tag already exists: {tag_name}")

    message = args.message or f"Backup source state for addon {manifest.version} at {current_head(root)}"
    create_tag(root, tag_name, message)
    print(f"Created addon backup tag: {tag_name}")
    return 0


def command_build(args: argparse.Namespace) -> int:
    root = repo_root()
    archive_path, archive_size, archive_hash = build_archive(root, Path(args.output_dir))
    print(f"Built: {archive_path}")
    print(f"Size: {archive_size}")
    print(f"SHA256: {archive_hash}")
    return 0


def command_publish(args: argparse.Namespace) -> int:
    root = repo_root()
    extensions_root = Path(args.extensions_repo).resolve()
    manifest = load_manifest(root)

    ensure_clean(root, "Addon repo")
    ensure_clean(extensions_root, "Extensions repo")

    if args.tag_source:
        tag_name = backup_tag_name(manifest, args.tag_prefix)
        if not tag_exists(root, tag_name):
            create_tag(root, tag_name, f"Backup source state for addon {manifest.version} at {current_head(root)}")
            print(f"Created addon backup tag: {tag_name}")
        else:
            print(f"Addon backup tag already exists: {tag_name}")

    archive_path, archive_size, archive_hash = build_archive(root, root / "dist")
    destination_archive = extensions_root / archive_path.name
    shutil.copy2(archive_path, destination_archive)
    update_extensions_index(extensions_root / "index.json", manifest, destination_archive, archive_size, archive_hash)

    print(f"Built addon archive: {archive_path}")
    print(f"Copied archive to: {destination_archive}")
    print(f"Updated extension index: {extensions_root / 'index.json'}")
    print(f"SHA256: {archive_hash}")
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Build, tag, and publish the Nymphs3D Blender addon release."
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    backup_parser = subparsers.add_parser(
        "backup-tag",
        help="Create an annotated rollback tag in the addon source repo.",
    )
    backup_parser.add_argument(
        "--prefix",
        default="backup-addon",
        help="Tag prefix to use before <version>-<date>.",
    )
    backup_parser.add_argument(
        "--name",
        help="Explicit tag name. Overrides the generated name.",
    )
    backup_parser.add_argument(
        "--message",
        help="Explicit tag message.",
    )
    backup_parser.set_defaults(func=command_backup_tag)

    build_parser_obj = subparsers.add_parser(
        "build",
        help="Build the addon zip from source into a local output directory.",
    )
    build_parser_obj.add_argument(
        "--output-dir",
        default=str(repo_root() / "dist"),
        help="Directory for the built addon zip.",
    )
    build_parser_obj.set_defaults(func=command_build)

    publish_parser = subparsers.add_parser(
        "publish",
        help="Build the addon zip and refresh the extension feed metadata.",
    )
    publish_parser.add_argument(
        "--extensions-repo",
        default=str(default_extensions_repo()),
        help="Path to the local Nymphs3D2-Extensions repo.",
    )
    publish_parser.add_argument(
        "--tag-source",
        action="store_true",
        help="Create a source backup tag in the addon repo before publishing.",
    )
    publish_parser.add_argument(
        "--tag-prefix",
        default="backup-addon",
        help="Tag prefix to use when --tag-source is set.",
    )
    publish_parser.set_defaults(func=command_publish)

    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
