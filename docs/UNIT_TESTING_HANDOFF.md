# Unit Testing Handoff — AI Agent Reference Guide

**Audience:** Future AI agents working on any project
**Purpose:** Explain why unit testing is needed, how to implement it, using WORBI as a concrete case study
**Created:** May 2026

---

## 1. Why Unit Testing Is Non-Negotiable

### 1.1 The Regression Problem

As a project grows, manual testing becomes impossible. WORBI has 6.2.58 versions, 47 UI components, 12 backend services, and 200+ configuration options. No human or agent can manually verify every combination of features after every change. Without automated tests, every change is a gamble — you fix one thing and silently break another.

**Rule of thumb:** Once a project exceeds ~10,000 lines of application code, manual testing is no longer a viable quality strategy.

### 1.2 Tests as Executable Documentation

Comments and documentation rot. Tests don't — they either pass or fail. A well-written test is the single source of truth for what a function is supposed to do. When an agent reads a test file, it learns:
- What inputs the function accepts
- What outputs it produces
- What edge cases it handles
- What errors it throws

**WORBI example:** Reading `authService.test.ts` teaches you that `verifyToken()` returns `{ valid: boolean, username?: string }` and does NOT throw on invalid tokens — a detail not documented anywhere else.

### 1.3 Safe Refactoring

Tests give you permission to change internals. Without tests, you're afraid to touch code because you don't know what will break. With tests, you can restructure, rename, and optimize with confidence — if the tests pass, the behavior is preserved.

### 1.4 Edge Case Discovery

The process of writing tests forces you to consider inputs you didn't think about:
- What if the username is empty?
- What if the file path contains `..`?
- What if the API returns a 500?

**WORBI example:** Writing tests for `fileService.getSafePath()` revealed that `path.join()` on Linux does NOT treat a leading `/` as filesystem root — an input of `/etc/passwd` becomes a subdirectory inside the workspace. The real security is the `..` sequence check, not absolute path detection. This insight was discovered during test writing, not during code review.

### 1.5 The AI-Assisted Development Imperative

AI agents make changes fast. A single agent can modify 20 files in a session. Without tests, there's no safety net — the agent can introduce subtle regressions that aren't obvious from reading the diff. Tests are the verification layer that makes AI-assisted development safe.

**Key insight:** The faster you make changes, the more you need tests. Agents amplify both productivity and risk. Tests are the counterweight.

### 1.6 Bug Fix Cost Curve

The cost of fixing a bug increases exponentially the later it's discovered:

| Stage | Relative Cost |
|-------|---------------|
| During development (caught by test) | 1x |
| After merge, before release | 3x |
| In production, before user notices | 10x |
| User reports the bug | 25x |
| Bug causes data loss / security breach | 100x+ |

Unit tests catch bugs at the cheapest point — during development, before the code is merged.

---

## 2. Testing Fundamentals (Stack-Agnostic)

### 2.1 The Test Pyramid

```
        /\
       / E2E \          — Few tests, slow, expensive
      /-------\
     /  Integration  —   — Some tests, medium speed
    /-------------\
   /       Unit           — Many tests, fast, cheap
  /-------------------\
```

| Layer | What it tests | Speed | Isolation | Count |
|-------|--------------|-------|-----------|-------|
| **Unit** | Single function/method in isolation | ~1ms | High (mock dependencies) | Hundreds to thousands |
| **Integration** | How components work together | ~100ms | Medium (real DB, real HTTP) | Dozens |
| **E2E** | Full user workflow through the UI | ~5s | Low (real everything) | A handful |

**Strategy:** Invest 70% of effort in unit tests, 20% in integration, 10% in e2e. The pyramid exists for a reason — unit tests give the most value per line of test code.

### 2.2 Arrange-Act-Assert

Every test follows the same 3-step structure:

```
// ARRANGE — Set up the test conditions
const input = createTestData();
const sut = new Service();  // System Under Test

// ACT — Execute the behavior being tested
const result = sut.process(input);

// ASSERT — Verify the outcome matches expectations
expect(result.status).toBe('success');
expect(result.output).toContain('expected');
```

This pattern makes tests readable, debuggable, and consistent. If a test doesn't fit this structure, it's probably testing too much.

### 2.3 What to Test vs. What to Mock

**Decision framework:**

| Rule | Explanation |
|------|-------------|
| **Test your code** | Functions, logic, decisions written by you/your team |
| **Mock external systems** | Databases, HTTP calls, file system, third-party APIs |
| **Mock slow dependencies** | Anything that makes tests slow (>10ms) |
| **Mock non-deterministic dependencies** | Clocks, random numbers, network state |
| **Test pure logic directly** | No mocks needed for functions with no dependencies |

**WORBI example:** In `toolService.test.ts`, the `executeTool()` dispatch logic is tested directly, but `axios.get()` (used by the `web_search` tool) is mocked because tests shouldn't make real HTTP requests.

### 2.4 Coverage Targets

Coverage is a hygiene metric, not a quality metric. 100% coverage with `expect(true).toBe(true)` is worthless. 50% coverage with meaningful assertions is valuable.

**Recommended starting thresholds by project type:**

| Project Type | Lines | Functions | Branches |
|-------------|-------|-----------|----------|
| Library/SDK | 80% | 80% | 70% |
| Web application | 50% | 60% | 40% |
| CLI tool | 60% | 70% | 50% |
| New project (starting) | 0% (ratchet up) | 0% | 0% |

**Ratcheting strategy:** Start at 0% (or current baseline). Increase by 5% per major release until the target is reached. WORBI starts at 33% lines / 47% functions and ratchets up.

### 2.5 The Smallest Useful Test

Don't start with a grand test plan. Start with one test that proves your test pipeline works:

```ts
it('should add two numbers', () => {
  expect(1 + 1).toBe(2);
});
```

Then immediately replace it with a real test of actual code. The goal is to verify the tooling works before investing in test content.

---

## 3. WORBI Case Study — Implementation Reference

### 3.1 Architecture Overview

WORBI built its test suite across 9 phases, growing from 0 to 363+ server tests and dozens of client tests:

```
WORBI/
├── vitest.config.ts              ← Root orchestrator (multi-project)
├── server/
│   ├── vitest.config.ts          ← Server config (node env)
│   └── tests/
│       ├── setup.ts              ← Sets NODE_ENV=test, mocks fetch
│       ├── helpers/
│       │   ├── authHelper.ts     ← JWT token generation
│       │   ├── fsHelper.ts       ← Temp workspace management
│       │   └── testApp.ts        ← Supertest factory
│       ├── unit/                 ← Service unit tests
│       └── integration/          ← HTTP endpoint tests
├── client/
│   ├── vitest.config.ts          ← Client config (jsdom env)
│   └── src/
│       ├── hooks/*.test.ts       ← Hook tests (colocated)
│       └── components/*.test.tsx ← Component tests (colocated)
│   └── tests/
│       ├── setup.ts              ← jest-dom, window mocks
│       └── helpers/
│           └── mockTiptap.ts     ← TipTap editor mocks
```

**Key design decisions:**
- **Multi-project Vitest:** Root config delegates to server/client configs. `npm test` runs both; `npm run test:server` runs server only.
- **Colocated client tests:** Hook tests live beside the hook source (`useAuth.test.ts` next to `useAuth.ts`). Server tests live in `tests/` directory.
- **TypeScript test files:** Even though server source is `.js` (ESM), test files use `.ts` for type safety.

### 3.2 Infrastructure Layer

#### Root Configuration (`vitest.config.ts`)

```ts
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './client/src'),
    },
  },
  test: {
    projects: ['server/vitest.config.ts', 'client/vitest.config.ts'],
    testTimeout: 10_000,
    passWithNoTests: true,
    coverage: {
      provider: 'v8',
      include: ['server/src/**/*.js', 'client/src/**/*.{ts,tsx}'],
      exclude: [
        '**/*.test.{ts,tsx,js}',
        '**/node_modules/**',
        'client/src/main.tsx',    // Entry point only
        'client/src/App.tsx',     // Orchestrator - tested via components
      ],
      thresholds: {
        lines: 33,
        functions: 47,
      },
    },
  },
});
```

**Why this matters:** The root config is the orchestrator. It defines shared settings (coverage, timeouts) and delegates to project-specific configs. The `projects` array is what enables `npm test` to run both server and client tests in a single command.

#### Server Configuration (`server/vitest.config.ts`)

```ts
import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    name: 'server',
    environment: 'node',
    include: ['src/**/*.test.ts', 'tests/**/*.test.ts', 'tests/**/*.test.js'],
    setupFiles: ['./tests/setup.ts'],
    globals: true,
    testTimeout: 10_000,
  },
});
```

**Key detail:** `environment: 'node'` means no browser APIs (`window`, `document`) are available. Server tests run in a real Node.js environment.

#### Client Configuration (`client/vitest.config.ts`)

```ts
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    setupFiles: ['./test-setup.ts'],
    include: ['src/**/*.test.ts', 'src/**/*.test.tsx'],
    globals: true,
  },
});
```

**Key detail:** `environment: 'jsdom'` provides a simulated browser DOM. Client tests have access to `window`, `document`, and DOM APIs.

#### Test Setup Files

**Server setup** (`server/tests/setup.ts`):
```ts
import { vi } from 'vitest';

process.env.NODE_ENV = 'test';
globalThis.fetch = vi.fn() as any;
```
- Sets `NODE_ENV=test` so `index.js` does NOT start the HTTP server
- Mocks `fetch` to prevent real HTTP calls during tests

**Client setup** (`client/tests/setup.ts`):
```ts
import '@testing-library/jest-dom';

Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query: string) => ({
    matches: false, media: query, onchange: null,
    addListener: () => {}, removeListener: () => {},
    addEventListener: () => {}, removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
});

window.confirm = () => true;
window.prompt = () => null;
```
- Loads `@testing-library/jest-dom` for custom matchers (`toBeInTheDocument`, `toHaveTextContent`)
- Mocks `window.matchMedia` (not available in jsdom)
- Mocks `window.confirm` / `window.prompt` to prevent blocking

### 3.3 Test Helpers

**Principle:** Helpers eliminate boilerplate. If you write the same setup code in 3+ tests, extract it into a helper.

#### Auth Helper (`server/tests/helpers/authHelper.ts`)

```ts
import jwt from 'jsonwebtoken';
import config from '../../src/config.js';

export function generateTestToken(username: string): string {
  return jwt.sign({ username }, config.jwtSecret, { expiresIn: '7d' });
}

export function generateExpiredToken(username: string): string {
  return jwt.sign({ username }, config.jwtSecret, { expiresIn: '0s' });
}

export function generateTamperedToken(username: string): string {
  return jwt.sign({ username }, 'wrong-secret', { expiresIn: '7d' });
}
```

**Why this exists:** Every auth test needs a JWT token. Without this helper, every test manually creates a token — duplicating the signing logic. The helper provides named factories for different token states (valid, expired, tampered).

#### Filesystem Helper (`server/tests/helpers/fsHelper.ts`)

```ts
import fs from 'fs';
import path from 'path';
import os from 'os';

export function createTempWorkspace(): string {
  return fs.mkdtempSync(path.join(os.tmpdir(), 'worbi-test-'));
}

export function cleanupTempWorkspace(rootPath: string): void {
  if (fs.existsSync(rootPath)) {
    fs.rmSync(rootPath, { recursive: true, force: true });
  }
}

export function createTestFile(workspaceRoot: string, relativePath: string, content: string): void {
  const fullPath = path.join(workspaceRoot, relativePath);
  fs.mkdirSync(path.dirname(fullPath), { recursive: true });
  fs.writeFileSync(fullPath, content, 'utf-8');
}
```

**Why this exists:** File service tests need a filesystem to test against but must NOT write to the real user data directory. Temp workspaces are isolated and cleaned up after each test.

#### Supertest Factory (`server/tests/helpers/testApp.ts`)

```ts
import request from 'supertest';
import app from '../../src/index.js';

export function getTestApp() {
  return request(app);
}

export async function getAuthedApp(username: string = 'testuser') {
  const res = await request(app).post('/api/auth/login').send({ username });
  const token = res.body.token;
  const agent = request(app);
  return { agent, token };
}
```

**Why this exists:** Integration tests need to make HTTP requests to the Express app without starting a real server. `supertest` lets you call routes directly against the app object. The `getAuthedApp` helper automates the login flow so every integration test doesn't repeat it.

### 3.4 Server Test Patterns

#### Unit Test Example (`authService.test.ts`)

```ts
import { describe, it, expect } from 'vitest';
import { loginOrCreate, verifyToken } from '../../../src/services/authService.js';
import { generateTestToken, generateExpiredToken, generateTamperedToken } from '../../helpers/authHelper.js';

describe('authService.loginOrCreate()', () => {
  it('creates a new user and returns user object', () => {
    const result = loginOrCreate('test_svc_newuser');
    expect(result.user).toBeDefined();
    expect(result.user.username).toBe('test_svc_newuser');
    expect(result).toHaveProperty('token');
    expect(result).toHaveProperty('dirs');
  });

  it('throws for missing username', () => {
    expect(() => loginOrCreate('')).toThrow();
  });

  it('throws for username with invalid characters', () => {
    expect(() => loginOrCreate('user name!')).toThrow();
  });
});

describe('authService.verifyToken()', () => {
  it('returns valid for valid token', () => {
    const token = generateTestToken('testuser');
    expect(verifyToken(token).valid).toBe(true);
  });

  it('returns invalid for expired token', () => {
    const token = generateExpiredToken('testuser');
    expect(verifyToken(token).valid).toBe(false);
  });
});
```

**Patterns demonstrated:**
- Import with `.js` extension (ESM requirement)
- Helper functions for test data
- Grouping related tests with `describe()`
- Testing both happy path and error path
- Testing edge cases (empty, invalid characters)

#### Mocking Dependencies

```ts
import { vi } from 'vitest';

// Mock BEFORE importing the module under test
vi.mock('../../../src/services/authService.js', () => ({
  getUserWorkspaceDir: () => '/tmp/mock-workspace',
  getUserAssetsDir: () => '/tmp/mock-assets',
}));

import * as fileService from '../../../src/services/fileService.js';
```

**Critical rule:** `vi.mock()` must be called BEFORE the import statement that loads the module under test. Vitest hoists `vi.mock()` calls to the top of the file, but the import must come after the mock in source order.

#### Integration Test Example

```ts
import { describe, it, expect } from 'vitest';
import { getTestApp, getAuthedApp } from '../helpers/testApp.js';

describe('Files API', () => {
  const app = getTestApp();

  it('returns 401 without auth', async () => {
    const res = await app.get('/api/files');
    expect(res.status).toBe(401);
  });

  it('returns file list with auth', async () => {
    const { agent } = await getAuthedApp('testuser');
    const res = await agent.get('/api/files');
    expect(res.status).toBe(200);
    expect(Array.isArray(res.body)).toBe(true);
  });
});
```

**Patterns demonstrated:**
- Test unauthenticated access first (security check)
- Test authenticated access second
- Use supertest agent for request chain

### 3.5 Client Test Patterns

#### Hook Test Example

```ts
import { renderHook, act } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';

// Mock API BEFORE importing the hook
vi.mock('../services/api', async () => {
  return {
    login: vi.fn(),
    getMe: vi.fn(),
  };
});

import * as api from '../services/api';
const mockLogin = api.login as ReturnType<typeof vi.fn>;
import useAuth from './useAuth';

beforeEach(() => {
  mockLogin.mockReset();
  localStorage.clear();
});

describe('useAuth', () => {
  it('should start unauthenticated', () => {
    const { result } = renderHook(() => useAuth());
    expect(result.current.isAuthenticated).toBe(false);
  });

  it('should login and set token', async () => {
    mockLogin.mockResolvedValue({ token: 'new-token', user: { username: 'user' } });

    const { result } = renderHook(() => useAuth());

    await act(async () => {
      await result.current.login('user');
    });

    expect(result.current.isAuthenticated).toBe(true);
    expect(localStorage.getItem('wbu_token')).toBe('new-token');
  });
});
```

**Patterns demonstrated:**
- Mock API calls so tests don't hit a real server
- `mockReset()` in `beforeEach` to prevent test interference
- `await act(async () => {...})` for async state changes
- Clear `localStorage` between tests

#### Component Mocking Strategy

When testing a component, mock its heavy dependencies:

```ts
// Mock lucide-react icons
vi.mock('lucide-react', () => {
  const icons: Record<string, React.FC> = {};
  for (const name of ['Home', 'Search', 'Settings', 'LogOut']) {
    icons[name] = vi.fn((props: any) => <svg data-testid={name} {...props} />);
  }
  return icons;
});
```

**Principle:** You're testing the component's behavior, not the icon library's rendering. Mock what you don't care about.

### 3.6 Phase-Based Implementation

WORBI didn't write 363 tests at once. The suite was built methodically across 9 phases:

| Phase | Focus | Tests Added | Rationale |
|-------|-------|-------------|-----------|
| 0 | Infrastructure (configs, helpers) | 0 | Foundation before content |
| 1 | Security (path traversal, auth, middleware) | 57 | Critical paths first |
| 2 | Core services (file, tag, tool, llm, reminder, location) | 246 | Most business logic |
| 3 | Lifecycle & config services | 99 | Remaining backend |
| 4 | Client hooks | 72 | Frontend logic |
| 5 | Feature modules | 31 | Frontend features |
| 6 | Component smoke tests | 153 | UI verification |
| 7 | TipTap extensions & utils | ~18 | Edge cases |
| 8 | Coverage thresholds + CI | 0 | Quality gates |

**Strategy:** Security-critical code gets tested first. If the test suite only gets partway through, at least the dangerous code is covered.

---

## 4. Implementation Blueprint for Any Project

### Step 1: Choose Your Test Runner

Match your test runner to your build tool:

| Build Tool | Test Runner | Why |
|-----------|-------------|-----|
| Vite | Vitest | Shares config, same plugin ecosystem |
| Webpack | Jest | Mature, great docs, large ecosystem |
| esbuild | Vitest | Fast, ESM-native |
| Python (any) | pytest | Standard, plugin-rich |
| Go | Built-in (`go test`) | No external dependency needed |
| Rust | Built-in (`cargo test`) | No external dependency needed |
| .NET | xUnit / NUnit | Standard .NET testing frameworks |

**Principle:** Don't add a new tool if your build tool already integrates with a test runner.

### Step 2: Minimal Working Setup

**Goal:** Get to "one test passes" as fast as possible. Don't configure coverage, CI, or helpers yet.

1. Install the test runner as a dev dependency
2. Create the minimal config file
3. Write one test file with one test
4. Run it. See it pass.

**Example (Node + Vitest):**
```bash
npm install --save-dev vitest
```

```ts
// vitest.config.ts
import { defineConfig } from 'vitest/config';
export default defineConfig({
  test: {
    environment: 'node',
    globals: true,
  },
});
```

```ts
// test/basic.test.ts
import { describe, it, expect } from 'vitest';
describe('basic', () => {
  it('1 + 1 = 2', () => {
    expect(1 + 1).toBe(2);
  });
});
```

```bash
npx vitest run
```

Once this works, replace the dummy test with a real test.

### Step 3: Create Test Helpers

Identify repetitive setup patterns and extract them:

| Pattern | Helper Type |
|---------|-------------|
| "Every test needs an auth token" | Auth helper (token factory) |
| "Every test needs a clean database" | DB helper (seed/truncate) |
| "Every test needs a temp directory" | FS helper (create/cleanup) |
| "Every test needs a fake HTTP server" | Server helper (test app factory) |
| "Every test needs mock user data" | Fixture helper (data factories) |

**Rule:** If you copy-paste the same setup code into 3 tests, extract it into a helper.

### Step 4: Test Critical Paths First

Not all code is equally important. Prioritize:

1. **Security code** — Auth, input validation, path traversal protection
2. **Data mutations** — Create, update, delete operations
3. **Money/business logic** — Pricing, calculations, business rules
4. **Data parsing/serialization** — JSON, CSV, file format handling
5. **Everything else** — UI rendering, display logic, formatting

**WORBI lesson:** Path traversal tests were written before feature tests because a path traversal bug is a security vulnerability, not just a feature bug.

### Step 5: Set Coverage Thresholds

Start low, ratchet up:

```ts
// Start with current baseline (even if 0%)
coverage: {
  thresholds: {
    lines: 0,       // or current percentage
    functions: 0,
  },
},
```

Increase by 5% per release until you reach your target. The ratcheting approach ensures coverage never drops.

### Step 6: Wire to CI

Add a test step to your CI pipeline. This is the gate that prevents untested code from being merged:

```yaml
# Example GitHub Actions step
- name: Run tests
  run: npm test
```

**Rule:** If tests aren't run in CI, they don't exist. Developers (and agents) will skip local tests. CI enforcement is the only reliable guard.

---

## 5. Anti-Patterns Every Agent Must Avoid

### 5.1 Testing Implementation Details

**Wrong:**
```ts
it('calls processItems with sorted array', () => {
  const mockProcess = vi.fn();
  service.process = mockProcess;
  service.run(['c', 'a', 'b']);
  expect(mockProcess).toHaveBeenCalledWith(['a', 'b', 'c']);
});
```

**Right:**
```ts
it('returns items in sorted order', () => {
  const result = service.run(['c', 'a', 'b']);
  expect(result).toEqual(['a', 'b', 'c']);
});
```

**Principle:** Test what the code does, not how it does it. If the internal implementation changes but the output is the same, the test should still pass.

### 5.2 Over-Mocking

**Wrong:** Testing a function that's 90% mocks. The test passes but the real integration is broken.

**Right:** Mock only external systems (HTTP, DB, file system). Test real interactions between your code's components.

**Rule of thumb:** If removing a mock doesn't change the test outcome, the mock is unnecessary.

### 5.3 Flaky Tests

Common causes:
- **Shared mutable state** — Tests modify a global that affects other tests. Fix: isolate test state (temp dirs, fresh objects per test).
- **Async timing** — Tests assume async operations complete in a fixed time. Fix: use promises, `waitFor`, or explicit synchronization.
- **Test order dependence** — Test B assumes Test A ran first. Fix: every test is self-contained.
- **Non-deterministic data** — Tests use `Math.random()` or `Date.now()`. Fix: mock these functions.

**WORBI lesson:** `beforeEach` creates a fresh temp workspace and `afterEach` cleans it up. No test shares filesystem state with another.

### 5.4 Ignoring Test Failures

"It works when I run it manually" is not a valid excuse. A failing test means one of two things:
1. The code is broken (fix the code)
2. The test is wrong (fix the test)

Never disable a test with `it.skip()` or `xit()` without a comment explaining why and a timeline for fixing it.

### 5.5 Testing Private Internals

**Wrong:** Making a function `export` just to test it, or using type assertions to access private properties.

**Right:** Test the public API. If a private function is complex enough to need its own tests, it's probably complex enough to be its own module.

**Principle:** If the function name starts with `_` or is marked `private`, don't test it directly. Test it through the public interface that calls it.

### 5.6 Coverage Obsession

100% coverage is a vanity metric. A test file with:
```ts
it('does something', () => {
  service.run();
  expect(true).toBe(true);
});
```
...achieves 100% branch coverage but tests nothing.

**Better:** 60% coverage with assertions that verify actual behavior.

### 5.7 Testing Third-Party Code

Don't write tests for libraries you don't own. If your code calls `axios.get()`, don't test that axios works — test that your code calls axios with the right parameters and handles the response correctly.

---

## 6. Quick Reference Tables

### Test File Naming

| Project | Convention | Example |
|---------|-----------|---------|
| TypeScript | `*.test.ts` / `*.test.tsx` | `authService.test.ts` |
| JavaScript | `*.test.js` | `auth.test.js` |
| Python | `test_*.py` | `test_auth.py` |
| Go | `*_test.go` | `auth_test.go` |
| Rust | `#[cfg(test)]` module | In-source `#[test]` functions |

### Test Location

| Strategy | Pattern | Pros | Cons |
|----------|---------|------|------|
| **Colocated** | `src/foo.test.ts` beside `src/foo.ts` | Easy to find, changes together | Mixed with source in file listings |
| **Separate** | `tests/unit/foo.test.ts` | Clean source tree, easy to filter | Must navigate to find test |
| **WORBI** | Hybrid: client colocated, server separate | Best of both per context | Inconsistent within project |

### Common Mock Patterns

| Dependency | How to Mock | Example |
|-----------|-------------|---------|
| HTTP client | Mock the library (`axios`, `fetch`) | `vi.spyOn(axios, 'get').mockResolvedValue({ data: {...} })` |
| Database | Mock the query layer | `vi.mock('./db', () => ({ query: vi.fn() }))` |
| File system | Use temp directories | `fs.mkdtempSync(path.join(os.tmpdir(), 'test-'))` |
| Time | Mock `Date.now()` | `vi.spyOn(global, 'Date', 'getter').mockReturnValue(new Date('2026-01-01'))` |
| Random | Mock `Math.random()` | `vi.spyOn(Math, 'random').mockReturnValue(0.5)` |
| Environment | Mock `process.env` | `vi.stubEnv('API_URL', 'http://test')` |

---

## 7. Checklist: Adding Tests to a New Project

Use this checklist when setting up tests for the first time on any project:

### Infrastructure
- [ ] Choose test runner matching build tool
- [ ] Install test runner + coverage provider as dev dependencies
- [ ] Create minimal config file
- [ ] Create test setup file (env vars, global mocks)
- [ ] Run one passing test to verify pipeline works
- [ ] Add test script to package manager (`npm test`, `pytest`, etc.)

### Helpers
- [ ] Identify repetitive setup patterns
- [ ] Extract auth/token helpers if auth exists
- [ ] Extract filesystem helpers if files are involved
- [ ] Extract data/fixture helpers if tests need structured data

### Test Writing
- [ ] Test security-critical code first (auth, validation, path safety)
- [ ] Test data mutation code second (create, update, delete)
- [ ] Test business logic third (calculations, transformations)
- [ ] Each test: Arrange-Act-Assert structure
- [ ] Each test: Test one behavior, one assertion group
- [ ] Edge cases: empty input, null/undefined, max values, malformed data

### Quality
- [ ] Set initial coverage threshold (start at baseline)
- [ ] Verify tests pass locally
- [ ] Add test step to CI pipeline
- [ ] Document test patterns in project README or handoff

### Maintenance
- [ ] When adding features, add corresponding tests
- [ ] When fixing bugs, add regression test first
- [ ] Ratchet coverage threshold up periodically
- [ ] Remove/update tests when code changes

---

## 8. WORBI-Specific Test Commands

```bash
npm test              # Run all tests (server + client)
npm run test:server   # Server tests only
npm run test:client   # Client tests only
npm run test:coverage # All tests + coverage report
npm run test:watch    # Watch mode (re-run on file change)
```

---

## 9. When to Skip Testing

Not everything needs a test. It's safe to skip testing:

- **Trivial pass-through functions** — `function getName() { return this.name; }`
- **Auto-generated code** — ORM models, protobuf stubs
- **Build tooling configuration** — `tsconfig.json`, `vite.config.ts`
- **Third-party boilerplate** — Code copied from documentation that you didn't modify
- **One-off scripts** — Data migration scripts run once and deleted

**Rule:** If the cost of writing the test exceeds the cost of manual verification, skip it. But be honest about the cost — "I don't know how to test this" is not the same as "this isn't worth testing."

---

*End of document.*