# Runtime Dependency Pins

`runtime-deps.lock.json` records the external runtime dependencies that are pinned for stable customer installs.

Normal installers use the pinned values so a released NymphsCore build installs the same upstream code that was tested for that release. The pins can still be overridden with environment variables when intentionally testing newer upstream code.

Check for upstream updates without changing anything:

```bash
python3 scripts/check_runtime_dependency_updates.py
```

If the checker reports updates, test those versions in a fresh runtime before changing the pins for the next release.
