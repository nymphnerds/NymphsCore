# WORLD BUILDER UI — Client Handoff Document

**Generated**: 2026-04-29
**Scope**: Frontend (React + TypeScript)

**WBUI Client** is a React 18 + TypeScript web application for game developers to write and manage main story quests, side quests, character profiles, lore, and related assets. It features a modern VSCode-inspired 3-panel layout with integrated AI chat powered by a local LLM server.

The application is **local-first**: all game content stays on the user's machine. The AI chat panel provides context-aware assistance by including the current document content in prompts sent to the local LLM server via the Express backend.

**v2.0 Multi-user**: Users authenticate via username-only passwordless login. Each user gets their own isolated workspace.

**v2.1 Simplified LLM Config**: No user-facing settings page — LLM configuration is hardcoded on the server.

---

## Architecture Overview

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
                        (Express Backend :8082)
```

### Key Architectural Decisions

- **React hooks** encapsulate API calls (`useFiles`, `useLLM`, `useAuth`)
- **TypeScript types** defined in `client/src/services/api.ts`
- **401 auto-redirect** — frontend clears token and redirects to `/login` on auth failure
- **Vite** as build tool with HMR for fast development
- **Tailwind CSS** with dark theme using CSS variables

---

## Tech Stack

| Layer | Technology | Purpose |
|-------|------------|---------|
| **Frontend Framework** | React 18 + TypeScript | Type-safe component architecture |
| **Build Tool** | Vite | Fast HMR development server |
| **CSS Framework** | Tailwind CSS + tailwindcss-animate | Utility-first styling with animations |
| **Icons** | Lucide React | Consistent icon library |
| **Markdown** | react-markdown | Document preview rendering |
| **HTTP Client** | Fetch API | Frontend→Backend communication |
| **IDE** | Visual Studio Code | Development environment |

---

## Directory Structure

```
WBServer/client/
├── package.json                      # Client dependencies
├── index.html                        # Vite entry HTML
├── vite.config.ts                    # Vite config (proxy, port 5173)
├── tsconfig.json                     # TypeScript config
├── tsconfig.node.json                # Node-type config for Vite
├── tailwind.config.js                # Tailwind + dark theme + CSS variables
├── postcss.config.js                 # PostCSS for Tailwind
└── src/
    ├── main.tsx                      # React entry (ErrorBoundary wrapper)
    ├── App.tsx                       # Main 3-panel layout + auth gate
    ├── pages/
    │   ├── Login.tsx                 # Passwordless login page (username only)
    │   └── Settings.tsx              # Full LLM configuration page (orphaned in v2.1)
    ├── components/
    │   ├── FileExplorer.tsx          # Left panel: file tree + keyboard nav
    │   ├── DocumentEditor.tsx        # Center: textarea + markdown preview
    │   ├── ChatPanel.tsx             # Right: AI chat with history
    │   ├── Header.tsx                # Top toolbar (user, logout, settings, new file)
    │   ├── StatusBar.tsx             # Bottom status (path, word count, errors)
    │   ├── ImagePreview.tsx          # Modal image viewer with download
    │   └── ErrorBoundary.tsx         # React error boundary (class component)
    ├── hooks/
    │   ├── useAuth.ts                # Auth state, login/logout, token persistence
    │   ├── useFiles.ts               # File CRUD operations state management
    │   └── useLLM.ts                 # Chat + settings state management
    ├── services/
    │   └── api.ts                    # API client + TypeScript types + auth helpers
    └── styles/
        └── globals.css               # Tailwind imports + CSS variables + resizer styles
```

---

## 3-Panel UI Layout

The main app uses a VSCode-inspired layout with three resizable panels:

### Left Panel — FileExplorer
- File tree with folders and files
- Create, rename, delete operations
- Keyboard navigation (Arrow keys, Enter, Backspace)
- Active file highlighting
- Scoped to authenticated user's workspace

### Center Panel — DocumentEditor
- Plain text textarea for editing
- Markdown preview using `react-markdown`
- Split view toggle (edit mode / preview mode / split)
- Image drag-and-drop upload
- Inline image rendering in markdown preview

### Right Panel — ChatPanel
- AI chat message history
- User and AI message bubbles
- Document context injection (current file sent as context)
- Markdown rendering in AI responses
- Clear chat button

### Header — Top Toolbar
- App title and branding
- User display with username and avatar
- Logout button
- New file button

### Status Bar — Bottom Bar
- Current file path display
- Word count for active document
- Error message display
- LLM connection status indicator

---

## Components

### Login (`pages/Login.tsx`)
- Passwordless username-only authentication form
- Gradient background with WorldBuilder branding
- Client-side username validation (2-30 chars, alphanumeric + hyphens + underscores)
- Loading states during API call
- Error display for invalid usernames
- Auto-redirect to workspace on success
- New user creation message

### FileExplorer (`components/FileExplorer.tsx`)
- Recursive tree view with folders and files
- Create folder via "+" button in header
- Rename/delete via hover action buttons
- **Breadcrumb navigation bar** — Appears when inside a subfolder, shows:
  - Home icon button to jump directly to root
  - Clickable path segments for parent folders (e.g., `> Misc/SubFolder`)
  - Current folder displayed as non-clickable text
- Back button (ArrowLeft) in header for one-level-up navigation
- Keyboard navigation:
  - `ArrowDown` / `ArrowUp` — navigate list
  - `Enter` — select/open focused item
  - `Backspace` — delete focused item
- Focus resets when browsing to a new folder
- Sorted: folders first, then alphabetical
- Active file highlighting

### DocumentEditor (`components/DocumentEditor.tsx`)
- Textarea for plain text editing
- Markdown preview with `react-markdown`
- Three view modes: Edit only, Preview only, Split
- Create File button — prompts for filename, creates in current explorer folder
  - When at root level, prompts user to select a target folder first (numbered list)
- Drag-and-drop image upload
- Auto-save with debounce
- Word count tracked in status bar

### ChatPanel (`components/ChatPanel.tsx`)
- Message list with user/AI distinction
- User messages: right-aligned, accent color
- AI messages: left-aligned, with markdown rendering
- Textarea for composing messages
- Document context automatically injected
- Clear chat button
- Loading indicator during API calls

### Header (`components/Header.tsx`)
- App title ("WorldBuilder")
- New file button
- Current username display with avatar icon
- Logout button (red hover state)

### StatusBar (`components/StatusBar.tsx`)
- File path display
- Word count
- Error message area
- LLM connection status (⚠️ **Non-functional** — accepts `llmConnected` prop but `useLLM` hook does not expose connection state, always shows "Disconnected")

### ImagePreview (`components/ImagePreview.tsx`)
- Modal overlay for full-size image viewing
- Download button
- Click-outside-to-close behavior
- Truncated filename display

### ErrorBoundary (`components/ErrorBoundary.tsx`)
- React class component catching rendering errors
- Displays error message with "Try again" button
- Logs errors to console via `componentDidCatch`
- Integrated into `main.tsx` wrapping the entire app

---

## React Hooks

### useAuth (`hooks/useAuth.ts`)
**Purpose**: Authentication state management

- `isAuthenticated` — boolean
- `user` — current user object (`{ username: string }`)
- `login(username)` — POST to `/api/auth/login`, stores token in localStorage
- `logout()` — clears localStorage, resets state
- Token persistence: reads `wbu_token` and `wbu_user` from localStorage on mount
- Session restore: validates token via `GET /api/auth/me` on mount
- 401 handling: on invalid token, clears localStorage and redirects to `/login`

### useFiles (`hooks/useFiles.ts`)
**Purpose**: File CRUD operations state management

- `files` — array of file/folder items in current directory
- `currentPath` — current browsing path in workspace
- `selectedFile` — currently open file
- `fileContent` — content of selected file
- `loading` — boolean
- `error` — error message string
- `listFiles(path)` — GET `/api/files?path=...`
- `createFile(path, name)` — POST `/api/files`
- `createFolder(path, name)` — POST `/api/files`
- `updateFile(path, content)` — PUT `/api/files`
- `deleteItem(path)` — DELETE `/api/files`
- `renameItem(path, newName)` — POST `/api/files/rename`
- `uploadImage(file)` — POST `/api/files/upload` (multipart)

### useLLM (`hooks/useLLM.ts`)
**Purpose**: AI chat and LLM operations state management

- `messages` — chat history array (in-memory, cleared on refresh)
- `loading` — boolean
- `error` — error message string
- `sendMessage(text)` — POST `/api/llm/chat` with document context
- `clearChat()` — clears message history
- `fetchModels()` — GET `/api/llm/models`
- Document context: includes current file content as system prompt

---

## API Client (`services/api.ts`)

### TypeScript Types

```typescript
interface User {
  username: string;
  createdAt?: string;
  lastLogin?: string;
  isNewUser?: boolean;
}

interface FileItem {
  name: string;
  path: string;
  type: 'file' | 'folder';
  content?: string;
}

interface ChatMessage {
  role: 'user' | 'assistant' | 'system';
  content: string;
}
```

### Auth Helpers

| Function | Description |
|----------|-------------|
| `authHeaders()` | Returns `{ 'Authorization': 'Bearer <token>' }` from localStorage |
| `authFetch(url, options)` | Fetch wrapper that includes auth headers; clears localStorage and redirects to `/login` on 401 (only when token was present) |

### API Functions

| Function | Method | Endpoint | Description |
|----------|--------|----------|-------------|
| `login(username)` | POST | `/api/auth/login` | Returns `{ token, user }` |
| `getMe()` | GET | `/api/auth/me` | Returns `{ user }` |
| `listFiles(path?)` | GET | `/api/files?path=...` | Returns file array |
| `getFileContent(path)` | GET | `/api/files/content?path=...` | Returns file content |
| `createFile(body)` | POST | `/api/files` | Create file/folder |
| `updateFile(body)` | PUT | `/api/files` | Update file content |
| `deleteFile(path)` | DELETE | `/api/files?path=...` | Delete file/folder |
| `renameFile(body)` | POST | `/api/files/rename` | Rename item |
| `uploadImage(file)` | POST | `/api/files/upload` | Multipart upload |
| `sendMessage(body)` | POST | `/api/llm/chat` | Chat with document context |
| `fetchModels()` | GET | `/api/llm/models` | List available models |
| `testConnection(body)` | POST | `/api/llm/test` | Test LLM connection |

### localStorage Keys

| Key | Value | Description |
|-----|-------|-------------|
| `wbu_token` | JWT string | Authentication token |
| `wbu_user` | JSON string | `{ username: string }` |

---

## Frontend Auth Flow

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

---

## Keyboard Navigation

### File Explorer
- `ArrowDown` / `ArrowUp` — navigate file/folder list
- `Enter` — select/open focused item
- `Backspace` — delete focused item
- Focus resets when browsing to a new folder

### Global Shortcuts
- `Ctrl+S` / `Cmd+S` — save current document

---

## Orphaned Code (v2.0 artifacts)

| File | Status |
|------|--------|
| `client/src/pages/Settings.tsx` | Orphaned — not rendered in App.tsx, no route |

This page was not deleted to allow easy reversion if user-configurable LLM settings are desired in the future. LLM config is now hardcoded in `server/src/config.js`.

---

## Status

### Completed

- [x] Project structure and config files (Vite, TypeScript, Tailwind, PostCSS)
- [x] Frontend: Login page (passwordless, username validation)
- [x] Frontend: Auth hook (useAuth) with token persistence & session restore
- [x] Frontend: Auth gate in App.tsx (Login vs main app routing)
- [x] Frontend: All 5 core components (Explorer, Editor, Chat, Header, StatusBar)
- [x] Frontend: React hooks (useAuth, useFiles, useLLM)
- [x] Frontend: API client with TypeScript types + auth helpers
- [x] Frontend: Header with user display + logout button
- [x] Frontend: 401 auto-redirect to login page
- [x] Frontend: Dark theme with Tailwind CSS
- [x] Frontend: ImagePreview component
- [x] Frontend: ErrorBoundary component
- [x] Keyboard navigation for file explorer
- [x] Keyboard shortcuts (Ctrl+S save)
- [x] TypeScript compilation (zero errors)

### Bugs Fixed (v2.0)

- [x] **Infinite login refresh loop** — `authFetch()` in `api.ts` was redirecting to `/login` on ANY 401 response, even when no token existed. Because `useFiles()` and `useLLM()` hooks in `App.tsx` make API calls on mount regardless of auth state, the 401 from unauthenticated requests triggered `window.location.href = '/login'`, causing a full page reload. **Fix**: `authFetch()` now only redirects on 401 when a token was present (`if (response.status === 401 && token)`).

- [x] **"Rendered more hooks than during the previous render" error** — The early return `if (!isAuthenticated) return <Login />` in `App.tsx` was placed between React hooks (after `useFiles`/`useLLM` but before `useCallback`/`useEffect`). React's Rules of Hooks require all hooks to be called in the same order every render. **Fix**: Moved the auth check to after all hooks, so all hooks run every render regardless of auth state.

- [x] **Can't navigate back to root in file explorer** — After entering a subfolder, the back button in the Explorer header was small and easy to miss, with no visible breadcrumb showing the current folder path. **Fix**: Added a breadcrumb navigation bar that renders when inside a subfolder, with a Home icon to return to root and clickable path segments for parent folders. Added `handleBackToRoot` and `handleBackToSegment` callbacks in `App.tsx`.

### Remaining (Future Enhancements)

- [ ] Drag-and-drop file reordering in explorer
- [ ] Right-click context menu in file explorer
- [ ] Search across all files
- [ ] Undo/redo for document editor
- [ ] Multiple document tabs
- [ ] Export to PDF/HTML
- [ ] Chat history persistence (currently in-memory only)
- [ ] Re-enable Settings page if user-configurable LLM is desired

---

## Known Issues

1. **Chat history is in-memory**: Cleared on page refresh (no persistence).
2. **No multi-tab editing**: Only one document open at a time.
3. **LLM status indicator non-functional**: `StatusBar` accepts `llmConnected` prop but `useLLM` hook does not expose connection state, so it always shows "LLM Disconnected".
4. **No file conflict resolution**: If multiple tabs/devices edit the same file, last write wins.

---

## Quick Start

```bash
# Install dependencies (if not already installed)
cd WBServer
npm install

# Start frontend (Terminal 2)
cd client && npm run dev

# Access:
# Frontend: http://localhost:5173
```

### Full Startup (with Backend)

```bash
# Terminal 1 - Backend
cd WBServer/server && npm run dev

# Terminal 2 - Frontend
cd WBServer/client && npm run dev

# Or use helper scripts:
Nymphs-Brain/bin/wbu-start    # Start both servers
Nymphs-Brain/bin/wbu-stop     # Stop both servers
Nymphs-Brain/bin/wbu-status   # Check status
```

### LLM Server (Required for AI Chat)

The AI chat requires the llama.cpp server to be running on port 8000:

```bash
Nymphs-Brain/bin/lms-start    # Start LLM server
Nymphs-Brain/bin/lms-stop     # Stop LLM server
```

**Full startup order**:
1. `Nymphs-Brain/bin/lms-start` (LLM server on port 8000)
2. `Nymphs-Brain/bin/wbu-start` (WBUI frontend + backend on ports 5173/8082)

---

## Configuration Files

### vite.config.ts
- Dev server port: 5173
- API proxy: `/api` → `http://localhost:8082`
- HMR enabled

### tailwind.config.js
- Dark mode via CSS variables
- Custom color palette for dark theme
- Animation support via `tailwindcss-animate`

### tsconfig.json
- Strict mode enabled
- JSX: `react-jsx`
- Module: `ESNext`, ESM targeting

### postcss.config.js
- Tailwind CSS + PostCSS loader

---

## Design Principles

1. **Modern & Clean** — Dark theme, subtle borders, consistent spacing
2. **Intuitive** — VSCode-like layout familiar to developers
3. **Local-First** — All data stays on the user's machine
4. **Image-First** — Images are first-class citizens in documents
5. **Frictionless Login** — No passwords to forget, no accounts to manage