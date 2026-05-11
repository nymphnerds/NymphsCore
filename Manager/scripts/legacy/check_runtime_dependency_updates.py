#!/usr/bin/env python3
from __future__ import annotations

import json
import re
import subprocess
import sys
from pathlib import Path
from urllib.request import urlopen


DEFAULT_LOCK_FILE = Path(__file__).with_name("runtime-deps.lock.json")


def _run_git_ls_remote(repo: str, ref: str) -> str:
    result = subprocess.run(
        ["git", "ls-remote", repo, ref],
        text=True,
        capture_output=True,
        check=True,
    )
    line = result.stdout.strip().splitlines()[0]
    return line.split()[0]


def _latest_pypi_version(url: str) -> str:
    with urlopen(url, timeout=20) as response:
        payload = json.loads(response.read().decode("utf-8"))
    return str(payload["info"]["version"])


def _latest_pip_index_version(package: str, index_url: str) -> str:
    result = subprocess.run(
        [
            sys.executable,
            "-m",
            "pip",
            "index",
            "versions",
            "--pre",
            "--index-url",
            index_url,
            package,
        ],
        text=True,
        capture_output=True,
        check=True,
    )
    output = "\n".join(part for part in (result.stdout, result.stderr) if part)
    available_match = re.search(r"Available versions:\s*([^\n]+)", output)
    if available_match:
        return available_match.group(1).split(",", 1)[0].strip()
    first_line = output.strip().splitlines()[0]
    version_match = re.search(r"\(([^)]+)\)", first_line)
    if version_match:
        return version_match.group(1).strip()
    raise RuntimeError(f"could not parse pip index output for {package}")


def _short(value: str) -> str:
    return value[:12] if len(value) > 16 else value


def main(argv: list[str]) -> int:
    lock_file = Path(argv[1]).resolve() if len(argv) > 1 else DEFAULT_LOCK_FILE
    data = json.loads(lock_file.read_text(encoding="utf-8"))
    deps = data.get("dependencies", {})
    exit_code = 0

    for name, dep in deps.items():
        pinned = str(dep.get("pinned", "")).strip()
        kind = dep.get("kind")
        try:
            if kind == "git":
                latest = _run_git_ls_remote(str(dep["repo"]), str(dep.get("tracking_ref") or "HEAD"))
            elif kind == "pypi":
                latest = _latest_pypi_version(str(dep["latest_url"]))
            elif kind == "pip_index":
                latest = _latest_pip_index_version(str(dep["package"]), str(dep["index_url"]))
            else:
                print(f"{name}: unknown dependency kind {kind!r}")
                exit_code = 2
                continue
        except Exception as exc:
            print(f"{name}: could not check updates: {exc}")
            exit_code = 2
            continue

        if latest == pinned:
            print(f"{name}: up to date ({_short(pinned)})")
        else:
            print(f"{name}: update available pinned={_short(pinned)} latest={_short(latest)}")
            exit_code = max(exit_code, 1)

    return exit_code


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
