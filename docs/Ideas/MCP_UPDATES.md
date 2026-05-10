# MCP Updates — Context7, Unity MCP, and mcpo Proxy

> **Location:** `~/Nymphs-Brain/mcp/` (inside NymphsCore WSL distro)  
> **Author:** Rauty  
> **Purpose:** Document how Context7, Unity MCP, web-forager, and the mcpo proxy were set up and how the three-layer MCP architecture works.

---

## 1. MCP Infrastructure Overview

The Nymphs-Brain MCP infrastructure uses a **three-layer architecture** that allows a single set of MCP servers to serve multiple AI clients (Cline, Open WebUI, etc.) without running duplicate processes.

```
┌─────────────────────────────────────────────────────────────────────┐
│                    Layer 1: Native MCP Servers                      │
│              (stdio transport, defined in mcp-proxy-servers.json)   │
│                                                                     │
│  filesystem   ── @modelcontextprotocol/server-filesystem (npm)      │
│  memory       ── @modelcontextprotocol/server-memory (npm)          │
│  web-forager  ── Python package (mcp-venv)                          │
│  context7     ── @upstash/context7-mcp (npm)                        │
│  unity        ── Custom TypeScript server → WebSocket → Unity Editor│
└────────────────────────────────┬────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│              Layer 2: mcpo Proxy (stdio → HTTP Bridge)              │
│         (mcpo-venv, config: mcpo-servers.json, port 8100)           │
│                                                                     │
│  Exposes all 5 servers as HTTP endpoints:                           │
│  http://127.0.0.1:8100/servers/{server-name}/mcp                    │
└────────────────────────────────┬────────────────────────────────────┘
                                 │
              ┌──────────────────┼──────────────────┐
              ▼                  ▼                   ▼
┌──────────────────┐ ┌──────────────────┐ ┌──────────────────────┐
│   Layer 3a:       │ │   Layer 3b:      │ │   Layer 3c:          │
│   Cline Client    │ │   Open WebUI     │ │   Any MCP Client     │
│   (cline-mcp-     │ │   (streamable    │ │   (mcpo-servers.json │
│    settings.json) │ │    HTTP)         │ │    direct reference) │
└──────────────────┘ └──────────────────┘ └──────────────────────┘
```

### Why This Architecture?

| Problem | Solution |
|---|---|
| Each client needs its own MCP server processes | mcpo proxy bridges one set of stdio servers to HTTP |
| Servers restart when client disconnects | Proxy keeps servers alive across client sessions |
| Different clients use different transport types | Proxy normalizes: stdio → streamable-http |
| Unity MCP needs to stay connected to Unity Editor | Proxy maintains the single Unity connection |

---

## 2. mcpo — MCP Proxy

### 2.1 What is mcpo?

**mcpo** is an MCP proxy that converts stdio-based MCP servers into HTTP endpoints using the Streamable HTTP transport. It runs inside a dedicated Python virtual environment (`mcpo-venv/`) and acts as a bridge between native MCP servers and HTTP-based clients.

### 2.2 Configuration (`mcpo-servers.json`)

Located at `~/Nymphs-Brain/mcp/config/mcpo-servers.json`:

```json
{
  "mcpServers": {
    "filesystem": {
      "type": "streamable-http",
      "url": "http://127.0.0.1:8100/servers/filesystem/mcp"
    },
    "memory": {
      "type": "streamable-http",
      "url": "http://127.0.0.1:8100/servers/memory/mcp"
    },
    "web-forager": {
      "type": "streamable-http",
      "url": "http://127.0.0.1:8100/servers/web-forager/mcp"
    },
    "context7": {
      "type": "streamable-http",
      "url": "http://127.0.0.1:8100/servers/context7/mcp"
    },
    "unity": {
      "type": "streamable-http",
      "url": "http://127.0.0.1:8100/servers/unity/mcp"
    }
  }
}
```

All 5 servers are exposed at `http://127.0.0.1:8100/servers/{name}/mcp`.

### 2.3 How It Works

1. **Proxy starts** and reads `mcp-proxy-servers.json` for native server definitions
2. **Spawns each server** as a child process (stdio transport)
3. **Listens on port 8100** for incoming HTTP/MCP connections
4. **Routes requests** from HTTP clients to the appropriate stdio server
5. **Forwards responses** back to the client

### 2.4 Proxy Logs

| File | Purpose |
|---|---|
| `~/Nymphs-Brain/mcp/logs/mcp-proxy.log` | Main proxy log |
| `~/Nymphs-Brain/mcp/logs/mcp-proxy.pid` | Proxy process PID |
| `~/Nymphs-Brain/mcp/logs/mcp-proxy-dynamic.json` | Dynamic state tracking |

---

## 3. Context7 MCP

### 3.1 What is Context7?

**Context7** is an MCP server that provides real-time access to up-to-date documentation for any programming library or framework. Instead of relying on training data that may be outdated, Context7 fetches live documentation and code examples from official sources.

### 3.2 Installation

```bash
# Installed as a global npm package in Nymphs-Brain
npm install -g @upstash/context7-mcp

# Location: ~/Nymphs-Brain/npm-global/lib/node_modules/@upstash/context7-mcp/
# Runtime: ~/Nymphs-Brain/local-tools/node/bin/node
```

### 3.3 Configuration

**Native server definition** (`mcp-proxy-servers.json`):
```json
"context7": {
  "command": "/home/nymph/Nymphs-Brain/local-tools/node/bin/node",
  "args": ["/home/nymph/Nymphs-Brain/npm-global/lib/node_modules/@upstash/context7-mcp/dist/index.js"]
}
```

**Proxy HTTP endpoint** (`mcpo-servers.json`):
```json
"context7": {
  "type": "streamable-http",
  "url": "http://127.0.0.1:8100/servers/context7/mcp"
}
```

### 3.4 Tools Provided

#### `resolve-library-id`
Resolves a package/product name to a Context7-compatible library ID.

**Input:**
- `query` — The question or task (used for relevance ranking)
- `libraryName` — Library name (e.g., "React", "Next.js", "Express")

**Output:** List of matching libraries with:
- Library ID (format: `/org/project` or `/org/project/version`)
- Description
- Code snippet count
- Source reputation (High/Medium/Low)
- Benchmark score
- Available versions

**Example:**
```
Query: "How to set up authentication"
Library: "Express"
→ /expressjs/express (High reputation, 1200 snippets, score: 78.5)
```

#### `query-docs`
Retrieves up-to-date documentation and code examples from Context7.

**Input:**
- `libraryId` — Context7-compatible ID from `resolve-library-id` (e.g., `/reactjs/react.dev`)
- `query` — Specific question about the library
- `researchMode` — Optional deep research mode (spins up sandboxed agents)

**Output:** Fresh documentation with code examples, API references, and best practices.

**Example:**
```
Library ID: /reactjs/react.dev
Query: "How to use useEffect cleanup function"
→ Returns current docs with code examples
```

### 3.5 Usage Pattern

```
User: "How do I set up JWT auth in Express?"

Step 1: resolve-library-id(query="JWT auth", libraryName="Express")
  → Returns: /expressjs/express

Step 2: query-docs(libraryId="/expressjs/express", query="JWT authentication setup")
  → Returns: Fresh documentation with code examples
```

### 3.6 Key Features

- **Always current:** Fetches from official sources, not stale training data
- **Version-aware:** Can target specific library versions (e.g., `/vercel/next.js/v14.3.0`)
- **Source reputation:** Rates sources as High/Medium/Low authority
- **Code snippets:** Includes real, runnable code examples
- **Research mode:** Deep research with sandboxed agents for complex questions

---

## 4. Unity MCP

### 4.1 What is Unity MCP?

**Unity MCP** is a custom MCP server that enables an AI assistant to interact with a running Unity Editor instance. It provides tools for scene manipulation, GameObject management, material editing, package management, test execution, and more — all through the MCP protocol.

### 4.2 Two-Part Architecture

```
┌──────────────────────────┐         ┌──────────────────────────┐
│   Node.js MCP Server     │         │   Unity Editor Extension │
│   (Server~/src/)         │         │   (Editor/)              │
│                          │         │                          │
│  TypeScript → index.js   │◄──────►│  C# McpUnityServer       │
│                          │  WS     │                          │
│  MCP Tools & Resources   │  8090   │  Unity API Wrappers      │
│  MCP Protocol Handler    │───────►│  Tool Executors          │
│                          │         │  Resource Providers      │
└──────────────────────────┘         └──────────────────────────┘
           │
           ▼
    MCP Client (Cline)
    via mcpo proxy
```

#### Part A: Node.js MCP Server (`Server~/`)

- **Source:** TypeScript at `Server~/src/`
- **Build output:** `Server~/build/index.js`
- **Protocol:** MCP over WebSocket
- **Role:** MCP protocol handler, tool/resource routing, Unity connection management

**Key files:**
```
Server~/src/
├── index.ts                    ← Main entry point, MCP server setup
├── tools/                      ← 25+ tool implementations
│   ├── addAssetToSceneTool.ts
│   ├── batchExecuteTool.ts
│   ├── createPrefabTool.ts
│   ├── createSceneTool.ts
│   ├── deleteSceneTool.ts
│   ├── gameObjectTools.ts
│   ├── getGameObjectTool.ts
│   ├── getSceneInfoTool.ts
│   ├── loadSceneTool.ts
│   ├── materialTools.ts
│   ├── menuItemTool.ts
│   ├── recompileScriptsTool.ts
│   ├── runTestsTool.ts
│   ├── saveSceneTool.ts
│   ├── selectGameObjectTool.ts
│   ├── transformTools.ts
│   ├── unloadSceneTool.ts
│   ├── updateComponentTool.ts
│   └── ...
├── resources/                  ← 8+ resource implementations
│   ├── getAssetsResource.ts
│   ├── getConsoleLogsResource.ts
│   ├── getGameObjectResource.ts
│   ├── getPackagesResource.ts
│   ├── getScenesHierarchyResource.ts
│   └── ...
├── prompts/                    ← Context prompts
│   └── gameobjectHandlingPrompt.ts
├── unity/                      ← Unity connection management
└── utils/                      ← Utilities, logging, command queue
```

**Configuration:**
```json
"unity": {
  "command": "/home/nymph/Nymphs-Brain/local-tools/node/bin/node",
  "args": ["/home/nymph/Nymphs-Brain/mcp-servers/mcp-unity/Server~/build/index.js"],
  "env": {
    "UNITY_HOST": "192.168.1.59",
    "UNITY_PORT": "8090",
    "LOGGING": "true"
  }
}
```

#### Part B: Unity Editor Extension (`Editor/`)

- **Language:** C#
- **Role:** Runs inside Unity Editor, exposes Unity APIs via WebSocket
- **WebSocket server:** Listens on port 8090 (configured in Unity Editor window)

**Key files:**
```
Editor/
├── UnityBridge/
│   ├── McpUnityEditorWindow.cs   ← Editor window for config/connect
│   ├── McpUnityServer.cs         ← WebSocket server, MCP protocol
│   ├── McpUnitySettings.cs       ← Settings persistence
│   └── McpUnitySocketHandler.cs  ← WebSocket message handling
├── Tools/                        ← Tool executors (Unity API wrappers)
│   ├── McpToolBase.cs            ← Base class for all tools
│   ├── AddAssetToSceneTool.cs
│   ├── BatchExecuteTool.cs
│   ├── CreatePrefabTool.cs
│   ├── GameObjectTools.cs
│   ├── MaterialTools.cs
│   ├── TransformTools.cs
│   └── ... (25+ tools)
├── Resources/                    ← Resource providers
│   ├── McpResourceBase.cs
│   ├── GetAssetsResource.cs
│   ├── GetConsoleLogsResource.cs
│   ├── GetGameObjectResource.cs
│   ├── GetScenesHierarchyResource.cs
│   └── ...
├── Services/                     ← Background services
│   ├── ConsoleLogsService.cs
│   └── TestRunnerService.cs
├── Utils/
│   ├── Logger.cs
│   ├── GameObjectHierarchyCreator.cs
│   └── VsCodeWorkspaceUtils.cs
└── Lib/
    └── websocket-sharp.dll       ← WebSocket library
```

### 4.3 Tools Provided (~25)

| Category | Tools |
|---|---|
| **Scene Management** | `createScene`, `loadScene`, `saveScene`, `deleteScene`, `unloadScene`, `getSceneInfo` |
| **GameObject** | `getGameObject`, `selectGameObject`, `updateGameObject`, `updateComponent`, `addAssetToScene` |
| **Transform** | `setPosition`, `setRotation`, `setScale`, `setParent` |
| **Prefab** | `createPrefab` |
| **Materials** | `createMaterial`, `setMaterialProperty`, `assignMaterial` |
| **Package** | `addPackage` |
| **Code** | `recompileScripts`, `runTests` |
| **Utilities** | `batchExecute`, `menuItem`, `sendConsoleLog`, `getConsoleLogs` |

### 4.4 Resources Provided (~8)

| Resource | Description |
|---|---|
| `getAssets` | List available assets in the project |
| `getConsoleLogs` | Unity console log messages |
| `getGameObject` | GameObject details and component hierarchy |
| `getMenuItems` | Available Unity menu items |
| `getPackages` | Installed Unity packages |
| `getScenesHierarchy` | Scene hierarchy tree |
| `getTests` | Available test methods |

### 4.5 How Unity MCP Was Set Up

1. **Clone/copy** the mcp-unity project to `~/Nymphs-Brain/mcp-servers/mcp-unity/`
2. **Build the Node.js server:**
   ```bash
   cd ~/Nymphs-Brain/mcp-servers/mcp-unity/Server~/
   npm install
   npm run build   # Outputs to build/index.js
   ```
3. **Import Unity package** into Unity Editor (`.unitypackage` or copy Editor/ folder)
4. **Configure Unity Editor:**
   - Open `Window → MCP Unity` editor window
   - Set host IP (`192.168.1.59`) and port (`8090`)
   - Click "Start Server"
5. **Add to MCP config** (`mcp-proxy-servers.json`) with `UNITY_HOST` and `UNITY_PORT` env vars
6. **Start mcpo proxy** to expose via HTTP

### 4.6 When Unity MCP is Available

Unity MCP only works when:
- Unity Editor is running on the target machine
- MCP Unity extension is loaded and WebSocket server is started
- The Node.js server can reach the Unity Editor (network connectivity to `UNITY_HOST:UNITY_PORT`)

When Unity is not running, the server starts but tool calls will fail with connection errors.

---

## 5. Web-Forager MCP

### 5.1 What is Web-Forager?

**Web-Forager** is a Python-based MCP server that provides web search and content fetching capabilities. It is installed in a dedicated Python virtual environment (`mcp-venv/`).

### 5.2 Tools Provided

| Tool | Description |
|---|---|
| `duckduckgo_search` / `search` | Search the web using DuckDuckGo |
| `duckduckgo_news_search` | Search recent news articles |
| `jina_fetch` | Fetch a URL and convert to markdown or JSON |

### 5.3 Configuration

```json
"web-forager": {
  "command": "/home/nymph/Nymphs-Brain/mcp-venv/bin/web-forager",
  "args": ["serve"]
}
```

---

## 6. Filesystem MCP

### 6.1 Configuration

```json
"filesystem": {
  "command": "/home/nymph/Nymphs-Brain/local-tools/node/bin/node",
  "args": [
    "/home/nymph/Nymphs-Brain/npm-global/lib/node_modules/@modelcontextprotocol/server-filesystem/dist/index.js",
    "/home/nymph",
    "/home/nymph/Nymphs-Brain",
    "/opt/nymphs3d/Nymphs3D"
  ]
}
```

The allowed directories are passed as arguments, restricting file access to safe paths.

---

## 7. Memory MCP

### 7.1 Configuration

```json
"memory": {
  "command": "/home/nymph/Nymphs-Brain/local-tools/node/bin/node",
  "args": ["/home/nymph/Nymphs-Brain/npm-global/lib/node_modules/@modelcontextprotocol/server-memory/dist/index.js"],
  "env": {
    "MEMORY_FILE_PATH": "/home/nymph/Nymphs-Brain/mcp/data/memory.jsonl"
  }
}
```

Persistent memory store using a JSONL file for cross-session context retention.

---

## 8. Client Configurations

### 8.1 Cline (`cline-mcp-settings.json`)

```json
{
  "mcpServers": {
    "filesystem": {
      "url": "http://127.0.0.1:8100/servers/filesystem/mcp",
      "type": "streamableHttp",
      "disabled": false,
      "timeout": 60
    },
    "memory": {
      "url": "http://127.0.0.1:8100/servers/memory/mcp",
      "type": "streamableHttp",
      "disabled": false,
      "timeout": 60
    },
    "web-forager": {
      "url": "http://127.0.0.1:8100/servers/web-forager/mcp",
      "type": "streamableHttp",
      "disabled": false,
      "timeout": 60
    }
  }
}
```

**Note:** Context7 is connected directly to Cline (not through the proxy settings file above) via Cline's built-in Context7 integration.

### 8.2 Open WebUI (`open-webui-mcp-servers.md`)

Same 3 servers (filesystem, memory, web-forager) added as External Tools in Open WebUI Admin Settings:
- Type: MCP (Streamable HTTP)
- URLs: `http://127.0.0.1:8100/servers/{name}/mcp`

### 8.3 mcpo Full Set (`mcpo-servers.json`)

All 5 servers exposed:
- filesystem
- memory
- web-forager
- context7
- unity

---

## 9. Virtual Environments

| Venv | Purpose | Location |
|---|---|---|
| `mcp-venv` | Python MCP tools (web-forager) | `~/Nymphs-Brain/mcp-venv/` |
| `mcpo-venv` | mcpo proxy (stdio → HTTP bridge) | `~/Nymphs-Brain/mcpo-venv/` |
| `venv` | Nymphs-Brain Python tools (gguf parsing, etc.) | `~/Nymphs-Brain/venv/` |

### Node.js Global Packages

Installed via npm to `~/Nymphs-Brain/npm-global/`:
- `@modelcontextprotocol/server-filesystem` — Filesystem MCP server
- `@modelcontextprotocol/server-memory` — Memory MCP server
- `@upstash/context7-mcp` — Context7 documentation server

---

## 10. Directory Structure

```
~/Nymphs-Brain/
├── mcp/
│   ├── config/
│   │   ├── cline-mcp-settings.json     ← Cline client config (streamableHttp)
│   │   ├── mcp-proxy-servers.json      ← Native server definitions (stdio)
│   │   ├── mcpo-servers.json           ← mcpo proxy config (all 5 servers)
│   │   └── open-webui-mcp-servers.md   ← Open WebUI setup instructions
│   ├── data/
│   │   └── memory.jsonl                ← Memory server persistent store
│   └── logs/
│       ├── mcp-proxy.log               ← Proxy log
│       ├── mcp-proxy.pid               ← Proxy PID file
│       └── mcp-proxy-dynamic.json      ← Dynamic state
├── mcp-venv/                           ← Python venv (web-forager)
├── mcpo-venv/                          ← Python venv (mcpo proxy)
├── mcp-servers/
│   └── mcp-unity/                      ← Unity MCP server
│       ├── Server~/                    ← Node.js MCP server (TypeScript)
│       │   ├── src/                    ← Source
│       │   └── build/                  ← Compiled output
│       └── Editor/                     ← Unity Editor extension (C#)
├── local-tools/
│   └── node/                           ← Node.js runtime
└── npm-global/                         ← Global npm packages
```

---

## 11. Adding a New MCP Server

### Step 1: Install the Server

**Node.js server:**
```bash
npm install -g <package-name>
```

**Python server:**
```bash
source ~/Nymphs-Brain/mcp-venv/bin/activate
pip install <package-name>
```

**Custom server:**
```bash
cp -r <server-folder> ~/Nymphs-Brain/mcp-servers/
cd ~/Nymphs-Brain/mcp-servers/<server-folder>
npm install && npm run build   # if TypeScript
```

### Step 2: Add to Proxy Config

Edit `~/Nymphs-Brain/mcp/config/mcp-proxy-servers.json`:

```json
"my-new-server": {
  "command": "/path/to/runtime",
  "args": ["/path/to/server"],
  "env": {
    "CUSTOM_ENV_VAR": "value"
  }
}
```

### Step 3: Add to mcpo Config

Edit `~/Nymphs-Brain/mcp/config/mcpo-servers.json`:

```json
"my-new-server": {
  "type": "streamable-http",
  "url": "http://127.0.0.1:8100/servers/my-new-server/mcp"
}
```

### Step 4: Restart the Proxy

```bash
# Kill existing proxy
kill $(cat ~/Nymphs-Brain/mcp/logs/mcp-proxy.pid)

# Start new proxy
source ~/Nymphs-Brain/mcpo-venv/bin/activate
mcp serve --config ~/Nymphs-Brain/mcp/config/mcp-proxy-servers.json
```

### Step 5: Add to Client Config

Update `cline-mcp-settings.json` or Open WebUI settings with the new server URL.

---

## 12. Troubleshooting

| Symptom | Likely Cause | Fix |
|---|---|---|
| Context7 returns no results | API key not set or network issue | Check Context7 API key at https://context7.com |
| Unity tools all fail | Unity Editor not running or WebSocket disconnected | Start Unity, open MCP Unity window, click "Start Server" |
| Unity connection timeout | Wrong IP/port in env vars | Verify `UNITY_HOST` and `UNITY_PORT` match Unity settings |
| Proxy not responding | mcpo process crashed | Check `mcp-proxy.log`, restart proxy |
| Fileserver can't read file | Path not in allowed directories | Add path to filesystem server args in `mcp-proxy-servers.json` |
| Web-forager not found | mcp-venv not activated or binary missing | Verify `~/Nymphs-Brain/mcp-venv/bin/web-forager` exists |
| Memory not persistent | Wrong `MEMORY_FILE_PATH` | Check env var points to an accessible path |

---

## 13. Summary

The MCP infrastructure follows a clean proxy-based architecture:

| Layer | Component | Role |
|---|---|---|
| **Native** | 5 MCP servers (stdio) | Filesystem, Memory, Web-Forager, Context7, Unity |
| **Proxy** | mcpo (port 8100) | Bridges stdio → HTTP, keeps servers alive |
| **Clients** | Cline, Open WebUI | Connect via streamable-http to proxy endpoints |

**Context7** provides real-time documentation lookup, eliminating stale knowledge. **Unity MCP** enables full Unity Editor control through 25+ tools and 8 resources. **Web-Forager** provides web search and content fetching. The **mcpo proxy** ties it all together, allowing multiple AI clients to share the same server instances.