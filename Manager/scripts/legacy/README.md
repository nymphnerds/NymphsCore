# Legacy Manager Scripts

This folder preserves monolith-era Manager scripts as migration reference material
while the official modules are moved into their own repos.

The generic Manager shell should not package or call files from this directory.
Anything still useful here should be copied into the owning module repo, adapted
to that module's `nymph.json` contract, tested from the registry card, and then
deleted from this legacy folder.

Some scripts still contain their original `Manager/scripts/...` path assumptions.
Treat them as source reference unless you deliberately restore/adapt those paths
inside a module migration branch.
