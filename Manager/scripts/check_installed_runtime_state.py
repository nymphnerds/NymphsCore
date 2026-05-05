#!/usr/bin/env python3
from __future__ import annotations

import json
import subprocess
import sys
from importlib import metadata
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
LOCK_FILE = SCRIPT_DIR / "runtime-deps.lock.json"
COMMON_PATHS = SCRIPT_DIR / "common_paths.sh"


def _run_git_head(path: Path) -> str:
    result = subprocess.run(
        ["git", "-C", str(path), "rev-parse", "HEAD"],
        text=True,
        capture_output=True,
        check=True,
    )
    return result.stdout.strip()


def _short(value: str) -> str:
    return value[:12] if len(value) > 16 else value


def _normalize_git_url(url: str) -> str:
    return url.lower().removesuffix(".git").rstrip("/")


def _read_nunchaku_commit(expected_repo: str, stamp_file: Path) -> tuple[str | None, str]:
    try:
        dist = metadata.distribution("nunchaku")
    except metadata.PackageNotFoundError:
        return None, "nunchaku package not installed"

    direct_url_text = dist.read_text("direct_url.json") or ""
    if direct_url_text:
        direct_url = json.loads(direct_url_text)
        actual_url = _normalize_git_url(direct_url.get("url") or "")
        actual_commit = ((direct_url.get("vcs_info") or {}).get("commit_id") or "").strip()
        if expected_repo and actual_url and actual_url != expected_repo:
            return actual_commit or None, f"nunchaku repo drift installed={actual_url} expected={expected_repo}"
        if actual_commit:
            return actual_commit, "direct-url"

    if stamp_file.is_file():
        stamp = json.loads(stamp_file.read_text(encoding="utf-8"))
        stamped_repo = _normalize_git_url(stamp.get("expected_repo") or "")
        stamped_commit = (stamp.get("expected_commit") or "").strip()
        if expected_repo and stamped_repo and stamped_repo != expected_repo:
            return stamped_commit or None, f"nunchaku stamp repo drift installed={stamped_repo} expected={expected_repo}"
        if stamped_commit:
            return stamped_commit, "stamp"

    return None, "nunchaku provenance unavailable"


def main() -> int:
    lock = json.loads(LOCK_FILE.read_text(encoding="utf-8"))
    deps = lock["dependencies"]

    home = Path.home()
    zimage_dir = home / "Z-Image"
    trellis_dir = home / "TRELLIS.2"
    nunchaku_stamp = zimage_dir / ".venv-nunchaku" / ".nymphs_nunchaku_runtime.json"

    issues: list[str] = []

    try:
        installed_n2d2 = _run_git_head(zimage_dir)
    except Exception as exc:
        issues.append(f"Nymphs2D2 missing/unreadable: {exc}")
        installed_n2d2 = ""

    expected_n2d2 = deps["Nymphs2D2"]["pinned"]
    if installed_n2d2 and installed_n2d2 != expected_n2d2:
        issues.append(f"Nymphs2D2 drift installed={_short(installed_n2d2)} expected={_short(expected_n2d2)}")

    try:
        installed_trellis = _run_git_head(trellis_dir)
    except Exception as exc:
        issues.append(f"TRELLIS.2 missing/unreadable: {exc}")
        installed_trellis = ""

    expected_trellis = deps["TRELLIS.2"]["pinned"]
    if installed_trellis and installed_trellis != expected_trellis:
        issues.append(f"TRELLIS.2 drift installed={_short(installed_trellis)} expected={_short(expected_trellis)}")

    expected_diffusers = deps["diffusers"]["pinned"]
    try:
        installed_diffusers = metadata.version("diffusers")
    except metadata.PackageNotFoundError:
        issues.append("diffusers not installed")
        installed_diffusers = ""

    if installed_diffusers and installed_diffusers != expected_diffusers:
        issues.append(f"diffusers drift installed={installed_diffusers} expected={expected_diffusers}")

    expected_nunchaku_repo = _normalize_git_url(deps["nunchaku"]["repo"])
    expected_nunchaku_commit = deps["nunchaku"]["pinned"]
    installed_nunchaku_commit, source = _read_nunchaku_commit(expected_nunchaku_repo, nunchaku_stamp)
    if installed_nunchaku_commit is None:
        issues.append(source)
    elif installed_nunchaku_commit != expected_nunchaku_commit:
        issues.append(
            f"nunchaku drift installed={_short(installed_nunchaku_commit)} expected={_short(expected_nunchaku_commit)}"
        )

    if issues:
        for line in issues:
            print(line)
        print("installed_runtime_state=drift")
        return 1

    print(f"Nymphs2D2: installed {_short(installed_n2d2)} matches current pin")
    print(f"TRELLIS.2: installed {_short(installed_trellis)} matches current pin")
    print(f"diffusers: installed {installed_diffusers} matches current pin")
    print(f"nunchaku: installed {_short(installed_nunchaku_commit or '')} matches current pin ({source})")
    print("installed_runtime_state=match")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
