# Addon Dev Workflow For Humans

This is the simple version.

If you forget everything else, remember this:

- write addon code in `Nymphs3D-Blender-Addon`
- keep backup points as git commits and tags in `Nymphs3D-Blender-Addon`
- publish finished addon zips into `Nymphs3D2-Extensions`
- do not treat `Nymphs3D2-Extensions` as your working code repo

## What Each Repo Is

### 1. `Nymphs3D-Blender-Addon`

This is your real addon source code.

This is where you edit:

- `Nymphs3D2.py`
- `__init__.py`
- `blender_manifest.toml`

If you are changing how the addon behaves in Blender, this is the repo you are
working in.

### 2. `Nymphs3D2-Extensions`

This is the published package repo.

It holds:

- the `.zip` files Blender downloads
- `index.json`
- `index.html`

Think of this repo like a shelf that holds boxed releases.
Do not do your main coding here.

### 3. `Nymphs3D`

This is the installer/launcher/platform repo.

Use this for:

- Windows installer work
- launcher work
- bigger platform integration work

Not for normal addon coding.

## The Golden Rule

If you are doing addon development:

- edit in `Nymphs3D-Blender-Addon`
- save your progress with git commits there
- create rollback tags there
- only publish finished zips into `Nymphs3D2-Extensions`

## The New Helper Tool

Use this file:

- `scripts/addon_release.py`

It gives you three main commands:

```bash
cd /home/babyj/Nymphs3D-Blender-Addon

python3 scripts/addon_release.py backup-tag
python3 scripts/addon_release.py build
python3 scripts/addon_release.py publish --tag-source
```

## The Normal Everyday Workflow

### Step 1. Start in the addon repo

```bash
cd /home/babyj/Nymphs3D-Blender-Addon
git status --short
```

You should be doing your addon coding here, not in the extensions repo.

### Step 2. Edit the addon

Work on files like:

- `Nymphs3D2.py`
- `__init__.py`
- `blender_manifest.toml`

### Step 3. When you reach a real checkpoint, commit it

A "real checkpoint" means:

- the addon is more correct than before
- or a bug is fixed
- or a feature is in a usable state
- or you just do not want to lose this state

Then run:

```bash
git status --short
git add Nymphs3D2.py __init__.py blender_manifest.toml
git commit -m "Describe what changed"
```

You do not need to wait for perfection.
A commit is just a save point with a message.

## How To Make A Backup Point You Can Roll Back To

If you have a version that feels safe or important, create a tag.

Example:

```bash
python3 scripts/addon_release.py backup-tag
```

This creates a named backup tag in the addon source repo.

That is your proper rollback point.

This is better than random copied folders because:

- git knows exactly what version it points to
- it stays attached to the real source history
- it is much easier to find later

## How To Build A Test Zip Without Publishing

If you just want a local addon package to test in Blender:

```bash
python3 scripts/addon_release.py build
```

This builds a zip into:

- `dist/`

That does not publish anything.
It just gives you a package to test.

## How To Publish A Real Addon Release

When you are happy with the addon and want to update the extension repo:

```bash
python3 scripts/addon_release.py publish --tag-source
```

This does all of the following:

- checks that `Nymphs3D-Blender-Addon` is clean
- checks that `Nymphs3D2-Extensions` is clean
- creates a backup tag in the addon source repo
- builds the addon zip
- copies the zip into `Nymphs3D2-Extensions`
- updates `Nymphs3D2-Extensions/index.json`

After that, you still need to commit and push the repo changes as normal.

Usually that means:

When someone says "push the addon", treat that as both of these:

- push the addon source repo changes in `Nymphs3D-Blender-Addon`
- publish and push the installable package feed in `Nymphs3D2-Extensions`

### In the addon repo

```bash
cd /home/babyj/Nymphs3D-Blender-Addon
git status --short
git push origin main
```

### In the extensions repo

```bash
cd /home/babyj/Nymphs3D2-Extensions
git status --short
git add index.json *.zip
git commit -m "Publish addon version X.Y.Z"
git push origin main
```

## The Safe Pattern To Follow

Use this pattern every time:

1. edit addon code in `Nymphs3D-Blender-Addon`
2. commit when you reach a good checkpoint
3. create a backup tag when you want a rollback point
4. build a local zip if you want to test
5. publish only when the addon is actually ready

## What Not To Do

Do not:

- code mainly in `Nymphs3D2-Extensions`
- keep random copied addon folders as your real backup system
- publish zips without knowing what source commit they came from
- leave important progress only as uncommitted local changes

## If You Need To Roll Back

First, list your backup tags:

```bash
cd /home/babyj/Nymphs3D-Blender-Addon
git tag -l "backup-addon-*"
```

Then inspect one:

```bash
git show backup-addon-EXAMPLE
```

If you need help actually moving the repo back to an older state, stop there
and do it carefully.
That is the moment to ask for help instead of guessing.

## The One-Sentence Version

Do all addon coding in `Nymphs3D-Blender-Addon`, save progress with commits,
mark safe rollback points with tags, and only use `Nymphs3D2-Extensions` to
hold the published zip and feed metadata.
