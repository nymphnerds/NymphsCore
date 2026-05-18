# NymphsCore Wiki Handoff

Date: 2026-05-18  
Repo: `/home/nymph/NymphsCore`  
Live wiki: `https://nymphnerds.github.io/NymphsCore/home/guides.html`  
Local preview: `http://localhost:8765/guides.html`

## Mission

This handoff is for helping with the NymphsCore GitHub Pages wiki.

The goal is to make the site clear and useful for users installing and using NymphsCore.

Focus on:

- Manager
- System Checks
- WSL Base Distro / Base Runtime install
- Modules
- Blender addon panels

Use screenshots and short MP4s where they help. Keep writing clear and user-facing. Do not invent module details; check source or ask.

Main wiki file:

```text
/home/nymph/NymphsCore/home/guides.html
```

Wiki media folder:

```text
/home/nymph/NymphsCore/home/assets/wiki
```

## ShareX Settings

Set this up first. The captures for the wiki should go straight into the website asset folder.

1. Run **ShareX as administrator**.

2. Set the save folder:

```text
ShareX > Application settings > Paths > Use custom screenshots folder
```

Folder:

```text
\\wsl.localhost\NymphsCore_Lite\home\nymph\NymphsCore\home\assets\wiki
```

3. Set hotkeys:

```text
ShareX > Hotkey settings
```

Use:

```text
Print Screen  = Capture active window
Scroll Lock   = Start/Stop screen recording
```

4. Turn cursor on:

```text
ShareX > Task settings > Capture > Show cursor in screenshots
```

5. Use MP4 recording, not GIF:

```text
ShareX > Task settings > Capture > Screen recorder > Screen recording options...
```

FFmpeg command:

```text
-f gdigrab -thread_queue_size 1024 -rtbufsize 256M -framerate 8 -offset_x $area_x$ -offset_y $area_y$ -video_size $area_width$x$area_height$ -draw_mouse $cursor$ -i desktop -c:v libx264 -preset veryfast -crf 32 -vf scale=trunc(iw/2/2)*2:trunc(ih/2/2)*2 -pix_fmt yuv420p -movflags +faststart -an -y "$output$"
```

Suggested filenames:

```text
manager-system-checks.mp4
manager-base-runtime-install.mp4
manager-base-runtime-ready.png
module-zimage-install.mp4
module-trellis-install.mp4
```

## Working Together

Take turns on the wiki.

Before starting:

```bash
git pull origin main
```

When finished:

```bash
git add home/guides.html home/assets/wiki
git commit -m "Update wiki"
git push origin main
```

Then the next person pulls and works.

Do not both edit `home/guides.html` at the same time. That is what causes messy Git conflicts.

## Useful Commands

Check current repo state:

```bash
cd /home/nymph/NymphsCore
git status --short --branch
```

See new captures:

```bash
find home/assets/wiki -maxdepth 1 -type f -printf '%TY-%Tm-%Td %TH:%TM:%TS %f %s bytes\n' | sort
```

Do not commit accidental ShareX captures.

GitHub Pages updates from `main`, usually after a short delay.
