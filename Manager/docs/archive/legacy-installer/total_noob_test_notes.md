# Total Noob Test Notes

This document captures the installer experience from the perspective of a
complete Windows beginner:

- non-Linux-savvy
- non-technical
- Blender artist first
- not comfortable with GitHub or repos
- expecting a simple "download and double-click" workflow

The goal is to record friction exactly as it is experienced, then clean it up
later in focused installer/packaging work.

## Current Test Context

- test branch: `installer_test_drive_choice`
- test goal: validate the safer create-new-distro flow without touching an
  existing working WSL install
- test persona: "total noob", beginner Windows artist

## Notes

### 1. The downloaded test zip still looks like a source repo

Observed reaction:

- the downloaded file contains the whole repo
- this feels wrong and confusing for a beginner
- the installer package should only contain the file or files needed to start
  the install

Why this is a problem:

- a beginner should not have to interpret a repo layout
- seeing docs, scripts, source files, and folders makes the package feel like a
  developer download instead of an installer
- it increases hesitation before the first click

Desired future state:

- a minimal installer package
- one obvious file to double-click
- optional tiny install note, not full repo content
- the rest of the logic downloaded by the bootstrap after launch

Status:

- confirmed UX issue
- acceptable for internal branch testing
- not acceptable for final beginner-facing packaging

### 2. Installer crashed immediately after launch with a PowerShell parser error

Observed reaction:

- after extraction and launcher handoff, the installer crashed with a PowerShell
  parser error
- the error text was highly technical and not understandable for a beginner

Observed failure text:

- `Variable reference is not valid. ':' was not followed by a valid variable name character.`

Why this is a problem:

- the user has no way to know whether they caused the problem
- the failure happens before any real install decision can be made
- the message is developer-facing, not beginner-facing

Root cause:

- PowerShell string interpolation bug in the drive-normalization code path

Status:

- confirmed implementation bug
- should be fixed before the next test attempt

### 3. Installer opening should feel more welcoming

Observed reaction:

- the installer should open with a welcoming message
- the first screen should explain what the installer is doing in plain English

Why this matters:

- confirms the user opened the correct file
- reduces anxiety before admin prompts and downloads
- makes the experience feel like an installer instead of a raw script

Status:

- confirmed UX improvement
- now implemented in the Windows bootstrap intro

### 4. The installer needs an explicit continue prompt before downloads begin

Observed reaction:

- after the welcome screen, the flow should ask to continue
- a raw `Terminate batch job (Y/N)?` prompt feels backwards and hostile
- when `N` was chosen, the terminal still shut down, which is extremely
  confusing for a beginner

Why this matters:

- a beginner expects `continue` to be the obvious default action
- Windows cancellation prompts are not understandable installer UX
- if the first meaningful choice is ambiguous, the installer feels broken

Desired future state:

- clear prompt: `Press Enter to continue, or type N to cancel`
- no reliance on raw `cmd.exe` cancellation prompts for the main beginner flow

Status:

- confirmed UX problem
- explicit continue/cancel prompt now added to the bootstrap intro

### 5. The download step still feels like a hang

Observed reaction:

- after pressing Enter, the download step still feels slow and uncertain
- the raw PowerShell web progress text is distracting and technical

Why this matters:

- beginners need reassurance that the installer is still working
- technical progress output makes the installer feel broken or unfinished

Desired future state:

- plain-English message before download starts
- suppress raw PowerShell web-request progress noise

Status:

- confirmed UX problem
- bootstrap download message simplified and PowerShell progress output suppressed

### 6. Drive-choice screen crashed before the user could choose a drive

Observed reaction:

- after choosing to create a new Ubuntu, the installer failed before showing the
  drive list properly
- the user never reached the actual drive-choice step

Why this matters:

- this blocks the main beginner feature being tested
- the user cannot tell whether the failure is theirs or the installer's

Root cause:

- PowerShell formatting bug while printing the drive list

Status:

- confirmed implementation bug
- fixed in the test branch after log review

### 7. The create-new flow asked for the drive twice

Observed reaction:

- the user selected a drive
- the installer confirmed the selected drive and target path
- then it asked for the Windows drive again

Why this matters:

- it makes the installer feel broken or forgetful
- it undermines trust in whether the chosen drive really stuck
- it adds confusion right at the main safety-critical step

Desired future state:

- ask for the drive once
- confirm the chosen drive once
- continue directly into distro creation

Status:

- confirmed implementation bug
- fixed in the test branch after beginner test feedback

### 8. The installer incorrectly claimed WSL did not support `--location`

Observed reaction:

- after the user chose the create-new-on-another-drive path, the installer said
  their WSL version did not support `--location`
- the tester reported that WSL was already up to date

Why this matters:

- it sends the user to the wrong fix
- it makes the installer appear unreliable
- it blocks the main create-new-distro workflow

Root cause:

- the script checked `wsl --help` instead of `wsl --install --help`
- that can falsely report missing `--location` support
- even the corrected help-text gate was still too brittle, so the installer now
  attempts the real `wsl --install ... --location` command directly and relies on
  WSL's actual response instead

Status:

- confirmed implementation bug
- fixed in the test branch after log review and tester feedback

### 9. The user should be warned that PowerShell and Windows permission prompts are expected

Observed reaction:

- the user needs advance warning that a PowerShell window may open
- the user needs to know that a Windows permission prompt is expected
- the user should be told to allow it so the installer can continue

Why this matters:

- without this warning, the UAC/PowerShell handoff feels suspicious
- a beginner may cancel the prompt because it looks unexpected
- the installer should explain expected Windows behavior before it happens

Desired future state:

- explicit wording in the intro that PowerShell may open
- explicit wording that Windows may ask for permission
- plain-English instruction to click `Yes` if they want to continue the install

Status:

- confirmed UX issue
- implemented in the bootstrap welcome text

### 10. The installer should feel like one PowerShell window, not a cmd window plus PowerShell

Observed reaction:

- the beginner experience felt confusing when both a batch window and a
  PowerShell window were involved
- the user explicitly asked for a PowerShell-only visible installer experience

Why this matters:

- two different terminal windows make the installer feel technical and messy
- the user should feel like they launched one installer, not a chain of scripts
- keeping one visible installer window builds trust and reduces hesitation

Desired future state:

- the batch launcher should hand off immediately and disappear
- the visible installer experience should be a single PowerShell window
- failures should pause inside that PowerShell window instead of relying on the
  outer batch window

Status:

- confirmed UX issue
- implemented in the test branch launcher flow

### 11. The WSL create step feels hung because it shows no visible progress

Observed reaction:

- after the user chose the drive and the installer started creating Ubuntu, the
  screen looked stuck
- the user explicitly asked whether the process was actually doing anything

Why this matters:

- long silent operations feel broken to a beginner
- this is the most trust-sensitive step because it is creating a new distro on
  another drive
- if progress is hidden, the user may force-close the installer mid-run

Desired future state:

- explain before the step that it can take several minutes
- stream WSL download and install progress visibly into the installer window
- avoid making the user check a log file to confirm that work is happening

Status:

- confirmed UX issue
- implemented in the test branch by streaming WSL create/install output

## How To Use This Document

As new beginner-friction points are discovered during testing, append them here
with:

- what the tester saw
- what confused them
- why it matters for a true beginner
- whether it is a blocker, moderate issue, or polish issue
