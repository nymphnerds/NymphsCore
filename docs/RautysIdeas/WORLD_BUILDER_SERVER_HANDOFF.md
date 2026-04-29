# WORLD BUILDER UI — Server Handoff Document

**Generated**: 2026-04-29
**Scope**: Backend (Express.js REST API)

**WBUI Server** is the Express.js backend for a local, self-hosted multi-user web application for game developers to manage main story quests, side quests, character profiles, lore, and related assets. It provides passwordless JWT authentication, per-user filesystem isolation, file CRUD operations, image upload/serving, and an LLM proxy to a local OpenAI-compatible AI server.

**v2.0 Multi-user**: Users authenticate via username-only passwordless login (JWT-based). Each user gets their own isolated workspace and assets on the local filesystem.

**v2.1 Simplified LLM Config**: LLM server configuration is hardcoded in `server/src/config.js` rather than user-configurable. All users share the same LLM settings.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    Express Server (Port 8082)                   │
│  ┌──────────┐  ┌───────────┐  ┌──────────┐  ┌───────────────┐  │
│  │ Auth     │  │ File      │  │ LLM       │  │ Settings      │  │
│  │ Routes   │  │ Routes    │  │ Routes    │  │ Routes        │  │
│  └────┬─────┘  └────┬──────┘  └────┬──────┘  └──────┬────────┘  │
│       │             │              │                │           │
│  ┌────▼─────────────▼──────────────▼────────────────▼─────────┐  │
│  │         Services (authService, fileService, llmService)     │  │
│  └──────────────────┬─────────────────────────────────────────┘  │
│                     │                                             │
│              ┌──────▼──────┐                                     │
│              │ Local Disk  │  (server/data/users/<username>/)    │
│              │  Filesystem │                                     │
│              └─────────────┘                                     │
└─────────────────────────────────────────────────────────────────┘
```

### Key Architectural Decisions

- **npm workspace** at `WBServer/package.json` with server and client as workspaces
- **ESM modules** throughout the backend (all `.js` files use `import`/`export`)
- **JWT authentication** — passwordless login, 7-day token expiry
- **Per-user data isolation** — each user gets `data/users/<username>/workspace/`, `assets/`, `settings.json`
- **Path traversal protection** in file service (prevents escaping user workspace)
- **Shared LLM config** hardcoded in `config.js` — all users share the same LLM server

---

## Tech Stack

| Layer | Technology | Purpose |
|-------|------------|---------|
| **Backend Framework** | Node.js + Express | REST API server |
| **Auth** | jsonwebtoken | JWT token generation & verification |
| **Module System** | ESM (`"type": "module"`) | Modern import/export syntax |
| **File Upload** | Multer 1.4.5-lts.1 | Multipart image uploads |
| **HTTP Client** | Fetch API | Backend→LLM proxy |
| **Deployment** | WSL (Linux) | Native Linux environment |

---

## Directory Structure

```
WBServer/server/
├── package.json                      # Server dependencies + ESM type
├── src/
│   ├── index.js                      # Express entry (routes, middleware, auth)
│   ├── config.js                     # Hardcoded LLM config (all users share)
│   ├── middleware/
│   │   └── authMiddleware.js         # JWT validation middleware
│   ├── routes/
│   │   ├── auth.js                   # POST /login, GET /me
│   │   ├── files.js                  # File CRUD + upload + serve images (user-scoped)
│   │   ├── llm.js                    # LLM proxy (chat, models, test)
│   │   └── settings.js               # Per-user settings get/save/test (orphaned in v2.1)
│   └── services/
│       ├── authService.js            # JWT auth, user management, workspace init
│       ├── fileService.js            # FileSystem ops scoped to user workspace
│       └── llmService.js             # OpenAI-compatible API client
└── data/
    ├── users.json                    # User registry (username → metadata)
    └── users/                        # Multi-user data root
        └── <username>/
            ├── workspace/            # User's game content
            │   ├── MainStory/
            │   ├── Quests/
            │   ├── Characters/
            │   └── World/
            ├── assets/images/        # User's uploaded images
            └── settings.json         # User's LLM settings (orphaned in v2.1)
```

---

## Authentication System (v2.0)

### Overview

WBUI uses **passwordless username-only authentication** with JWT tokens. Users simply type a username to enter their workspace. New users are created automatically on first login.

### How It Works

1. **Login flow**: User enters username → `POST /api/auth/login` → server creates/updates user entry → returns JWT token
2. **Token storage**: JWT stored in browser `localStorage` under key `wbu_token` (frontend concern)
3. **Token validation**: Server validates JWT via `authMiddleware` on every protected request
4. **Token expiry**: 7 days, configurable via `TOKEN_EXPIRY` in `authService.js`
5. **Session restore**: Frontend calls `GET /api/auth/me` to validate token on page load
6. **401 handling**: Frontend clears localStorage and redirects to `/login` on 401 responses

### Username Rules

- 2-30 characters
- Lowercase letters, numbers, hyphens, underscores only (`^[a-zA-Z0-9_-]+$`)
- Auto-lowercased and trimmed
- New users created automatically on first login

### User Data Structure (`data/users.json`)

```json
{
  "alice": {
    "username": "alice",
    "createdAt": "2026-04-29T12:00:00.000Z",
    "lastLogin": "2026-04-29T12:30:00.000Z"
  },
  "bob": {
    "username": "bob",
    "createdAt": "2026-04-29T11:00:00.000Z",
    "lastLogin": "2026-04-29T12:25:00.000Z"
  }
}
```

### Per-User Data Isolation

Each user gets a dedicated directory structure:

```
data/users/<username>/
├── workspace/          # Game content (default folders created on login)
│   ├── MainStory/
│   ├── Quests/
│   ├── Characters/
│   └── World/
├── assets/images/      # Uploaded images
└── settings.json       # LLM configuration (orphaned in v2.1)
```

**All file operations are scoped to the authenticated user's workspace.** The `fileService` uses `getSafePath()` which resolves paths relative to the user's workspace root and rejects any path traversal attempts.

### Security Considerations

- JWT secret is configurable via `JWT_SECRET` environment variable (default: `wbu-secret-key-change-in-production`)
- Path traversal protection in `fileService.getSafePath()` prevents escaping user workspace
- CORS currently allows all origins (private/local system) — may restrict in production
- No password hashing needed (passwordless design)

---

## API Endpoints

### Authentication

| Method | Endpoint | Auth | Description | Body/Response |
|--------|----------|------|-------------|---------------|
| `POST` | `/api/auth/login` | Public | Login or create user | Body: `{ username }` → `{ token, user }` |
| `GET` | `/api/auth/me` | Required | Get current user info | → `{ user: { username } }` |

### File Operations

| Method | Endpoint | Auth | Description | Query/Body |
|--------|----------|------|-------------|------------|
| `GET` | `/api/files` | Required | List files/folders in directory | `?path=folder/path` |
| `GET` | `/api/files/content` | Required | Get file content | `?path=file/path` |
| `POST` | `/api/files` | Required | Create file or folder | `{ path, type, content? }` |
| `PUT` | `/api/files` | Required | Update file content | `{ path, content }` |
| `DELETE` | `/api/files` | Required | Delete file or folder | `?path=item/path` |
| `POST` | `/api/files/rename` | Required | Rename file/folder | `{ path, name }` |
| `POST` | `/api/files/upload` | Required | Upload image (multipart) | `image` field |
| `GET` | `/api/files/images/:filename` | Required | Serve uploaded image | — |

### LLM Operations

| Method | Endpoint | Auth | Description | Body |
|--------|----------|------|-------------|------|
| `GET` | `/api/llm/models` | Required | Fetch models from LLM server | — |
| `POST` | `/api/llm/chat` | Required | Send chat message with document context | `{ message, documentContent?, model?, messages? }` |
| `POST` | `/api/llm/test` | Required | Test LLM connection | settings object |

### Settings (Per-User) — Orphaned in v2.1

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `GET` | `/api/settings` | Required | Get current user's LLM settings |
| `POST` | `/api/settings` | Required | Save current user's LLM settings |
| `POST` | `/api/settings/test` | Required | Test connection with provided settings |

### System

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `GET` | `/api/health` | Public | Health check (`{ status: "ok", timestamp }`) |

---

## Server Configuration (`config.js`)

LLM settings are hardcoded in `server/src/config.js`. Current configuration points to a local llama.cpp server on port 8000:

```js
llm: {
  baseUrl: 'http://localhost:8000/v1',
  apiKey: 'dummy',
  modelName: 'qwen3.6-27b',
  maxTokens: 4096,
  temperature: 0.7,
}
```

| Field | Description |
|-------|-------------|
| `baseUrl` | OpenAI-compatible API endpoint (current: `http://localhost:8000/v1` for llama.cpp) |
| `apiKey` | API key (use placeholder if not needed) |
| `modelName` | Model name to use for chat (current: `qwen3.6-27b`) |
| `maxTokens` | Max tokens in responses |
| `temperature` | Creativity (0.0–2.0) |

**LLM Server Management**: The llama.cpp server (port 8000) running `Qwen3.6-27B-Q6_K.gguf` is managed separately via `Nymphs-Brain/bin/lms-start` and `Nymphs-Brain/bin/lms-stop`. It must be running before the WBUI AI chat will function.

---

## Services

### authService.js
- JWT token generation and verification
- User creation and lookup in `data/users.json`
- Automatic workspace initialization on first login (creates default folder structure)
- Last-login timestamp updates

### fileService.js
- File CRUD operations scoped to authenticated user's workspace
- `getSafePath()` — resolves and sanitizes paths to prevent directory traversal attacks
- Directory listing with type detection (file vs folder)
- File/folder creation, renaming, deletion
- Image upload handling (via Multer) and serving
- Recursive folder deletion

### llmService.js
- OpenAI-compatible API client (ESM)
- Proxies chat requests to hardcoded LLM server
- Injects document context into system prompts
- Model listing from upstream LLM server
- Connection testing

### authMiddleware.js
- JWT validation middleware
- Extracts and verifies Bearer token from `Authorization` header
- Attaches `req.user` with username to request object
- Returns 401 on missing/invalid token

---

## Orphaned Code (v2.0 artifacts)

| File | Status |
|------|--------|
| `server/src/routes/settings.js` | Orphaned — still mounted in index.js but unused in v2.1 |

This route was not deleted to allow easy reversion if user-configurable LLM settings are desired in the future. LLM config is now hardcoded in `server/src/config.js`.

---

## Status

### Completed

- [x] Backend: Express server with ESM modules
- [x] Backend: Passwordless JWT auth system (username only, no password)
- [x] Backend: Auth middleware with JWT validation
- [x] Backend: User management service (create, verify, workspace init)
- [x] Backend: Multi-user data isolation (per-user workspace/assets/settings)
- [x] Backend: File CRUD routes + image upload/serve (user-scoped)
- [x] Default folder template (MainStory, Quests, Characters, World) created on login
- [x] Backend: LLM proxy routes (chat, models, test)
- [x] Backend: Per-user settings routes with disk persistence + cache
- [x] Backend: File service with user-scoped path traversal protection
- [x] Backend: LLM service (ESM)
- [x] ESM module system across entire backend

### Bugs Fixed (v2.0)

- [x] **Missing authMiddleware on /me route** — `GET /api/auth/me` in `auth.js` was missing `authMiddleware`, so `req.user` was always `undefined`, causing the endpoint to always return 401. This created a login loop: login succeeds → token saved → `useAuth` fires `getMe()` → 401 → token cleared → back to login. **Fix**: Added `authMiddleware` to the `/me` route: `router.get('/me', authMiddleware, (req, res) => { ... })`.

### Remaining (Future Enhancements)

- [ ] Password-based auth option (for higher security)
- [ ] Token refresh mechanism (currently 7-day single token)
- [ ] Consider upgrading multer from 1.x to 2.x (current: 1.4.5-lts.1 stable)
- [ ] CORS restriction for production deployment
- [ ] Git version integration hooks
- [ ] File conflict resolution / locking

---

## Known Issues

1. **Multer 1.x**: Currently on `1.4.5-lts.1` which shows a security advisory for version 1.x. The LTS variant is stable, and multer 2.x is available but may require API changes.
2. **JWT secret hardcoded in dev**: Default secret `wbu-secret-key-change-in-production` — set `JWT_SECRET` env var for production.
3. **CORS open**: Currently allows all origins (`*`) — appropriate for local use but should be restricted for network exposure.
4. **No password option**: Current auth is username-only — anyone who knows a username can access that user's workspace.
5. **Token expiry**: 7-day fixed expiry with no refresh mechanism — users must re-login after 7 days.
6. **No file conflict resolution**: If multiple tabs/devices edit the same file, last write wins.

---

## Quick Start

```bash
# Install dependencies (if not already installed)
cd WBServer
npm install

# Start backend (Terminal 1)
cd server && npm run dev

# Access:
# Backend API: http://localhost:8082
# Health check: http://localhost:8082/api/health
```

### Verify Everything Works

```bash
# Test health endpoint (public)
curl http://localhost:8082/api/health
# Expected: {"status":"ok","timestamp":"..."}

# Test login (creates user if new)
curl -X POST http://localhost:8082/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser"}'
# Expected: {"token":"eyJ...","user":{"username":"testuser","isNewUser":true,...}}

# Test file listing (requires auth token from login response)
TOKEN="<token from login response>"
curl http://localhost:8082/api/files \
  -H "Authorization: Bearer $TOKEN"
# Expected: JSON array with user's folder structure

# Test settings (requires auth)
curl http://localhost:8082/api/settings \
  -H "Authorization: Bearer $TOKEN"
# Expected: settings object with default values
```

### Stop / Start Helpers

Scripts available in `Nymphs-Brain/bin/`:

```bash
wbu-stop    # Stop both frontend and backend servers
wbu-start   # Start both servers
wbu-status  # Check server status
```

### LLM Server (Required for AI Chat)

The AI chat requires the llama.cpp server to be running on port 8000. Start it separately:

```bash
# Start the LLM server (loads Qwen3.6-27B-Q6_K.gguf)
Nymphs-Brain/bin/lms-start

# Stop the LLM server
Nymphs-Brain/bin/lms-stop
```

**Full startup order**: Start the LLM server first, then the WBUI servers:
1. `Nymphs-Brain/bin/lms-start` (LLM server on port 8000)
2. `Nymphs-Brain/bin/wbu-start` (WBUI frontend + backend on ports 5173/8082)

---

## Git Workflow

**Always commit and push changes to the `rauty` branch.** Never push directly to `main`.

```bash
git checkout rauty
git add -A
git commit -m "descriptive commit message"
git push origin rauty
```

## Changelog

### v2.3 — Image Upload Fix + Full-Width Images (2026-04-29)

**Image upload fix**: `POST /api/files/upload` switched from `multer.diskStorage` to `multer.memoryStorage` so the `folder` form field can be read after multer finishes parsing. File buffer is manually written to `assets/{folder}/filename.png` using `fs.writeFile()`.

**New function**: Added `saveImageFromBuffer(username, buffer, originalname, folderPath, mimeType)` to `fileService.js` to handle writing the in-memory file buffer to the correct user subdirectory.

**Full-width images** (client-side): Images in the editor now render at `width: 100%` by default. Click any image to toggle a compact `.reduced` style (`max-width: 300px`). Toggle persists in saved HTML.

**Files modified**:
- `server/src/routes/files.js` — memoryStorage + manual buffer write
- `server/src/services/fileService.js` — new `saveImageFromBuffer()` function

###

### v2.2 — Image Upload with Folder Support (2026-04-29)

`POST /api/files/upload` now accepts an optional `folder` form field to save the image in a specific subdirectory within the user's assets folder. `fileService.saveImage()` updated to create and write to `assets/{folder}/` subdirectories.

### v2.1 — Simplified LLM Config (2026-04-29)

LLM server configuration is now hardcoded in `config.js`. All users share the same LLM settings.

### v2.0 — Multi-user Authentication (2026-04-29)

Username-only passwordless login with JWT tokens. Per-user workspace isolation.

### v1.0 — Initial Release

Express REST API with file CRUD, LLM proxy, and static file serving.

## Design Principles

1. **Local-First** — All data stays on the user's machine
2. **Multi-User** — Isolated workspaces, per-user data, passwordless auth
3. **Flexible LLM** — Supports any OpenAI-compatible endpoint
4. **Secure by Default** — Path traversal protection, JWT auth
5. **Frictionless Login** — No passwords to forget, no accounts to manage