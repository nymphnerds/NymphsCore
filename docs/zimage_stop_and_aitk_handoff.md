# Z-Image Stop / AI Toolkit Handoff

Date: 2026-05-02

## 2026-05-02 Current Restart Point

This document started as the stop-button handoff, but it now needs to serve as the broader restart point for the current trainer state.

The short honest version is:

- the trainer began life as a DiffSynth-style managed sidecar flow
- it was later dragged toward AI Toolkit
- that split caused days of pain
- the fundamentals are now working again
- the system is still not finished

Current working baseline:

- `Add Job` creates or updates a real AI Toolkit job
- `Start Job` starts that existing AI Toolkit job
- `Stop Job` stops that job through the AI Toolkit path
- `Kill AI Toolkit` now stops the AI Toolkit server

What is still incomplete:

- the Manager page still needs cleaner status/log wording
- dataset/image visibility in AI Toolkit still needs to feel more natural
- the preset model is still too small compared with the real AI Toolkit job surface
- the trainer page is still carrying too much history from the old custom-managed flow

Snapshot checkpoint:

- git commit: `2f9cd94`
- message: `Snapshot trainer handoff progress and changelog pain trail`

This is the safest place to restart from if more cleanup goes wrong.

## Current Product Direction

This is now the agreed shape of the trainer:

```text
Manager = easy AI Toolkit front end
AI Toolkit = real engine / queue / worker / runtime state
```

Meaning:

- Manager should prepare datasets, captions, and presets
- Manager should create or update real AI Toolkit jobs
- Manager should start and stop those jobs through AI Toolkit semantics
- Manager should not try to become its own second training backend

## Current Button Semantics

The current intended Manager button meanings are:

- `Add Job`
  - prepare caption/data handoff
  - create or update the AI Toolkit job
  - do not start training

- `Start Job`
  - start the existing AI Toolkit job
  - do not recreate the job

- `Stop Job`
  - stop the existing AI Toolkit job through AI Toolkit

- `Delete Job`
  - remove the saved AI Toolkit job

This matters because a lot of earlier confusion came from `Start Training` pretending to do several different things at once.

## Current Preset Direction

The next major phase is not more stop-button surgery.

The next major phase is:

```text
make presets much richer while keeping the Manager UI simple
```

The current Manager preset object already controls some AI Toolkit fields:

- steps
- learning rate
- rank
- resolution
- low VRAM
- content/style mode
- save cadence
- sample cadence
- sample prompt

But AI Toolkit jobs contain many more useful fields than the current preset object carries.

That means the correct next direction is:

- keep the Manager UI small
- expand the preset definition underneath it
- let presets own many more AI Toolkit defaults
- only expose a few safe user-facing knobs in Manager

Examples of fields that should likely move into presets rather than cluttering the Manager UI:

- optimizer
- weight decay
- batch size
- gradient accumulation
- timestep type
- loss type
- EMA usage
- cache text embeddings
- caption dropout rate
- cache latents
- flip X / flip Y
- quantization defaults
- save dtype
- save retention
- resolution arrays rather than a single number

## Next Phase: Researched Z-Image Turbo Presets

The next meaningful stage should be:

```text
research real-world tried-and-tested Z-Image Turbo settings,
then turn those into AI Toolkit-backed Manager presets
```

Planned sources:

- YouTube walkthrough transcripts
- AI Toolkit issue threads / discussions
- successful local NymphsCore runs

Important rule:

- the presets should be based on settings that actually produced usable results
- not just guessed defaults

Examples of the kind of preset library this points toward:

- `Z-Image Turbo Character Baseline`
- `Z-Image Turbo Stylized Drawing`
- `Z-Image Turbo 12 GB VRAM Safe`
- `Z-Image Turbo Fast Test`
- `Z-Image Turbo Strong Style`

The point is:

- Manager stays simple
- AI Toolkit still stores the real job
- presets become the place where the deeper tested config lives

### First captured YouTube-tested setting note

One recent YouTube walkthrough described a good Z-Image Turbo training setup using:

- optimizer: `AdamW8Bit`
- learning rate: `0.0001`
- weight decay: `0.0001`
- timestep type: `weighted`
- timestep bias: `balanced`
- loss type: `Mean Squared Error`
- EMA: `off`
- unload text encoder: `off`
- cache text embeddings: `on`
- differential output preservation: `off`
- blank prompt preservation: `off`

Important note for current Manager state:

- Manager already matches much of this
- but `cache_text_embeddings` is currently still `false` in the generated job config
- `timestep_bias` is also not yet being carried explicitly in the current Manager preset/config path

So this is one of the first concrete examples of why the next preset phase should expand beyond just:

- steps
- rank
- learning rate
- resolution
- low VRAM

and start carrying more of the deeper AI Toolkit training defaults inside the preset itself.

## Technical Interface Testing Still Needed

Even with the current progress, there are still important Manager-to-AI-Toolkit interface points that need explicit testing.

These should be treated as the next technical verification checklist.

### 1. Add Job -> AI Toolkit job creation

Need to keep verifying:

- `Add Job` creates the job in AI Toolkit without starting it
- the job appears in the correct AI Toolkit section (`Jobs > Idle`)
- the saved AI Toolkit job contains the expected config values
- re-running `Add Job` updates the existing job instead of silently creating duplicates

### 2. Start Job -> AI Toolkit start flow

Need to keep verifying:

- `Start Job` acts on the existing job
- it does not silently rebuild metadata or recreate the job first
- the job moves from idle into active queue/running state correctly
- AI Toolkit queue state and job state remain consistent

### 3. Stop Job -> AI Toolkit stop flow

Need to keep verifying:

- `Stop Job` sets the AI Toolkit stop path correctly
- the trainer exits without requiring a manual Linux kill
- stopping from Manager and stopping from AI Toolkit UI stay consistent with each other

### 4. Kill AI Toolkit -> actual server shutdown

Need to keep verifying:

- `Kill AI Toolkit` stops the real server, not just the wrapper
- Manager does not claim AI Toolkit is gone while `localhost:8675` is still live
- browser-tab confusion does not get mistaken for server liveness

### 5. Dataset visibility in AI Toolkit

Need to keep verifying:

- Manager pushes the correct AI Toolkit settings for dataset folders
- datasets show up in AI Toolkit `Datasets`
- image counts are correct
- per-image previews appear correctly
- this stays true after relaunch, repair, and job recreation

### 6. Caption handoff

Need to keep verifying:

- the Manager caption path produces the `.txt` captions AI Toolkit training expects
- CSV-to-TXT mirroring stays in sync
- manual caption edits are preserved and not silently overwritten
- Brain captioning still works inside the isolated trainer/runtime setup

### 7. Job-config field mapping

Need to keep verifying:

- Manager values land in the AI Toolkit job config under the correct types
- no string-vs-number regressions reappear
- preset-backed defaults survive save/start/reopen cycles
- opening the same job in AI Toolkit reflects the values Manager intended

### 8. UI-state honesty

Need to keep verifying:

- Manager button labels match what AI Toolkit is actually doing
- Manager status/log text reflects the real AI Toolkit state
- queue-empty, idle-job, and running-job states are not confused with each other

### 9. Repair / relaunch resilience

Need to keep verifying:

- repair does not break the AI Toolkit interface assumptions
- relaunch after stop or kill stays fast and predictable
- Manager can reconnect to an already-running healthy AI Toolkit instance cleanly

### 10. Multi-run sanity

Need to keep verifying:

- adding, editing, and starting several different jobs does not cross-wire state
- deleting one job does not affect others
- dataset/job matching remains stable when switching between LoRA names in Manager

## Current Important Lesson

The biggest lesson from this entire pass is still:

```text
the pain mostly came from architecture drift, not from AI Toolkit itself
```

Stop, queue, UI launch, and job visibility all got much harder when the Manager tried to own too much runtime behavior itself.

The more the trainer now follows:

- AI Toolkit job rows
- AI Toolkit queue/worker semantics
- AI Toolkit settings
- AI Toolkit job pages

the simpler the system gets.

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

## 2026-05-02 follow-up: the `bf16` pivot mattered

After the original handoff slog, a second pass focused on the repeated `loss is nan` problem in Z-Image Turbo training.

What was confirmed:

- AI Toolkit really does print `loss is nan` only when the loss tensor is actually NaN
- AI Toolkit then replaces that NaN loss with a zero tensor and keeps training
- this explains the repeated `loss: 0.000e+00` lines after the NaN warning

What changed:

- Manager-generated Z-Image trainer jobs were switched from `fp16` to `bf16`
- learning-rate display/import was normalized so `0.0001` and `1e-4` map to the same Manager UI option

What finally improved:

- a later `Fast Test` run using:
  - `bf16`
  - `8e-5`
  - `Low VRAM`
  produced a real nonzero loss line:
  - `lr: 8.0e-05 loss: 5.098e-01`

Why this matters:

- this is the first genuinely healthy-looking Z-Image Turbo training signal seen through the rebuilt Manager -> AI Toolkit flow
- it strongly suggests the old `fp16` path was a major cause of the repeated NaN behavior

Important correction:

- `Stop Job` does appear to work
- but it is slow enough that it was initially mistaken for being broken
- so future debugging should treat stop as `slow / laggy`, not `definitely dead`

What still needs work:

- AI Toolkit overview can still stay stuck on `Starting job...` / `Step 0`
- AI Toolkit loss graph can remain empty even while the raw job log is active
- Manager progress is now much better, but Manager live log text can still lag behind the real run state
