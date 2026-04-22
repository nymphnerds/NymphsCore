# No-Tar Live Audit Checklist

This guide is the practical next step for the `no-hunyuan-2mv-no-tar` branch.

Goal:

- inspect one known-good tar-based install
- capture what state it actually contains
- identify anything that would be lost or change if the installer stops depending on `NymphsCore.tar`

## What To Run

Inside the installed distro as the managed user:

```bash
bash /path/to/NymphsCore/Manager/scripts/audit_no_tar_readiness.sh | tee ~/no-tar-audit.txt
```

If the helper repo is already present inside the distro at `/opt/nymphs3d/Nymphs3D`, this is also fine:

```bash
bash /opt/nymphs3d/Nymphs3D/scripts/audit_no_tar_readiness.sh | tee ~/no-tar-audit.txt
```

If you want to run it from Windows against the managed distro:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "bash /mnt/c/path/to/NymphsCore/Manager/scripts/audit_no_tar_readiness.sh | tee ~/no-tar-audit.txt"
```

## What To Look For

Pay attention to:

- whether `/opt/nymphs3d/Nymphs3D` exists and whether it differs from the effective helper checkout
- whether `/opt/nymphs3d/runtime` still contains seeded backend repos from the tar
- whether the effective runtime repos under `/home/nymph` are on expected remotes and branches
- whether any repo has local modifications or untracked files that look intentional
- whether `/etc/profile.d/nymphscore.sh`, `/etc/wsl.conf`, and sudoers snippets match current script expectations
- whether required packages are present only because the tar carried them forward

## High-Value Questions

1. Is the tar carrying repo snapshots under `/opt` that the live install no longer uses?
2. Are there custom edits in helper/backend repos that are not reproducibly created by scripts?
3. Are there config files or package assumptions that would break a fresh-Ubuntu bootstrap?
4. Does the current working install rely on anything outside:
   - packaged helper scripts
   - bootstrap package install
   - finalize/setup scripts

## Expected Good Outcome

A clean no-tar migration becomes much safer if the audit shows:

- helper/backend repos are on known remotes and expected branches
- local diffs are absent or understood
- `/etc` config files match the scripts
- nothing important depends on hidden `/opt` state from the exported base image

## If The Audit Finds Custom State

Do not remove the tar path yet.

Instead:

1. write down the custom state exactly
2. decide whether it belongs in versioned scripts or versioned repo files
3. reproduce it in bootstrap/finalize logic first
4. only then replace the tar install path
