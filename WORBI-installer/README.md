# WORBI Installer

Install WORBI on Linux with a single command — no technical knowledge required.

---

## What is WORBI?

WORBI is a web application that runs locally on your machine. It has a frontend (what you see in your browser) and a backend (the server that powers it). This installer handles everything for you.

**Installation location:** `~/worbi/` (e.g., `/home/yourname/worbi/`)

---

## Requirements

- **Operating System:** Linux (x86_64 or ARM64)
- **Internet Connection:** Required during installation to download files
- **Disk Space:** About 2 GB free
- **git:** Comes pre-installed on most Linux systems
- **curl:** Comes pre-installed on most Linux systems

> **Note:** Node.js is **not** required beforehand. The installer will install it automatically if it's missing, without needing `sudo` (administrator password).

---

## Running this on WSL (Windows Subsystem for Linux)

If you're running this inside WSL on Windows, you'll first need to open a Linux terminal.

### How to open a WSL terminal

1. Click the **Start** button on Windows
2. Type **`Ubuntu`** (or **`WSL`**) and press Enter
3. A terminal window will open with a Linux command prompt (it looks like: `user@computer:~$`)
4. **Copy and paste** the commands below into this window

> **Tip:** In WSL, you can paste with a **right-click** instead of Ctrl+V.

---

## Quick Install (One Command)

Copy and paste this entire command into your terminal:

```bash
if [ -d ~/NymphsCore ]; then cd ~/NymphsCore && git checkout rauty && git pull; else cd ~ && git clone --branch rauty --depth 1 --sparse https://github.com/nymphnerds/NymphsCore.git && cd NymphsCore && git sparse-checkout set WORBI-installer; fi && chmod +x WORBI-installer/install.sh && ./WORBI-installer/install.sh
```

### What does this command do?

1. **Checks** if a `~/NymphsCore` folder already exists
2. **If it exists:** Switches to the `rauty` branch and updates it
3. **If it doesn't exist:** Downloads only the `WORBI-installer` folder from GitHub
4. **Runs the installer**, which installs WORBI to `~/worbi/`

---

## Step-by-Step Install (Alternative)

If you prefer to go one step at a time, follow the instructions below.

---

### Step 1 — Check if NymphsCore Already Exists

Run this command to see if you already have the NymphsCore folder:

```bash
ls ~/NymphsCore
```

**What to do next:**

- **If you see a list of files** → The folder exists → Go to **Step 2A**
- **If you see an error** like "No such file or directory" → The folder doesn't exist → Go to **Step 2B**

---

### Step 2A — Update Existing NymphsCore Folder

If the folder already exists, switch to the `rauty` branch and update it:

```bash
cd ~/NymphsCore
```

```bash
git checkout rauty
```

```bash
git pull
```

Then continue to **Step 3**.

---

### Step 2B — Download the Installer (First Time)

If the folder doesn't exist, download only the `WORBI-installer` folder from GitHub:

```bash
cd ~
```

```bash
git clone --branch rauty --depth 1 --sparse https://github.com/nymphnerds/NymphsCore.git
```

```bash
cd NymphsCore
```

```bash
git sparse-checkout set WORBI-installer
```

Then continue to **Step 3**.

---

### Step 3 — Run the Installer

Make the installer script executable, then run it:

```bash
chmod +x WORBI-installer/install.sh
```

```bash
./WORBI-installer/install.sh
```

Sit back and let it run. You'll see progress messages as it works.

When it finishes, WORBI will be installed to `~/worbi/`.

---

## After Installation

### Add Commands to Your PATH

The installer places commands in `~/.local/bin/`. To make them work from any terminal, run:

```bash
echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.bashrc
```

```bash
source ~/.bashrc
```

> You only need to do this **once**.

---

### Start WORBI

Run this command to start the application:

```bash
worbi-start
```

Then open your web browser and go to:

| URL | What You'll See |
|-----|----------------|
| **http://localhost:5173** | The WORBI frontend (main app) |
| **http://localhost:8082** | The WORBI backend API |

### Other Commands

| Command | What It Does |
|---------|-------------|
| `worbi-stop` | Stops both servers |
| `worbi-status` | Checks if the servers are running and healthy |

**Logs** are saved in `~/worbi/logs/` — check them if something goes wrong.

---

## Uninstall

To remove WORBI from your system, run these commands one at a time:

**1. Stop WORBI if it's running:**

```bash
worbi-stop
```

**2. Remove the WORBI folder:**

```bash
rm -rf ~/worbi
```

**3. Remove the commands:**

```bash
rm -f ~/.local/bin/worbi-start ~/.local/bin/worbi-stop ~/.local/bin/worbi-status
```

**4. (Optional) Remove Node.js** — only if it was installed by this installer and you don't need it for anything else:

```bash
rm -rf ~/.local
```

---

## Troubleshooting

### Problem: "command not found" for worbi-start / worbi-stop / worbi-status

**Cause:** The commands are in `~/.local/bin/` but your shell doesn't know to look there.

**Fix:**

```bash
echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.bashrc
```

```bash
source ~/.bashrc
```

---

### Problem: "permission denied" when running the installer

**Cause:** The installer script is not marked as executable.

**Fix:**

```bash
chmod +x ~/NymphsCore/WORBI-installer/install.sh
```

Then run it again:

```bash
~/NymphsCore/WORBI-installer/install.sh
```

---

### Problem: Installer fails or gets stuck

**Fix:** Make sure you have a working internet connection and at least 2 GB of free disk space. Check available space with:

```bash
df -h ~/
```

---

### Problem: Frontend doesn't load in the browser

**Fix:** Wait a minute after running `worbi-start` — the frontend can take some time to build on first launch. Check the status with:

```bash
worbi-status
```

If it still doesn't work, look at the log files in `~/worbi/logs/` for clues.

---

## File Contents

| File | Description |
|------|-------------|
| `README.md` | This file |
| `install.sh` | The installer script — run this to install WORBI |
| `worbi-6.2.18.tar.gz` | The WORBI application package (compressed, no dependencies) |