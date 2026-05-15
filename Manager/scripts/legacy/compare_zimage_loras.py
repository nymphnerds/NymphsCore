#!/usr/bin/env python3
"""Summarize and compare Z-Image LoRA safetensors files.

This reads only safetensors headers, so it is safe to run on large LoRAs
without loading tensor payloads into memory.

Examples:
  python3 compare_zimage_loras.py /home/nymph/LoRA/loras/GroupTest
  python3 compare_zimage_loras.py /home/nymph/LoRA/loras/GroupTest /home/nymph/LoRA/loras/yamamoto
  python3 compare_zimage_loras.py a.safetensors b.safetensors
"""

from __future__ import annotations

import argparse
import json
import struct
from collections import Counter
from pathlib import Path
from typing import Iterable


def _read_header(path: Path) -> dict:
    with path.open("rb") as handle:
        raw_len = handle.read(8)
        if len(raw_len) != 8:
            raise ValueError("File is too small to be a safetensors file.")
        header_len = struct.unpack("<Q", raw_len)[0]
        header_bytes = handle.read(header_len)
        if len(header_bytes) != header_len:
            raise ValueError("Truncated safetensors header.")
    return json.loads(header_bytes.decode("utf-8"))


def _load_entries(path: Path) -> tuple[dict, dict[str, dict]]:
    header = _read_header(path)
    metadata = header.get("__metadata__", {})
    tensors = {key: value for key, value in header.items() if key != "__metadata__"}
    return metadata, tensors


def _group_key(key: str) -> str:
    if ".lora_A.weight" in key:
        return "lora_A"
    if ".lora_B.weight" in key:
        return "lora_B"
    if ".alpha" in key:
        return "alpha"
    return key.split(".", 3)[0]


def _module_prefix(key: str) -> str:
    parts = key.split(".")
    if len(parts) >= 4:
        return ".".join(parts[:4])
    return key


def _shape_signature(entries: dict[str, dict]) -> Counter[tuple[int, ...]]:
    return Counter(tuple(value.get("shape", [])) for value in entries.values())


def _dtype_signature(entries: dict[str, dict]) -> Counter[str]:
    return Counter(str(value.get("dtype", "unknown")) for value in entries.values())


def _group_signature(entries: dict[str, dict]) -> Counter[str]:
    return Counter(_group_key(key) for key in entries)


def _module_signature(entries: dict[str, dict]) -> Counter[str]:
    return Counter(_module_prefix(key) for key in entries)


def _summarize_file(path: Path) -> dict:
    metadata, entries = _load_entries(path)
    return {
        "path": str(path),
        "size_bytes": path.stat().st_size,
        "tensor_count": len(entries),
        "metadata": metadata,
        "dtypes": _dtype_signature(entries),
        "shapes": _shape_signature(entries),
        "groups": _group_signature(entries),
        "module_prefixes": _module_signature(entries),
        "keys": set(entries.keys()),
    }


def _iter_targets(values: Iterable[str]) -> list[Path]:
    results: list[Path] = []
    for raw in values:
        path = Path(raw).expanduser()
        if path.is_dir():
            results.extend(sorted(path.glob("*.safetensors")))
        elif path.is_file():
            results.append(path)
        else:
            raise FileNotFoundError(f"Path not found: {path}")
    if not results:
        raise FileNotFoundError("No .safetensors files found.")
    return results


def _print_counter(title: str, counter: Counter, limit: int = 12) -> None:
    print(f"{title}:")
    for key, count in counter.most_common(limit):
        print(f"  {count:4d}  {key}")
    if len(counter) > limit:
        print(f"  ... {len(counter) - limit} more")


def _print_summary(summary: dict) -> None:
    print("=" * 88)
    print(summary["path"])
    print(f"size_bytes: {summary['size_bytes']}")
    print(f"tensor_count: {summary['tensor_count']}")
    print(f"metadata: {summary['metadata'] or '{}'}")
    _print_counter("dtype_counts", summary["dtypes"])
    _print_counter("group_counts", summary["groups"])
    _print_counter("top_module_prefixes", summary["module_prefixes"])
    _print_counter("top_shapes", summary["shapes"])


def _print_pairwise(left: dict, right: dict) -> None:
    left_name = Path(left["path"]).name
    right_name = Path(right["path"]).name
    print("-" * 88)
    print(f"compare: {left_name}  <->  {right_name}")
    print(f"tensor_count_delta: {left['tensor_count'] - right['tensor_count']}")
    print(f"size_bytes_delta: {left['size_bytes'] - right['size_bytes']}")

    only_left = sorted(left["keys"] - right["keys"])
    only_right = sorted(right["keys"] - left["keys"])
    print(f"keys_only_in_{left_name}: {len(only_left)}")
    if only_left:
        for key in only_left[:10]:
            print(f"  {key}")
    print(f"keys_only_in_{right_name}: {len(only_right)}")
    if only_right:
        for key in only_right[:10]:
            print(f"  {key}")

    shared = left["keys"] & right["keys"]
    shape_mismatches = []
    if shared:
        _, left_entries = _load_entries(Path(left["path"]))
        _, right_entries = _load_entries(Path(right["path"]))
        for key in sorted(shared):
            left_shape = tuple(left_entries[key].get("shape", []))
            right_shape = tuple(right_entries[key].get("shape", []))
            left_dtype = str(left_entries[key].get("dtype", "unknown"))
            right_dtype = str(right_entries[key].get("dtype", "unknown"))
            if left_shape != right_shape or left_dtype != right_dtype:
                shape_mismatches.append((key, left_shape, right_shape, left_dtype, right_dtype))

    print(f"shared_keys: {len(shared)}")
    print(f"shape_or_dtype_mismatches: {len(shape_mismatches)}")
    for key, left_shape, right_shape, left_dtype, right_dtype in shape_mismatches[:10]:
        print(
            f"  {key}\n"
            f"    left : shape={left_shape} dtype={left_dtype}\n"
            f"    right: shape={right_shape} dtype={right_dtype}"
        )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "paths",
        nargs="+",
        help="One or more .safetensors files or directories containing them.",
    )
    args = parser.parse_args()

    targets = _iter_targets(args.paths)
    summaries = [_summarize_file(path) for path in targets]

    for summary in summaries:
        _print_summary(summary)

    if len(summaries) >= 2:
        print("=" * 88)
        print("pairwise comparisons")
        for idx in range(len(summaries) - 1):
            _print_pairwise(summaries[idx], summaries[idx + 1])

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
