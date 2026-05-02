# Z-Image AI Toolkit Backend Handoff

Date: 2026-05-01

## What This Handoff Is About

This handoff records the real architectural problem in the current Z-Image Trainer integration:

```text
The Manager is still acting like a second training orchestrator
instead of being a guided front-end for the AI Toolkit job system.
```

That split is the root cause behind multiple confusing behaviors:

```text
- Manager-created jobs can run without appearing in the Official UI
- stop/progress/log handling had to be reimplemented in parallel
- Official UI and Manager do not naturally agree on job state
- fixes in one path do not automatically fix the other path
```

This document should be treated as the current restart point for anyone touching the trainer backend.

## Current Reality

### The Manager path

The current Manager trainer path still does all of this itself:

```text
- writes AI Toolkit YAML to /home/<user>/ZImage-Trainer/jobs/<name>.yaml
- launches training directly through ztrain-run-config / python run.py
- parses stdout for progress
- has its own stop logic
- has its own caption sync path
```

Relevant source:

```text
Manager/apps/NymphsCoreManager/Services/InstallerWorkflowService.cs
Manager/apps/NymphsCoreManager/ViewModels/MainWindowViewModel.cs
Manager/scripts/ztrain_run_config.sh
Manager/scripts/zimage_trainer_status.sh
```

### The Official AI Toolkit UI path

The Official UI does **not** list jobs by scanning `/jobs/*.yaml`.

Verified from source:

```text
ai-toolkit/ui/src/app/api/jobs/route.ts
ai-toolkit/ui/prisma/schema.prisma
```

What that code shows:

```text
- Official UI reads jobs from the Prisma Job table in aitk_db.db
- Official UI start/stop flows work through its DB-backed worker path
- Official UI logs/files/samples are resolved by job row and training folder
```

That means this is true:

```text
Manager YAML job != Official UI job row
```

## Why This Became Painful

The Manager trainer flow gradually became:

```text
guided UI
  +
separate YAML writer
  +
separate runner
  +
separate stop logic
  +
separate progress logic
```

instead of:

```text
guided UI
  ->
Official AI Toolkit job row
  ->
Official AI Toolkit queue / worker / logs / status
```

That duplication created avoidable churn in:

```text
- stop behavior
- progress reporting
- job visibility in Official UI
- queue state
- browser UI expectations
```

## Important Verified Findings

### 1. Official UI job list is DB-backed

Verified from:

```text
ai-toolkit/ui/src/app/api/jobs/route.ts
ai-toolkit/ui/prisma/schema.prisma
```

Meaning:

```text
If a job is not inserted into the Prisma Job table,
it will not show in the Official UI jobs list.
```

### 2. Official UI start path is worker-backed

Verified from:

```text
ai-toolkit/ui/cron/actions/startJob.ts
```

Meaning:

```text
The Official UI expects to write/read a DB job,
generate its own .job_config.json,
and launch run.py from that worker path.
```

### 3. Manager direct-run path is real but parallel

Manager training runs can be valid even when they are invisible to the Official UI.

That is because they currently use:

```text
/home/<user>/ZImage-Trainer/jobs/<name>.yaml
```

and bypass the DB worker path.

### 4. Captioning and preset work are not the same problem

Caption Brain improvements, preset tuning, and AI Toolkit sidecar install work can all be useful,
but they do not fix the architectural split by themselves.

The split is specifically:

```text
job creation + queue + start/stop + status source of truth
```

## Current Good Work That Should Be Kept

These pieces are still worth preserving:

```text
- metadata.csv as a first-class editable caption file
- per-image .txt caption mirroring before training
- Caption with Brain as optional helper only
- trainer sidecar under /home/<user>/ZImage-Trainer
- managed adapter preparation
- managed model install/repair path
- guided preset UX on the Manager page
```

The problem is not the guided UI.

The problem is duplicating the backend orchestration.

## Current Source State To Know About

As of this handoff, the Manager source contains work in progress around:

```text
- AI Toolkit YAML preset generation
- structured progress reporting
- direct stop logic for direct-run jobs
- best-effort registration of Manager-created jobs into aitk_db.db
```

Important:

```text
Best-effort DB registration is not the same thing as using the Official UI queue as the source of truth.
```

It may help visibility, but it does not fully solve the split architecture.

## Recommended Direction

### Product direction

Keep the Manager page as the beginner-first guided front-end.

Do **not** keep a second full training-control stack if it can be avoided.

### Backend direction

Move toward this shape:

```text
Manager edits/creates AI Toolkit job records
Manager starts jobs through the Official AI Toolkit queue/worker
Manager reads progress/status/logs from the same job model
Official UI and Manager both observe the same underlying job
```

This is the correct long-term fix.

## Practical Transition Plan

### Stage 1

Stop treating `/jobs/<name>.yaml` as the only source of truth.

Add a reliable Manager-owned translation layer that creates:

```text
- the YAML/config content the trainer needs
- the matching Job row in aitk_db.db
```

But do not stop there.

### Stage 2

Change Manager `Start Training` so it starts the AI Toolkit job through the same DB/queue path the Official UI uses,
instead of launching `run.py` directly itself.

That means:

```text
Manager should enqueue/start the DB job
not call ztrain-run-config as the main training authority
```

### Stage 3

Once Stage 2 is real, simplify or remove duplicated Manager logic for:

```text
- training stop
- training progress scraping
- job visibility fixes
- output/log source disagreement
```

At that point, Manager can become a cleaner observer/controller of the same AI Toolkit job system.

## Things To Avoid Repeating

Do not repeat these mistakes:

```text
- treating Official UI job visibility as a simple YAML-folder problem
- re-adding a second hard launch/readiness gate around localhost:8675
- inventing more Manager-only job state when AI Toolkit already has a DB-backed job model
- calling the duplicate path “good enough” once it starts training
```

The duplicate path is exactly what made stop/progress/visibility so fragile.

## Bottom Line

The important lesson is:

```text
The Manager should be the guided front-end.
AI Toolkit should be the training engine and job system.
```

Not:

```text
Manager as guided front-end
plus a second parallel training engine path
```

If future work has to choose between:

```text
patching the duplicate direct-run path again
or
moving closer to the Official UI queue/job model
```

the correct choice is:

```text
move closer to the Official UI queue/job model
```
