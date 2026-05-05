# WORBI Installer

Install WORBI on Linux with a single command — no technical knowledge required.

---

## What is WORBI?

WORBI is a web application that runs locally on your machine. It has a frontend (what you see in your browser) and a backend (the server that powers it). This installer handles everything for you.

**WORBI is installed to:** `~/worbi/` (e.g., `/home/yourname/worbi/`)

---

## Requirements

| Requirement | Details |
|-------------|---------|
| **Operating System** | Linux (x86_64 or ARM64) |
| **Internet Connection** | Needed during installation to download files |
| **Disk Space** | About 2 GB free |
| **git** | Comes pre-installed on most Linux systems |
| **curl** | Comes pre-installed on most Linux systems |

**Note:** Node.js is **not** required beforehand — the installer will install it automatically if it's missing, and it does so without needing `sudo` (administrator password).

---

## Quick Install (One Command)

Open your terminal and run the following command:

```bash
if [ -d ~/NymphsCore ]; then cd ~/NymphsCore && git checkout rauty && git pull; else cd ~ && git clone --branch rauty --depth 1 --sparse https://github.com/nymphnerds/NymphsCore.git && cd NymphsCore && git sparse-checkout set WORBI-installer; fi && chmod +x WORBI-installer/install.sh && ./WORBI-installer/install.sh
```

### What does this command do?

| Step | Action |
|------|--------|
| 1 | Checks if `~/NymphsCore` folder already exists |
| 2a | **If it exists:** Switches to the `rauty` branch and updates it |
| 2b | **If it doesn't exist:** Downloads only the `WORBI-installer` folder from GitHub |
| 3 | Makes the install script runnable |
| 4 | Runs the installer — WORBI is installed to `~/worbi/` |

The installer will then:
- Install **Node.js** if it's not already on your system (no `sudo` needed)
- Extract WORBI to `~/worbi/`
- Install all required dependencies
- Set up three easy-to-use commands: `worbi-start`, `worbi-stop`, `worbi-status`

---

## Step-by-Step Install (Alternative)

If you prefer to do things one step at a time, follow these instructions:

### Step 1 — Open a Terminal

Press `Ctrl + Alt + T` on most Linux systems, or search for "Terminal" in your applications menu.

### Step 2 — Check if NymphsCore Already Exists

Run this command to see if you already have the NymphsCore folder:

```bash
ls ~/NymphsCore
```

- **If you see a list of files:** The folder already exists — go to **Step 3A**
- **If you see an error like "No such file or directory":** The folder doesn't exist — go to **Step 3B**

### Step 3A — You Already Have NymphsCore

Switch to the `rauty` branch and update it:

```bash
cd ~/NymphsCore
```

```bash
git checkout rauty
```

```bash
git pull
```

Then jump to **Step 4**.

### Step 3B — Download the Installer (First Time)

This downloads only the `WORBI-installer` folder (not the entire repository):

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

### Step 4 — Run the Installer

Make the installer script executable, then run it:

```bash
chmod +x WORBI-installer/install.sh
```

```bash
./WORBI-installer/install.sh
```

Sit back and let it run. You'll see progress messages as it works. WORBI will be installed to `~/worbi/`.

---

## After Installation

### Make Commands Available Everywhere

The installer places commands in `~/.local/bin/`. To make them work from any terminal, add this folder to your PATH:

```bash
echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.bashrc
```

```bash
source ~/.bashrc
```

**What this does:** The first line adds the path to your shell configuration file. The second line loads it right away. You only need to do this once.

### Using WORBI

| Command | What It Does |
|---------|-------------|
| `worbi-start` | Starts both the frontend and backend servers |
| `worbi-stop` | Stops both servers |
| `worbi-status` | Checks if the servers are running and healthy |

After running `worbi-start`, open your web browser and go to:

| URL | What You'll See |
|-----|----------------|
| **http://localhost:5173** | The WORBI frontend (main app) |
| **http://localhost:8082** | The WORBI backend API |

**Logs** are saved in `~/worbi/logs/` — check them if something goes wrong.

---

## Uninstall

To remove WORBI from your system, run these commands one at a time:

```bash
# Stop WORBI if it's running
worbi-stop
```

```bash
# Remove the WORBI folder
rm -rf ~/worbi
```

```bash
# Remove the commands
rm -f ~/.local/bin/worbi-start ~/.local/bin/worbi-stop ~/.local/bin/worbi-status
```

**Optional:** If the installer set up Node.js for you and you don't need it for anything else, you can remove it too:

```bash
rm -rf ~/.local
```

---

## Troubleshooting

### "command not found" when running worbi-start / worbi-stop / worbi-status

The commands are installed in `~/.local/bin/` but your shell doesn't know to look there. Fix it by running:

```bash
echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.bashrc
```

```bash
source ~/.bashrc
```

Then try again.

### "permission denied" when running the installer

The installer script needs to be marked as executable. Run:

```bash
chmod +x ~/NymphsCore/WORBI-installer/install.sh
```

Then try running it again:

```bash
~/NymphsCore/WORBI-installer/install.sh
```

### Installer fails or gets stuck

Check that you have a working internet connection and at least 2 GB of free disk space. You can check your available space with:

```bash
df -h ~/
```

### Frontend doesn't load in the browser

Wait a minute after running `worbi-start` — the frontend can take some time to build on first launch. Check the status with:

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