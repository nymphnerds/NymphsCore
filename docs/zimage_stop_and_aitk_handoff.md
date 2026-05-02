# Z-Image Stop / AI Toolkit Handoff

Date: 2026-05-01

## WSL context

This handoff assumes two different WSL roles:

- dev/source WSL: `NymphsCore_Lite`
- test/runtime WSL: `NymphsCore`

Meaning:
- code edits and builds happen from the `NymphsCore_Lite` dev WSL source tree
- live trainer/runtime testing happens in the `NymphsCore` managed test WSL

Important:
- do not confuse source verification in `NymphsCore_Lite` with runtime verification in `NymphsCore`
- build/publish output comes from the dev WSL
- stop/start/status checks for the live trainer must be checked in the test WSL

## What this handoff is for

This is the restart point for the current Z-Image Trainer stop-button mess.

It covers:
- why `Stop Training` kept failing
- what AI Toolkit actually does for stop
- what has now been changed in Manager source
- what still needs real end-to-end verification

## The key architectural lesson

The repeated failure came from trying to stop Manager-launched training runs as if they were just arbitrary Linux processes.

AI Toolkit already has a real stop model:
- DB-backed `Job` rows in `aitk_db.db`
- a stored `pid`
- `Job.stop = 1`
- trainer-side polling of the DB stop flag

Relevant upstream files:
- `/home/nymph/tmp/ai-toolkit/ui/src/app/api/jobs/[jobID]/stop/route.ts`
- `/home/nymph/tmp/ai-toolkit/ui/src/utils/jobs.ts`
- `/home/nymph/tmp/ai-toolkit/extensions_built_in/sd_trainer/DiffusionTrainer.py`
- `/home/nymph/tmp/ai-toolkit/extensions_built_in/sd_trainer/UITrainer.py`

Important behavior:
- Official UI stop sets `stop = true` in the DB
- if a PID is known, it also sends a process signal
- trainer code checks `should_stop()` / `maybe_stop()` against the DB

This is the right mechanism.

## What Manager was doing before

Manager training was a parallel path:
- create YAML in `/home/nymph/ZImage-Trainer/jobs/<name>.yaml`
- launch `/home/nymph/ZImage-Trainer/bin/ztrain-run-config`
- try to stop with pidfiles / `pgrep` / `kill`

That caused repeated problems:
- click path confusion in WPF
- pidfile/state drift
- quoting bugs in bash strings
- heredoc bugs in installer scripts
- Manager and Official UI not sharing one authoritative stop system

## What was verified today

### 1. AI Toolkit stop mechanism was read directly

Verified from source:
- UI stop route sets DB `stop`
- trainer code polls DB `stop`
- this is not guesswork

### 2. Real trainer process shape was inspected in live `NymphsCore`

Observed live runs looked like:
- `bash /home/nymph/ZImage-Trainer/bin/ztrain-run-config ...`
- `python run.py /home/nymph/ZImage-Trainer/jobs/<name>.yaml`
- progress helper python

They also wrote:
- `/home/nymph/ZImage-Trainer/run/active_train.pid`

### 3. Stop backend logic was tested locally

A detached dummy run with:
- wrapper shell
- train pid
- progress pid
- pidfile

was successfully stopped by:
- reading pidfile
- deriving PGID
- `TERM` to the process group

This proved the raw backend logic can work.

### 4. Real release build script was run successfully

Verified by running:
- `build-release.ps1`

Result:
- source compiled and published successfully

## What changed in source today

### Manager stop UI path

Files:
- `Manager/apps/NymphsCoreManager/Views/MainWindow.xaml`
- `Manager/apps/NymphsCoreManager/Views/MainWindow.xaml.cs`
- `Manager/apps/NymphsCoreManager/ViewModels/MainWindowViewModel.cs`

Changes:
- `Stop Training` now uses a direct click handler
- click handler calls the view-model stop method directly
- stop method logs `Requesting training stop...` immediately
- duplicate-click guard moved inside the stop method

Intent:
- remove silent failure before the stop flow even starts

### Manager trainer stop backend

File:
- `Manager/apps/NymphsCoreManager/Services/InstallerWorkflowService.cs`

Changes made over the session:
- pidfile-first stop logic
- process-group kill support
- no-active-run handling
- reduced false failure messages

But the important new change is this:

### Direct Manager runs now try to use AI Toolkit stop semantics

File:
- `Manager/apps/NymphsCoreManager/Services/InstallerWorkflowService.cs`

New behavior:
- generated training config now includes:
  - `sqlite_db_path: '/home/<user>/ZImage-Trainer/aitk_db.db'`
- before direct run starts, Manager:
  - finds the AI Toolkit `Job` row by `name`
  - clears `stop`
  - marks it `running`
  - gets the real `job id`
- direct run exports:
  - `AITK_JOB_ID=<job id>`
- stop path now also updates the DB:
  - `stop = 1`
  - `info = 'Stopping job...'`

Intent:
- let direct Manager runs participate in the same DB stop path that AI Toolkit expects

This is the most important change from today.

## Other relevant source state

### Captioner

The good Brain captioner state should remain:
- anti-style cleanup
- retry prompts
- `return clean_style_caption(best)`

Do not re-litigate that unless a real regression is verified.

### Progress

Manager progress work now includes:
- staged pre-step progress text
- structured `TRAIN_PROGRESS current=... total=...`
- no fake `2/2` setup completion bar

### Learning rate list

Current UI list is the smaller low-to-high set:
- `5e-5`
- `8e-5`
- `1e-4`

User explicitly dislikes losing style choices and wants transcript-aligned reconsideration later.
Do not silently trim options further.

## What still is NOT honestly verified

Not fully verified end-to-end:
- clicking the built Windows `Stop Training` button in the actual released Manager and watching it stop a real run through the new DB-backed path

Important:
- source compiles
- backend logic and AI Toolkit stop path were verified
- but the final Windows click flow is still the thing that must be proven in a fresh build

## What to do next

1. Build from the current source.
2. Copy the fresh `publish/win-x64`.
3. Start a fresh training run created by this build.
   This matters because the run must include:
   - `sqlite_db_path`
   - `AITK_JOB_ID`
4. Click `Stop Training`.
5. Verify:
   - live log shows `Requesting training stop...`
   - DB `Job.stop` is set
   - trainer exits without manual kill

## If stop still fails

Check these in order:

1. Did the run start from the new build?
   The run must be started after the new `AITK_JOB_ID` / `sqlite_db_path` changes.

2. Does the job row exist in `aitk_db.db`?
   Manager now registers jobs there, but this must still be verified on the live run.

3. Is `AITK_JOB_ID` reaching the trainer process?
   If not, the trainer will not poll the DB stop flag.

4. Does the generated YAML include `sqlite_db_path`?
   Without that, AI Toolkit trainer code will not behave as UI-backed trainer logic.

5. If all of that is present and stop still fails:
   inspect the live run log for trainer-side stop polling behavior instead of adding more `pgrep` hacks.

## What not to do

- Do not keep inventing more ad-hoc Linux process kill logic first.
- Do not treat the Manager runner as a separate orchestration universe if AI Toolkit already has a job/stop system.
- Do not assume stop is “just a button binding” or “just a PID issue” without checking the DB-backed path.

## Bottom line

The correct direction is:
- Manager as front-end
- AI Toolkit job DB as source of truth
- AI Toolkit trainer stop semantics for stop

That is the lesson from this session.
