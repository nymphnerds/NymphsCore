# WORLD BUILDER UI — Project Analysis & Handoff Document

**Generated**: 2026-04-29
**Status**: This combined handoff has been split into two focused documents:
- 📄 **[Server Handoff](./WORLD_BUILDER_SERVER_HANDOFF.md)** — Backend (Express.js REST API)
- 📄 **[Client Handoff](./WORLD_BUILDER_CLIENT_HANDOFF.md)** — Frontend (React + TypeScript)

> **Note**: This document is retained for reference. For team-specific onboarding, use the split handoff documents linked above.

**WBUI** (WorldBuilder UI) is a local, self-hosted multi-user web application for game developers to write and manage main story quests, side quests, character profiles, lore, and related assets. It features a modern VSCode-inspired 3-panel layout with integrated AI chat powered by a hardcoded local LLM server (OpenAI-compatible).

The application is **local-first**: all game content stays on the user's machine, stored in per-user directories on the local filesystem. The AI chat panel provides context-aware assistance by including the current document content in prompts sent to the local LLM server.

**v2.0 Multi-user**: Users authenticate via username-only passwordless login (JWT-based). Each user gets their own isolated workspace and assets. No passwords required — type a username and enter.

**v2.1 Simplified LLM Config**: LLM server configuration is hardcoded in `server/src/config.js` rather than being user-configurable. There is no settings page — the admin configures the LLM once in the server config and all users share it. This reduces UI complexity and eliminates per-user settings management.

---

## 2. Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        Browser (Port 5173)                      │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │                    Login Page (if not authenticated)         ││
│  └─────────────────────────────────────────────────────────────┘│
│  ┌─────────────┬────────────────────────┬──────────────────────┐│
│  │ File/Folder │   Document Editor      │   AI Chat Panel      ││
│  │ Explorer    │   (Markdown + Preview) │   (Context-Aware)    ││
│  │ (Left)      │   (Center)             │   (Right)            ││
│  └─────────────┴────────────────────────┴──────────────────────┘│
│            Header (user, logout, new file)  │  Status Bar       │
└─────────────────────────────────────────────────────────────────┘
                               │ HTTP/Fetch (Bearer JWT)
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
- **JWT authentication** — passwordless login, 7-day token expiry, stored in localStorage
- **Per-user data isolation** — each user gets `data/users/<username>/workspace/`, `assets/`, `settings.json`
- **Path traversal protection** in file service (prevents escaping user workspace)
- **Shared LLM config** hardcoded in `config.js` — all users share the same LLM server (no per-user settings)
- **React hooks** encapsulate API calls (`useFiles`, `useLLM`, `useAuth`)
- **TypeScript types** defined in `client/src/services/api.ts`
- **401 auto-redirect** — frontend clears token and redirects to `/login` on auth failure

---

## 3. Tech Stack

| Layer | Technology | Purpose |
|-------|------------|---------|
| **Frontend Framework** | React 18 + TypeScript | Type-safe component architecture |
| **Build Tool** | Vite | Fast HMR development server |
| **CSS Framework** | Tailwind CSS + tailwindcss-animate | Utility-first styling with animations |
| **Icons** | Lucide React | Consistent icon library |
| **Markdown** | react-markdown | Document preview rendering |
| **Backend** | Node.js + Express | REST API server |
| **Auth** | jsonwebtoken | JWT token generation & verification |
| **Module System** | ESM (`"type": "module"`) | Modern import/export syntax |
| **File Upload** | Multer 1.4.5-lts.1 | Multipart image uploads |
| **HTTP Client** | Fetch API | Backend→LLM proxy, Frontend→Backend |
| **Deployment** | WSL (Linux) | Native Linux environment |

---

## 4. Directory Structure

```
WBServer/
├── package.json                          # Root workspace config
├── WBUI_HANDOFF.md                       # Original handoff doc
├── server/
│   ├── package.json                      # Server dependencies + ESM type
│   ├── src/
│   │   ├── index.js                      # Express entry (routes, middleware, auth)
│   │   ├── config.js                     # Hardcoded LLM config (all users share)
│   │   ├── middleware/
│   │   │   └── authMiddleware.js         # JWT validation middleware
│   │   ├── routes/
│   │   │   ├── auth.js                   # POST /login, GET /me
│   │   │   ├── files.js                  # File CRUD + upload + serve images (user-scoped)
│   │   │   ├── llm.js                    # LLM proxy (chat, models, test)
│   │   │   └── settings.js               # Per-user settings get/save/test
│   │   └── services/
│   │       ├── authService.js            # JWT auth, user management, workspace init
│   │       ├── fileService.js            # FileSystem ops scoped to user workspace
│   │       └── llmService.js             # OpenAI-compatible API client
│   └── data/
│       └── users/                        # Multi-user data root
│           └── <username>/
│               ├── workspace/            # User's game content
│               │   ├── MainStory/
│               │   ├── Quests/
│               │   ├── Characters/
│               │   └── World/
│               ├── assets/images/        # User's uploaded images
│               └── settings.json         # User's LLM settings
├── client/
│   ├── package.json                      # Client dependencies
│   ├── index.html                        # Vite entry HTML
│   ├── vite.config.ts                    # Vite config (proxy, port 5173)
│   ├── tsconfig.json                     # TypeScript config
│   ├── tsconfig.node.json                # Node-type config for Vite
│   ├── tailwind.config.js                # Tailwind + dark theme + CSS variables
│   ├── postcss.config.js                 # PostCSS for Tailwind
│   └── src/
│       ├── main.tsx                      # React entry (ErrorBoundary wrapper)
│       ├── App.tsx                       # Main 3-panel layout + auth gate
│       ├── pages/
│       │   ├── Login.tsx                 # Passwordless login page (username only)
│       │   └── Settings.tsx              # Full LLM configuration page
│       ├── components/
│       │   ├── FileExplorer.tsx          # Left panel: file tree + keyboard nav
│       │   ├── DocumentEditor.tsx        # Center: textarea + markdown preview
│       │   ├── ChatPanel.tsx             # Right: AI chat with history
│       │   ├── Header.tsx                # Top toolbar (user, logout, settings, new file)
│       │   ├── StatusBar.tsx             # Bottom status (path, word count, errors)
│       │   ├── ImagePreview.tsx          # Modal image viewer with download
│       │   └── ErrorBoundary.tsx         # React error boundary (class component)
│       ├── hooks/
│       │   ├── useAuth.ts                # Auth state, login/logout, token persistence
│       │   ├── useFiles.ts               # File CRUD operations state management
│       │   └── useLLM.ts                 # Chat + settings state management
│       ├── services/
│       │   └── api.ts                    # API client + TypeScript types + auth helpers
│       └── styles/
│           └── globals.css               # Tailwind imports + CSS variables + resizer styles
```

---

## 5. Authentication System (v2.0)

### Overview

WBUI uses **passwordless username-only authentication** with JWT tokens. Users simply type a username to enter their workspace. New users are created automatically on first login.

### How It Works

1. **Login flow**: User enters username → `POST /api/auth/login` → server creates/updates user entry → returns JWT token
2. **Token storage**: JWT stored in browser `localStorage` under key `wbu_token`
3. **User info**: User object stored in `localStorage` under key `wbu_user`
4. **Auth requests**: All API requests (except login/health) include `Authorization: Bearer <token>` header
5. **Token validation**: Server validates JWT via `authMiddleware` on every protected request
6. **Token expiry**: 7 days, configurable via `TOKEN_EXPIRY` in `authService.js`
7. **Session restore**: On page load, `useAuth` checks for existing token, validates via `GET /api/auth/me`
8. **401 handling**: Frontend `authFetch()` clears localStorage and redirects to `/login` on 401 responses
9. **Logout**: Clears localStorage, resets auth state, returns to login page

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
└── settings.json       # LLM configuration (URL, model, API key, etc.)
```

**All file operations are scoped to the authenticated user's workspace.** The `fileService` uses `getSafePath()` which resolves paths relative to the user's workspace root and rejects any path traversal attempts.

### Security Considerations

- JWT secret is configurable via `JWT_SECRET` environment variable (default: `wbu-secret-key-change-in-production`)
- Path traversal protection in `fileService.getSafePath()` prevents escaping user workspace
- CORS currently allows all origins (private/local system) — may restrict in production
- No password hashing needed (passwordless design)

---

## 6. API Endpoints

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

### Settings (Per-User)

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

## 7. AI/LLM Integration

### How It Works (v2.1)

1. **Admin configures** the LLM server once in `server/src/config.js` (base URL, model, API key, temperature, max tokens)
2. **All users share** the same LLM configuration — no per-user settings
3. **Chat context**: When user sends a message, the current document content is included as a system/context prompt
4. **Conversation history**: Maintained per session in the `useLLM` hook (in-memory chat history)

### Server Configuration (`config.js`)

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

**To re-enable user-configurable LLM settings**: Restore the settings routes, `Settings.tsx` page, and the `getSettings`/`saveSettings`/`testConnection` API functions.

---

## 8. Key Features Implemented

### Login Page
- Passwordless username-only authentication
- Gradient background with WorldBuilder branding
- Client-side username validation (length, characters)
- Loading states during login
- Auto-redirect to workspace on success
- New user creation message

### File Explorer (Left Panel)
- Tree view with folders and files
- Create, rename, delete operations
- **+ folder button** in header — prompts for folder name, creates in current explorer path, refreshes file list
- Keyboard navigation (ArrowUp/Down, Enter, Backspace)
- Back button for folder navigation
- Hover actions (rename/delete buttons)
- Active file highlighting
- Sorted: folders first, then alphabetical
- Scoped to authenticated user's workspace

### Document Editor (Center Panel)
- Plain text textarea for editing
- Markdown preview using `react-markdown`
- Split view toggle
- **Create File button** — prompts for filename, creates file in current explorer folder, auto-opens editor; **when at root level, prompts user to select a target folder first** (lists available folders numbered, user enters number)
- Drag-and-drop image upload
- Inline image rendering in preview
- Word count displayed in status bar

### AI Chat Panel (Right Panel)
- Message history display
- User and AI message bubbles
- Document context injection
- Loading states during API calls
- Clear chat button
- Markdown rendering in AI responses

### Header / Toolbar
- App title and branding
- **User display** with username and avatar icon
- **Logout button** (red hover state)
- New file button only (no folder button — folders are predefined)

### Status Bar
- Current file path display
- Word count for active document
- Error message display
- LLM Connection status indicator (⚠️ **Non-functional** — `StatusBar` accepts `llmConnected` prop but `useLLM` hook does not expose connection state, so it always shows "LLM Disconnected")

### ImagePreview Component
- Modal overlay for full-size image viewing
- Download button to save images
- Click-outside-to-close behavior
- Truncated filename display

### ErrorBoundary Component
- React class component that catches rendering errors
- Displays error message with "Try again" button
- Logs errors to console via `componentDidCatch`
- Integrated into `main.tsx` wrapping the entire app

### Keyboard Navigation (File Explorer)
- `ArrowDown` / `ArrowUp` — navigate file/folder list
- `Enter` — select/open focused item
- `Backspace` — delete focused item
- Focus resets when browsing to a new folder

### Keyboard Shortcuts (Global)
- `Ctrl+S` / `Cmd+S` — save current document

---

## 9. Frontend Auth Flow

```
Page Load
    │
    ▼
useAuth() checks localStorage for wbu_token
    │
    ├─ No token ──► Show Login page
    │
    └─ Token found ──► GET /api/auth/me (validate token)
                          │
                          ├─ 200 ──► Show App (user authenticated)
                          │
                          └─ 401 ──► Clear localStorage, Show Login page
```

### Frontend Auth Components

| File | Purpose |
|------|---------|
| `pages/Login.tsx` | Login page component (username form, error display) |
| `hooks/useAuth.ts` | Auth state management (login, logout, token persistence, session restore) |
| `services/api.ts` | `login()`, `getMe()`, `authHeaders()`, `authFetch()` with 401 redirect |
| `App.tsx` | Auth gate — renders `Login` when `!isAuthenticated`, main app otherwise |
| `components/Header.tsx` | User display + logout button |

### localStorage Keys

| Key | Value | Description |
|-----|-------|-------------|
| `wbu_token` | JWT string | Authentication token |
| `wbu_user` | JSON string | `{ username: string }` |

---

## 10. Status

### Completed

- [x] Project structure and config files (Vite, TypeScript, Tailwind, PostCSS)
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
- [x] Frontend: Login page (passwordless, username validation)
- [x] Frontend: Auth hook (useAuth) with token persistence & session restore
- [x] Frontend: Auth gate in App.tsx (Login vs main app routing)
- [x] Frontend: All 5 core components (Explorer, Editor, Chat, Header, StatusBar)
- [x] Frontend: Settings page (per-user LLM config)
- [x] Frontend: React hooks (useAuth, useFiles, useLLM)
- [x] Frontend: API client with TypeScript types + auth helpers
- [x] Frontend: Header with user display + logout button
- [x] Frontend: 401 auto-redirect to login page
- [x] Frontend: Dark theme with Tailwind CSS
- [x] Frontend: ImagePreview component
- [x] Frontend: ErrorBoundary component
- [x] Keyboard navigation for file explorer
- [x] Keyboard shortcuts (Ctrl+S save)
- [x] ESM module system across entire backend
- [x] TypeScript compilation (zero errors)
- [x] Both servers tested and running

### Bugs Fixed (v2.0)

- [x] **Infinite login refresh loop** — `authFetch()` in `api.ts` was redirecting to `/login` on ANY 401 response, even when no token existed. Because `useFiles()` and `useLLM()` hooks in `App.tsx` make API calls on mount regardless of auth state, the 401 from unauthenticated requests triggered `window.location.href = '/login'`, causing a full page reload that reset the Login component and cleared typed input. **Fix**: `authFetch()` now only redirects on 401 when a token was present (`if (response.status === 401 && token)`), so requests made without a token simply fail silently instead of triggering a redirect loop.

- [x] **"Rendered more hooks than during the previous render" error** — The early return `if (!isAuthenticated) return <Login />` in `App.tsx` was placed between React hooks (after `useFiles`/`useLLM` but before `useCallback` handlers and `useEffect` hooks). React's Rules of Hooks require all hooks to be called in the same order every render. When transitioning from unauthenticated to authenticated, more hooks were called, triggering this error. **Fix**: Moved the auth check to after all hooks, so all hooks run every render regardless of auth state.

- [x] **Login screen refreshes but never enters app** — The `/api/auth/me` route in `auth.js` was missing `authMiddleware`, so `req.user` was always `undefined`, causing the endpoint to always return 401. This created a loop: login succeeds → token saved → `useAuth` effect fires `getMe()` → 401 → `.catch()` calls `logout()` → token cleared → back to login screen. **Fix**: Added `authMiddleware` to the `/me` route in `routes/auth.js`: `router.get('/me', authMiddleware, (req, res) => { ... })`.

### Remaining (Future Enhancements)

- [ ] Drag-and-drop file reordering in explorer
- [ ] Right-click context menu in file explorer
- [ ] Search across all files
- [ ] Undo/redo for document editor
- [ ] Multiple document tabs
- [ ] Export to PDF/HTML
- [ ] Git version integration
- [ ] Chat history persistence (currently in-memory only)
- [ ] Password-based auth option (for higher security)
- [ ] Token refresh mechanism (currently 7-day single token)
- [ ] Consider upgrading multer from 1.x to 2.x (current: 1.4.5-lts.1 stable)
- [ ] CORS restriction for production deployment

---

## 11. Known Issues

1. **Multer 1.x**: Currently on `1.4.5-lts.1` which shows a security advisory for version 1.x. The LTS variant is stable, and multer 2.x is available but may require API changes.
2. **No multi-tab editing**: Only one document open at a time.
3. **Chat history is in-memory**: Cleared on page refresh (no persistence).
4. **No file conflict resolution**: If multiple tabs/devices edit the same file, last write wins.
5. **JWT secret hardcoded in dev**: Default secret `wbu-secret-key-change-in-production` — set `JWT_SECRET` env var for production.
6. **CORS open**: Currently allows all origins (`*`) — appropriate for local use but should be restricted for network exposure.
7. **No password option**: Current auth is username-only — anyone who knows a username can access that user's workspace.
8. **Token expiry**: 7-day fixed expiry with no refresh mechanism — users must re-login after 7 days.

---

## 12. Quick Start

```bash
# Install dependencies (if not already installed)
cd WBServer
npm install

# Start backend (Terminal 1)
cd server && npm run dev

# Start frontend (Terminal 2)
cd ../client && npm run dev

# Access:
# Frontend: http://localhost:5173
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

## 13. Orphaned Code (v2.0 artifacts)

The following files remain on disk from v2.0 (user-configurable LLM settings) but are **no longer used** in v2.1:

| File | Status |
|------|--------|
| `client/src/pages/Settings.tsx` | Orphaned — not rendered in App.tsx, no route |
| `server/src/routes/settings.js` | Orphaned — still mounted in index.js but unused |

These were not deleted to allow easy reversion if user-configurable LLM settings are desired in the future. LLM config is now hardcoded in `server/src/config.js`.

---

## 14. Design Principles

1. **Modern & Clean** — Dark theme, subtle borders, consistent spacing
2. **Intuitive** — VSCode-like layout familiar to developers
3. **Local-First** — All data stays on the user's machine
4. **Multi-User** — Isolated workspaces, per-user settings, passwordless auth
5. **Flexible LLM** — Supports any OpenAI-compatible endpoint
6. **Image-First** — Images are first-class citizens in documents
7. **Frictionless Login** — No passwords to forget, no accounts to manage