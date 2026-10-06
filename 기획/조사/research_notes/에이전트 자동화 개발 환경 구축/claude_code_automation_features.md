# Claude Code automation building blocks and best practices (as of 2026-10)

Scope note: almost everything below comes from the official docs at code.claude.com (fetched 2026-10-06). The docs reference Claude Code versions up to v2.1.283, so the facts describe the product as of roughly September–October 2026. Several features are labeled research preview or experimental: routines, agent view, agent teams, agent-type hooks, and dynamic workflows. Their names, limits, and APIs may change. Where a fact came from a model summary of a page (WebFetch) and not from verbatim text, the wording is paraphrased, but the names and numbers were checked against the page text where possible.

## 1. Hooks: events, what they can block or modify, and real-world recipes

### Takeaway
Hooks are the only way to make something happen every time. The docs repeat that CLAUDE.md and skills are requests, while a hook is enforcement. In 2026 there are about 30 hook events and five handler types (command, http, mcp_tool, prompt, agent). The workhorses are `PreToolUse` (block, or rewrite input), `PostToolUse` (format or lint, then feed results back), `Stop` (a gate that refuses "done" until checks pass), `SessionStart` (inject context, including after compaction) and `Notification` (alerts).

### Cited Findings
**Event list and capabilities**
- The hook events are: `SessionStart`, `Setup`, `UserPromptSubmit`, `UserPromptExpansion`, `PreToolUse`, `PermissionRequest`, `PermissionDenied`, `PostToolUse`, `PostToolUseFailure`, `PostToolBatch`, `Notification`, `MessageDisplay`, `SubagentStart`, `SubagentStop`, `TaskCreated`, `TaskCompleted`, `Stop`, `StopFailure`, `TeammateIdle`, `InstructionsLoaded`, `ConfigChange`, `CwdChanged`, `DirectoryAdded`, `FileChanged`, `WorktreeCreate`, `WorktreeRemove`, `PreCompact`, `PostCompact`, `PreModelSwitch`, `PostModelSwitch`, `Elicitation`, `ElicitationResult`, and `SessionEnd`. — [Hooks reference](https://code.claude.com/docs/en/hooks)
- Blocking: exit code 2 blocks the action on `PreToolUse`, `UserPromptSubmit`, `UserPromptExpansion`, `Stop`/`SubagentStop` (prevents stopping), `TaskCreated`/`TaskCompleted`, `ConfigChange`, `PreCompact`, `Elicitation*` and `WorktreeCreate/Remove`. On `PostToolUse` and other after-the-fact events, exit 2 only shows stderr to Claude, because the tool already ran. — [Hooks reference](https://code.claude.com/docs/en/hooks)
- What hooks can modify:
  - `PreToolUse` can return `permissionDecision` (`allow|deny|ask|defer`), `updatedInput` (rewrite the tool arguments) and `additionalContext`.
  - `PostToolUse` can return `updatedToolOutput` and `additionalContext`.
  - `PermissionRequest` can return `decision.behavior` allow or deny.
  - `SessionStart` can return `additionalContext`, `sessionTitle`, `initialUserMessage`, `watchPaths` and `reloadSkills`.
  - `Stop` with `decision: "block"` plus a reason makes Claude keep working.
  
  — [Hooks reference](https://code.claude.com/docs/en/hooks)
- Handler types:
  - `command` (shell; JSON on stdin; exit code plus stdout/stderr)
  - `http` (POST JSON to a URL; it cannot block by status code alone and must return 2xx with a JSON decision)
  - `mcp_tool` (calls a tool on a configured MCP server)
  - `prompt` (one LLM call that returns `{"ok": true/false, "reason": ...}`)
  - `agent` (spawns a verifier subagent with tools such as Read and Grep; up to 50 tool turns; marked **experimental**, and the docs say "for production workflows, prefer command hooks")
  
  — [Hooks reference](https://code.claude.com/docs/en/hooks); [Hooks guide](https://code.claude.com/docs/en/hooks-guide.md)

**Where hooks live and how they are scoped**
- Config locations: managed policy, `~/.claude/settings.json`, `.claude/settings.json` (committable), `.claude/settings.local.json` (gitignored), plugin `hooks/hooks.json`, skill frontmatter (active for the rest of the session after the skill is invoked) and subagent frontmatter (active while that subagent runs). Hooks from all sources merge, and all of them fire. — [Hooks reference](https://code.claude.com/docs/en/hooks); [Features overview](https://code.claude.com/docs/en/features-overview)
- Matcher syntax: a plain name or `|` list does an exact match (`Edit|Write`). Any other character turns the matcher into an unanchored JS regex (`mcp__memory__.*`). An `if` field takes permission-rule syntax (`Bash(git *)`, `Edit(*.ts)`), but Bash `if` matching is "best-effort; don't rely on them for security enforcement". — [Hooks reference](https://code.claude.com/docs/en/hooks)
- Path placeholders and env vars: `${CLAUDE_PROJECT_DIR}`, `${CLAUDE_PLUGIN_ROOT}`, `${CLAUDE_PLUGIN_DATA}`, `$CLAUDE_ENV_FILE` (persists env vars; only in SessionStart, Setup, CwdChanged and FileChanged) and `$CLAUDE_CODE_REMOTE` (set to "true" in web/cloud sessions). — [Hooks reference](https://code.claude.com/docs/en/hooks)
- Windows specifics:
  - Shell-form hooks run via `sh -c` on Unix and Git Bash on Windows, or PowerShell when `"shell": "powershell"` is set.
  - Exec form (`command` + `args`) on Windows requires a real `.exe`, not a `.cmd` or `.bat`.
  
  — [Hooks reference](https://code.claude.com/docs/en/hooks)

**Timeouts, async hooks, and interaction with permissions**
- Default timeouts: command, http and mcp_tool get 600 s, lowered to 30 s for `UserPromptSubmit` and the model-switch hooks. Prompt hooks get 30 s and agent hooks 60 s. `SessionEnd` hooks share a 1.5 s budget, which rises to match a longer per-hook timeout up to 60 s. — [Hooks guide](https://code.claude.com/docs/en/hooks-guide.md)
- Async: `"async": true` runs a hook in the background and discards its result. `"asyncRewake": true` runs it in the background and wakes Claude on exit code 2, which suits long-running monitors. — [Hooks reference](https://code.claude.com/docs/en/hooks)
- Hooks fire before any permission-mode check. A `PreToolUse` `permissionDecision: "deny"` blocks the tool even in `bypassPermissions` mode or with `--dangerously-skip-permissions`. The reverse does not hold: a hook returning `"allow"` does not override deny rules from settings. — [Hooks guide](https://code.claude.com/docs/en/hooks-guide.md)

**Official recipes in the docs**
- Desktop notification when Claude needs input (`Notification` event).
- Auto-format after edits: `PostToolUse`, matcher `Edit|Write`, command `jq -r '.tool_input.file_path' | xargs npx prettier --write`. A `FileChanged` hook can instead react to changes made through Bash.
- Block edits to protected files (`PreToolUse` script that exits 2 with a stderr message).
- Re-inject context after compaction: `SessionStart` with matcher `compact` that echoes reminders.
- Audit config changes (`ConfigChange`).
- Reload the environment when the directory or files change.
- Auto-approve specific permission prompts (`PermissionRequest` returning `{"decision":{"behavior":"allow"}}`).
- A prompt-based `Stop` hook that checks every task is complete.
- An agent-based `Stop` hook that verifies tests pass.

— [Hooks guide](https://code.claude.com/docs/en/hooks-guide.md)

- Cost-saving recipe: a `PreToolUse` hook on `Bash` rewrites test commands (`npm test|pytest|go test`) through `updatedInput` so that they pipe output through `grep -A 5 -E '(FAIL|ERROR|error:)' | head -100`. The aim is to return failures only. The docs say such preprocessing can cut "tens of thousands of tokens to hundreds". — [Costs](https://code.claude.com/docs/en/costs)
- Stop-hook loop guard: Claude Code overrides a Stop hook after 8 consecutive blocks with no tool call in between. Scripts should read `stop_hook_active` from the input and exit 0 when it is true. The cap can be raised with `CLAUDE_CODE_STOP_HOOK_BLOCK_CAP`. — [Hooks guide](https://code.claude.com/docs/en/hooks-guide.md)
- In the best-practices "verification ladder", a Stop hook is the "deterministic gate" that blocks the turn from ending until a check passes. The other rungs are a `/goal` condition re-checked by an evaluator after every turn, and a verification subagent or workflow. — [Best practices](https://code.claude.com/docs/en/best-practices)

**Limitations and debugging**
- Command hooks cannot trigger `/` commands or tool calls.
- When multiple PreToolUse hooks return `updatedInput`, the last one to finish wins and the order is non-deterministic.
- Stop hooks fire on every response end, not only when a task completes.
- Hooks have no `/dev/tty`.
- `disableAllHooks: true` turns all hooks off, but it cannot disable managed hooks.
- Debug with `/hooks`, `--debug-file <path>` or `CLAUDE_CODE_DEBUG=1`.

— [Hooks guide](https://code.claude.com/docs/en/hooks-guide.md); [Hooks reference](https://code.claude.com/docs/en/hooks)

**Community catalog (lower confidence)**
- The community list awesome-claude-code (about 55.1k stars) catalogs hook tools. Examples: desktop/phone notifiers (e.g., "ai-agent-notifier"), sound-effect hooks, secret-leak guards ("Agent Guard"), Slack/Mattermost session streaming, and sandboxed devcontainers for unattended auto-approve runs ("aicontainer"). — [awesome-claude-code](https://github.com/hesreallyhim/awesome-claude-code) (names come from a model summary of the README; treat individual entries as unverified)

### Inferences
- A solo developer gets the most from a small set of hooks:
  1. A `PostToolUse` formatter or linter on `Edit|Write`.
  2. A `PreToolUse` guard for paths and dangerous commands.
  3. A `Stop` gate that runs the fast test or compile check (with the `stop_hook_active` guard).
  4. A `SessionStart` (`compact` matcher) re-injection of the current task state.
  5. A `Notification` alert.
  
  Each one replaces a CLAUDE.md line that would otherwise be advisory.
- On Windows, prefer `"shell": "powershell"` or exec form with a real `.exe` (e.g., `node`, `python`) to avoid Git Bash quoting problems.
- Bash `if` matching is best-effort, so security-relevant guards should parse `tool_input.command` themselves instead of relying on `if` patterns.

### Gaps
- I did not verify which hook events are newest (for example `MessageDisplay`, `PreModelSwitch`, `PostToolBatch`) or when each was introduced. The changelog was not fetched.
- Community recipe repos (e.g., specific "claude-code-hooks" collections) were not individually verified.

## 2. Skills, subagents, slash commands, plugins, output styles, and keeping CLAUDE.md/rules/memory small

### Takeaway
The official model is layered by load cost:
- **CLAUDE.md**: always loaded; keep it under 200 lines.
- **`.claude/rules/`**: always loaded, or path-scoped so they load only when matching files are touched.
- **Skills**: only the description is loaded each session; the full body loads on demand. This is the progressive-disclosure layer, and custom slash commands are now skills.
- **Subagents**: run in an isolated context and return a summary.
- **Hooks**: zero context cost; deterministic.
- **Output styles**: role and tone, with high instruction weight.
- **Plugins**: the packaging and distribution layer.

### Cited Findings
**Choosing the right feature**
- The "build your setup over time" trigger table:

  | Trigger | Add |
  |---|---|
  | Claude gets a convention wrong twice | CLAUDE.md |
  | You keep typing the same prompt | User-invocable skill |
  | You paste the same playbook a third time | Skill |
  | You keep copying data from a browser tab | MCP |
  | A side task floods context | Subagent |
  | Something must happen every time | Hook |
  | A second repo needs the same setup | Plugin |

  — [Features overview](https://code.claude.com/docs/en/features-overview)
- Context cost by feature:
  - CLAUDE.md: full content on every request.
  - Output style: full instructions on every request.
  - Skills: descriptions on every request, body when used. `disable-model-invocation: true` removes even the description.
  - MCP: tool names at start, schemas deferred (tool search is on by default).
  - Subagents: isolated.
  - Hooks: zero unless they return output.
  
  — [Features overview](https://code.claude.com/docs/en/features-overview)
- "Put guardrails in hooks. An instruction like 'never edit `.env`' in CLAUDE.md or a skill is a request, not a guarantee." — [Features overview](https://code.claude.com/docs/en/features-overview)
- The Anthropic blog "Steering Claude Code" (June 18, 2026) lists seven steering methods. It describes Output Styles as "highest instruction weight; never compacted" and Hooks as "bypasses compaction". Anti-patterns it names:
  - "every time X, always do Y" rules kept in CLAUDE.md instead of hooks
  - unscoped rules (add `paths:`)
  - 30-line procedures kept in CLAUDE.md instead of `.claude/skills/`
  - hard guardrails kept in prompts instead of `PreToolUse` hooks or managed settings
  
  — [Claude blog: Steering Claude Code](https://claude.com/blog/steering-claude-code-skills-hooks-rules-subagents-and-more)

**CLAUDE.md, rules and auto memory**
- Size: "target under 200 lines per CLAUDE.md file". `@path` imports help organization "but don't reduce its context cost". Imports can nest up to 4 hops. A file over 4 MiB is skipped. Block-level HTML comments are stripped before injection, so maintainer notes cost no tokens. — [Memory](https://code.claude.com/docs/en/memory.md)
- Locations and load order:
  - Managed: `C:\Program Files\ClaudeCode\CLAUDE.md` on Windows
  - User: `~/.claude/CLAUDE.md`
  - Project: `./CLAUDE.md` or `./.claude/CLAUDE.md`, with AGENTS.md support
  - Local: `./CLAUDE.local.md`
  
  Files from parent directories load at launch; files in subdirectories load on demand when Claude touches files there. — [Memory](https://code.claude.com/docs/en/memory.md)
- `.claude/rules/*.md` are discovered recursively. Rules without `paths:` load at launch. Rules with `paths:` frontmatter (globs) load only when Claude works with matching files. — [Memory](https://code.claude.com/docs/en/memory.md)
- Auto memory:
  - Stored at `~/.claude/projects/<project>/memory/MEMORY.md` plus topic files. Only the first 200 lines or 25KB of `MEMORY.md` load at session start; topic files are read on demand.
  - Types: `user`, `feedback`, `project`, `reference`.
  - Toggle with `/memory`, `autoMemoryEnabled`, or `CLAUDE_CODE_DISABLE_AUTO_MEMORY=1`.
  
  — [Memory](https://code.claude.com/docs/en/memory.md)
- Maintenance tools: `/doctor` proposes trims for a checked-in CLAUDE.md, cutting what Claude can derive from code. `/doctor prompt-audit` (v2.1.283+) audits CLAUDE.md, rules, skills, agents and output styles for stale or conflicting instructions. — [Memory](https://code.claude.com/docs/en/memory.md); [Best practices](https://code.claude.com/docs/en/best-practices)
- Compaction: after `/compact`, the project-root CLAUDE.md is re-read from disk. Nested CLAUDE.md files and path-scoped rules reload on demand. Instructions given only in conversation are lost. — [Memory](https://code.claude.com/docs/en/memory.md)
- Best-practice line for CLAUDE.md: "For each line, ask: 'Would removing this cause Claude to make mistakes?' If not, cut it. Bloated CLAUDE.md files cause Claude to ignore your actual instructions!" Use "IMPORTANT" emphasis on one line only. — [Best practices](https://code.claude.com/docs/en/best-practices)

**Skills**
- Locations: `~/.claude/skills/<name>/SKILL.md`, `.claude/skills/<name>/SKILL.md`, nested `<subdir>/.claude/skills/`, plugin `skills/`, and claude.ai-synced skills (`~/.claude/skills/synced/`, read-only; `!` commands are never run). — [Skills](https://code.claude.com/docs/en/skills.md)
- Frontmatter fields: `name`, `description`, `when_to_use` (description + when_to_use are truncated at 1,536 chars combined), `disable-model-invocation`, `user-invocable`, `allowed-tools` (pre-approved for that turn only), `disallowed-tools`, `arguments`, `context: fork` + `agent` (run in a subagent), `background`, `model`, `effort`, `paths` (auto-activate on matching files), `shell` (bash or powershell), `hooks`, `metadata`. — [Skills](https://code.claude.com/docs/en/skills.md)
- Progressive disclosure: keep SKILL.md under about 500 lines and put detail in sibling files (`reference.md`, `scripts/`) referenced from SKILL.md. — [Skills](https://code.claude.com/docs/en/skills.md)
- Dynamic context injection: `` !`git diff HEAD` `` runs before the skill reaches Claude. A non-zero exit aborts the skill, so append `|| true` where needed. Substitutions include `$ARGUMENTS`, `$0..$N`, `${CLAUDE_SKILL_DIR}`, `${CLAUDE_PROJECT_DIR}` and `${CLAUDE_SESSION_ID}`. — [Skills](https://code.claude.com/docs/en/skills.md)
- Lifecycle: once invoked, skill content stays in context. After auto-compaction, the most recent invocation of each skill is preserved, up to its first 5,000 tokens and 25,000 tokens combined across skills. — [Skills](https://code.claude.com/docs/en/skills.md)
- Visibility controls: `skillOverrides` in settings (`on|name-only|user-invocable-only|off`) and `Skill(...)` permission rules. `/skill-doctor` (v2.1.252+) shows unused skills and their context cost. The skill-creator plugin runs evals comparing with-skill and without-skill. — [Skills](https://code.claude.com/docs/en/skills.md)
- Bundled skills include `/code-review`, `/batch` (splits a change across 5–30 subagents, each in its own worktree), `/debug`, `/loop`, `/verify`, `/run`, `/doctor`, `/claude-api` and `/simplify`. Use `disable-model-invocation: true` for workflows with side effects such as deploy, commit or messaging. — [Skills](https://code.claude.com/docs/en/skills.md); [Best practices](https://code.claude.com/docs/en/best-practices)

**Subagents**
- Locations and priority: managed > `--agents` CLI JSON > `.claude/agents/` > `~/.claude/agents/` > plugin `agents/`. — [Subagents](https://code.claude.com/docs/en/sub-agents.md)
- Frontmatter fields: `name`, `description` (add "use proactively" to encourage automatic delegation), `tools`/`disallowedTools`, `model` (`sonnet|opus|haiku|fable|inherit|<id>`), `permissionMode`, `maxTurns`, `skills` (preloaded in full), `mcpServers`, `hooks`, `memory` (`user|project|local`, which injects the first 200 lines or 25KB of its own MEMORY.md), `background`, `omitClaudeMd`, `effort`, `isolation: worktree`, `color`, `initialPrompt`. — [Subagents](https://code.claude.com/docs/en/sub-agents.md)
- Built-in agents:
  - Explore: read-only, with thoroughness levels quick, medium and very thorough. It omits CLAUDE.md and git status.
  - Plan
  - general-purpose
  - `claude-code-guide` (Haiku)
  
  — [Subagents](https://code.claude.com/docs/en/sub-agents.md)
- Limits:
  - Nesting depth defaults to 3 (`CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH`).
  - Up to 20 subagents can run concurrently by default (`CLAUDE_CODE_MAX_CONCURRENT_SUBAGENTS`).
  - Every subagent can be forced onto one model with `CLAUDE_CODE_SUBAGENT_MODEL` + `CLAUDE_CODE_SUBAGENT_MODEL_FORCE=1`.
  - Background subagents lose some tools.
  - Plugin subagents ignore `hooks`, `mcpServers` and `permissionMode`.
  
  — [Subagents](https://code.claude.com/docs/en/sub-agents.md)
- Fork versus subagent: `/subtask` forks the current conversation, with full context and a shared cache. A normal subagent starts fresh and isolated. Use a fork when the side task needs the conversation history; use a subagent for verbose, self-contained or permission-restricted work. — [Subagents](https://code.claude.com/docs/en/sub-agents.md)
- Anthropic engineering guidance: sub-agents should return "condensed, distilled summaries (typically 1,000–2,000 tokens)". Agents should also do just-in-time retrieval and keep structured notes (e.g., NOTES.md) outside the context window. — [Anthropic Engineering: Effective context engineering for AI agents (2025-09-29)](https://www.anthropic.com/engineering/effective-context-engineering-for-ai-agents)

**Related and newer orchestration features**
- Dynamic workflows (scripts Claude writes that run many subagents in the background) are for work that outgrows a handful of subagents.
- Cross-session messaging, agent view (`claude agents`, research preview), and agent teams (experimental, needs `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=1`). Agent teams use about 7x the tokens of a standard session when teammates run in plan mode.

— [Features overview](https://code.claude.com/docs/en/features-overview); [Best practices](https://code.claude.com/docs/en/best-practices); [Costs](https://code.claude.com/docs/en/costs)

**Output styles and plugins**
- Output styles set role, tone and format for the whole session. One style is active at a time; switch with `/output-style`. They are cache-safe since v2.1.251. — [Features overview](https://code.claude.com/docs/en/features-overview); [Prompt caching](https://code.claude.com/docs/en/prompt-caching.md)
- Plugins bundle skills, hooks, subagents and MCP servers. Their skills are namespaced (`/plugin:skill`). Install with `/plugin`; the official marketplace includes code-intelligence (LSP) plugins that cut file reads and report type errors after edits. — [Features overview](https://code.claude.com/docs/en/features-overview); [Best practices](https://code.claude.com/docs/en/best-practices)

### Inferences
- The Tycoon repo's existing structure already matches the official layering:
  - an always-read core under a 12KB budget
  - path-scoped `.claude/rules/`
  - procedures in `.claude/skills/`
  - auto memory for preferences only
  
  The main levers left are moving "every time" rules into hooks and moving verbose work (Unity log reading, test runs) into subagents or skills with `context: fork`.
- Skills with side effects (commit or push, publishing artifacts, Unity bake menus) should set `disable-model-invocation: true`. That keeps their descriptions out of context and stops accidental auto-invocation.

### Gaps
- I did not fetch the output-styles and plugins pages directly. The details above come from the overview pages.
- There are no official numbers on how much adherence drops as CLAUDE.md grows past 200 lines. The guidance is qualitative.

## 3. Headless and unattended use: `claude -p`, Agent SDK, GitHub Actions, scheduling, background tasks, remote, and safety

### Takeaway
There are four tiers of automation:
1. **In-session**: `/loop`, cron tools, `/goal`, Monitor, background subagents.
2. **Local scheduled**: Desktop scheduled tasks; the machine must be awake.
3. **Scripted**: `claude -p`, or the Agent SDK in Python/TypeScript.
4. **Cloud**: routines (schedule, API and GitHub triggers), GitHub Actions (`anthropics/claude-code-action@v1`), and cloud sessions.

Safe unattended runs combine `--permission-mode dontAsk` or `auto`, narrow `--allowedTools`, `--permission-prompts none`, and the sandbox or a container. `bypassPermissions` belongs in isolated containers or VMs only.

### Cited Findings
**Headless `claude -p`**
- `claude -p "<prompt>"` runs non-interactively and exits 0 or non-zero.
  - `--output-format text|json|stream-json`; JSON includes `result`, `session_id` and `total_cost_usd`.
  - `--json-schema` places structured output in `structured_output`.
  - `--continue` / `--resume <session_id>` continue a conversation; `--append-system-prompt` adds instructions; `--allowedTools` uses permission-rule syntax (e.g., `Bash(git diff *)`).
  - Piped stdin is capped at 10MB.
  
  — [Headless](https://code.claude.com/docs/en/headless)
- `--bare` skips auto-discovery of hooks, skills, commands, subagents, plugins, MCP, auto memory and CLAUDE.md, for reproducible CI. It "will become the default for `-p` in a future release". It requires `ANTHROPIC_API_KEY` and does not use subscription OAuth. Context is passed explicitly via `--settings`, `--mcp-config`, `--agents`, `--plugin-dir` and `--append-system-prompt-file`. — [Headless](https://code.claude.com/docs/en/headless)
- Security note: without `--bare`, a `-p` run executes the project's `.claude/settings.json` hooks and connects `.mcp.json` servers "even in a folder you've never trusted". There is no trust dialog in `-p`. — [Headless](https://code.claude.com/docs/en/headless)
- `--permission-prompts none` (v2.1.259+) is for unattended jobs. Anything that would prompt is denied unless a `PermissionRequest` hook allows it, Claude is told not to retry, and `AskUserQuestion` is removed. — [Headless](https://code.claude.com/docs/en/headless)
- Background work in `-p`:
  - Background Bash tasks are killed about 5 s after the result.
  - Background subagents and workflows keep the process alive, up to a 10-minute idle ceiling (`CLAUDE_CODE_PRINT_BG_WAIT_CEILING_MS`).
  - SIGTERM exits with code 143.
  
  — [Headless](https://code.claude.com/docs/en/headless)
- Fan-out recipe: loop over a file list with `claude -p "Migrate $file ... Return OK or FAIL." --allowedTools "Edit,Bash(git commit *)" --permission-mode dontAsk`. Test on 2–3 files first. Alternatively, `/batch <instruction>` runs 5–30 subagents in worktrees. — [Best practices](https://code.claude.com/docs/en/best-practices)
- Cost cap flag: `--max-budget-usd` exists, and the session cost figure counts toward it. — [Costs](https://code.claude.com/docs/en/costs)

**Agent SDK**
- The Agent SDK has the same tools, agent loop, context management, hooks (as callbacks), subagents, MCP, permissions, sessions, skills and memory as Claude Code, packaged as Python and TypeScript libraries (repos `claude-agent-sdk-python` and `claude-agent-sdk-typescript`). Other languages run the CLI as a subprocess with `-p --output-format json`. — [Agent SDK overview](https://code.claude.com/docs/en/agent-sdk/overview.md)
- Third-party products built on the SDK may not offer claude.ai login or rate limits; they must use API keys. — [Agent SDK overview](https://code.claude.com/docs/en/agent-sdk/overview.md)

**GitHub Actions**
- Setup: `anthropics/claude-code-action@v1`, installed with `/install-github-app` (github.com only; needs `gh`) or manually.
- Secrets: `ANTHROPIC_API_KEY`, or `CLAUDE_CODE_OAUTH_TOKEN` from `claude setup-token` (subscription billing).
- Modes: interactive mode responds to `@claude` mentions. Automation mode runs when a `prompt` input is given, on any event including `schedule` cron.
- `claude_args` passes CLI flags (`--max-turns`, `--model`, `--allowedTools`, `--mcp-config`).
- `prompt` can be a skill (`/skill-name`) or a plugin skill.

— [GitHub Actions](https://code.claude.com/docs/en/github-actions.md)

- Action guardrails:
  - Only users with write access can trigger runs, and bots are rejected unless listed in `allowed_bots`.
  - Scheduled workflows run only from the default branch and are disabled after 60 days without activity in public repos.
  - Commits made with the default `GITHUB_TOKEN` do not trigger CI.
  
  Cost advice: concise CLAUDE.md, `--max-turns`, workflow timeouts, concurrency controls. Runs consume Actions minutes plus tokens. — [GitHub Actions](https://code.claude.com/docs/en/github-actions.md)

**Scheduling**
- `/loop`:
  - `/loop 5m <prompt>` runs on a fixed cron interval. `/loop <prompt>` is self-paced, with Claude picking a delay of 1 min–1 h each iteration.
  - A bare `/loop` runs a built-in maintenance prompt (continue unfinished work, tend the PR, run cleanup), or `.claude/loop.md` / `~/.claude/loop.md` when present; `loop.md` is truncated beyond 25,000 bytes.
  - The underlying tools are `CronCreate`/`CronList`/`CronDelete`. A session holds at most 50 tasks, and recurring tasks expire after 7 days.
  - Jitter: recurring tasks fire up to 30 min late, or half the interval when that is shorter.
  - Tasks fire only while the session is open and idle, with no catch-up.
  - Disable with `CLAUDE_CODE_DISABLE_CRON=1`.
  
  — [Scheduled tasks](https://code.claude.com/docs/en/scheduled-tasks.md)
- Comparison of the three scheduling options:

  | | Cloud routines | Desktop tasks | `/loop` |
  |---|---|---|---|
  | Minimum interval | 1 h | 1 min | 1 min |
  | Needs machine on | No | Yes | Yes |
  | Needs open session | No | No | Yes |
  | Local file access | No (fresh clone) | Yes | Yes |
  | Permission prompts | None (fully autonomous) | Configurable per task | Inherits from session |

  — [Scheduled tasks](https://code.claude.com/docs/en/scheduled-tasks.md)
- Desktop scheduled tasks:
  - They run only while the Desktop app is open and the machine is awake. After sleep, the app runs exactly one catch-up for the most recent missed run within 7 days.
  - Each task has its own permission mode and model, plus an optional isolated worktree. "Always allow" approvals persist per task.
  - The prompt is stored at `~/.claude/scheduled-tasks/<task-name>/SKILL.md`.
  - A Manual-mode run that hits an unapproved tool stalls until approved.
  
  — [Desktop scheduled tasks](https://code.claude.com/docs/en/desktop-scheduled-tasks.md)
- Routines (research preview):
  - Defined at claude.ai/code/routines or via `/schedule` in the CLI; run on Anthropic cloud on Pro, Max, Team and Enterprise plans.
  - Triggers: schedule (minimum 1 h; one-off runs possible), API (`POST .../routines/<id>/fire` with a bearer token and beta header `experimental-cc-routine-2026-04-01`; the `text` payload arrives wrapped as untrusted data), and GitHub (`pull_request.*`, `release.*`, with filters).
  - Each run clones the repo, pushes to `claude/`-prefixed branches, and uses claude.ai connectors (not local `claude mcp add` servers, though a committed `.mcp.json` works).
  - Limits: 100 scheduled runs per hour per account; 30 Run-now/API fires per routine per hour.
  - A green run status means only "no infra error", so the transcript must be checked.
  
  — [Routines](https://code.claude.com/docs/en/routines.md)
- Other unattended or long-running mechanisms in the best-practices page:
  - `/goal` (evaluator-checked completion condition)
  - Monitor tool (streams background script output instead of polling)
  - Channels (CI pushes events into a running session)
  - Remote Control (continue a local session from any device)
  - Cloud sessions
  - Worktrees (`--worktree`) for parallel sessions without edit collisions
  
  — [Best practices](https://code.claude.com/docs/en/best-practices); [Scheduled tasks](https://code.claude.com/docs/en/scheduled-tasks.md)

**Permissions and sandboxing**
- Permission modes:

  | Mode | What runs without asking | Intended use |
  |---|---|---|
  | `default` (Manual) | Reads only | Reviewing every action |
  | `acceptEdits` | Reads, file edits, mkdir/touch/mv/cp | Iterating on reviewed code |
  | `plan` | Reads (plus classifier-approved commands when auto is available) | Exploring before changing |
  | `auto` | Everything, with a classifier model reviewing actions | Long tasks |
  | `dontAsk` | Reads and pre-approved tools; everything else denied | Locked-down CI |
  | `bypassPermissions` | Everything | "Isolated containers and VMs only" |

  — [Permission modes](https://code.claude.com/docs/en/permission-modes.md)
- Auto mode is the built-in starting mode for interactive sessions from v2.1.283. — [Permission modes](https://code.claude.com/docs/en/permission-modes.md); [Best practices](https://code.claude.com/docs/en/best-practices)
- The auto-mode classifier blocks by default:
  - `curl | bash`
  - sending sensitive data externally
  - production deploys and migrations
  - force push
  - `git reset --hard`, `git checkout -- .`, `git clean -fd`
  - `git commit --amend` on commits not made in this session
  - `terraform destroy`
  - IAM changes
  
  After 3 consecutive blocks or 20 total, auto mode pauses and returns to prompting. These thresholds are not configurable. — [Permission modes](https://code.claude.com/docs/en/permission-modes.md)
- Protected paths are never auto-approved except in bypassPermissions. These are writes to repo state and Claude's own config. — [Permission modes](https://code.claude.com/docs/en/permission-modes.md)
- Full unattended pattern: `claude -p "<prompt>" --dangerously-skip-permissions` requires a container, VM or the sandbox runtime, and on Linux/macOS should run as a non-root user. — [Permission modes](https://code.claude.com/docs/en/permission-modes.md)
- Sandboxed Bash:
  - Supported on macOS (Seatbelt), Linux and WSL2 (bubblewrap + socat).
  - "On native Windows, Claude Code runs commands unsandboxed"; use WSL2 instead.
  - Built on the open-source `@anthropic-ai/sandbox-runtime`.
  - Covers shell commands only: the Read/Edit/WebFetch tools, MCP servers and hooks run outside it.
  - Network allowlist `sandbox.network.allowedDomains` starts empty. `autoAllowBashIfSandboxed` defaults true. `excludedCommands` (e.g., `docker compose *`) runs listed commands unsandboxed.
  - If the sandbox cannot start, commands run unsandboxed unless `sandbox.failIfUnavailable` is set.
  
  — [Sandboxing](https://code.claude.com/docs/en/sandboxing.md)

### Inferences
- On this Windows 11 machine the OS sandbox is not available natively, so unattended local runs should rely on `dontAsk` plus explicit allowlists, or auto mode with `--permission-prompts none`, plus PreToolUse guard hooks. Truly hands-off `bypassPermissions` should run only in WSL2 or a container.
- Unity-dependent tasks (editor compile, play-mode capture) need the local machine. That rules out cloud routines and GitHub-hosted runners for them, unless there is a self-hosted runner with Unity. Desktop scheduled tasks or `/loop` are the practical local schedulers. Cloud routines suit pure-text work such as docs drift, issue triage and PR review.
- Recommended headless hygiene for scripts: `--bare` (with an API key), explicit `--settings`/`--mcp-config`, `--output-format json`, `--max-turns`/`--max-budget-usd`, and parsing `permission_denials`.

### Gaps
- I did not fetch the separate pages for Remote Control, channels, agent view or cloud environments, so their limits are not detailed here.
- The exact semantics and limits of `--max-budget-usd` were not verified. Only its existence is confirmed, from the costs page.
- I did not check whether routines and Desktop tasks have special support for Windows paths.

## 4. Cost and token management

### Takeaway
Context is "the most important resource to manage". The official levers:
- `/clear` between tasks.
- Keep CLAUDE.md under 200 lines and move procedures into skills.
- Push verbose work into subagents and preprocessing hooks.
- Choose Sonnet or Haiku over Opus where possible, and use effort levels.
- Prefer CLI tools over MCP.
- Avoid mid-session cache-busting (model, effort or MCP changes).
- Watch `/usage` and `/context`.

### Cited Findings
**Benchmarks and measurement**
- Enterprise average is about $13 per developer per active day and $150–250 per month; 90% of users stay under $30 per active day. — [Costs](https://code.claude.com/docs/en/costs)
- Measurement tools:
  - `/usage`: session cost, plus a `Prompt cache (main)` line with hit %, misses, warm/cold and TTL, and attribution by skill, subagent, plugin, MCP server and `/loop` task.
  - `/context` and `/context all`: per-tool token use.
  - `/insights`: HTML report on friction across up to 200 sessions.
  - OpenTelemetry export.
  
  — [Costs](https://code.claude.com/docs/en/costs)

**Token-reduction levers**
- From the costs page:
  - `/clear` between tasks (and `/rename` first so the session can be found).
  - `/compact <focus>`, plus "Compact instructions" in CLAUDE.md.
  - Sonnet for most coding and Opus for complex reasoning; `model: haiku` for simple subagents.
  - `/effort` or `MAX_THINKING_TOKENS` on fixed-budget models. Thinking cannot be disabled on Opus 5.5, Sonnet 5.5 or Fable.
  - Prefer CLI tools (`gh`, `aws`, `sentry-cli`) over MCP.
  - Disable unused MCP servers.
  - Install code-intelligence plugins.
  - Preprocessing hooks.
  - Delegate verbose operations to subagents.
  - Specific prompts and plan mode for complex tasks.
  
  — [Costs](https://code.claude.com/docs/en/costs)
- Why usage climbs in long sessions: the full context is resent each request, cache misses follow breaks, scheduled tasks fire on idle sessions, `/goal` check-ins occur, and subagents and teammates add requests. `/compact` is itself a large request, while `/clear` costs nothing. — [Costs](https://code.claude.com/docs/en/costs)

**Prompt caching**
- Mechanics: prefix-matching, with the system prompt and tools first, then project context (CLAUDE.md, memory, unscoped rules), then the conversation. — [Prompt caching](https://code.claude.com/docs/en/prompt-caching.md)
- Cache-invalidating actions: switching models (including `opusplan` plan-mode toggles), changing effort (except on Opus 5.5, Sonnet 5.5 and Fable 5.1 with API or subscription), turning on fast mode, connecting MCP servers when tools are not deferred, enabling plugins that ship MCP, denying a whole tool without tool search, compaction, many images, and upgrading Claude Code. — [Prompt caching](https://code.claude.com/docs/en/prompt-caching.md)
- Cache-safe actions: editing files, editing CLAUDE.md mid-session (which also does not apply until `/clear`, `/compact` or a restart), changing permission mode, changing output style, invoking skills, `/rewind`, and spawning subagents. — [Prompt caching](https://code.claude.com/docs/en/prompt-caching.md)
- TTL:
  - On a subscription within plan usage, the main conversation gets 1 h and subagents get 5 min. With usage credits or an API key, everything gets 5 min.
  - Override with `promptCacheTtl` / `CLAUDE_CODE_PROMPT_CACHE_TTL` and `subagentPromptCacheTtl` (v2.1.242+), or `ENABLE_PROMPT_CACHING_1H=1` / `FORCE_PROMPT_CACHING_5M=1`.
  - Tip: "Pick your model and effort level at the top of a session, then save `/compact` for natural breaks."
  
  — [Prompt caching](https://code.claude.com/docs/en/prompt-caching.md)
- The prompt-caching page links a design-rationale post, "Lessons from building Claude Code: Prompt caching is everything", which covers plan mode, deferred tool loading and compaction. — [Prompt caching](https://code.claude.com/docs/en/prompt-caching.md) (post itself not fetched)
- Context-engineering principles from Anthropic engineering: context rot, an attention budget, just-in-time retrieval, compaction, structured note-taking, and subagents returning 1–2k-token summaries. — [Anthropic Engineering (2025-09-29)](https://www.anthropic.com/engineering/effective-context-engineering-for-ai-agents)

**Agent teams**
- Agent teams use about 7x the tokens when teammates are in plan mode. Use Sonnet for teammates, keep teams small, and shut them down when done. — [Costs](https://code.claude.com/docs/en/costs)

### Inferences
- For a Max-plan solo developer, the main cost risk is session sprawl and cache busting, not list price. Useful habits:
  - one task per session with `/clear`
  - no mid-task `/model` switches
  - Haiku or Sonnet for exploratory and log-reading subagents
  - `disable-model-invocation` on rarely used skills
  - `/usage` attribution to find heavy `/loop`s or MCP servers

### Gaps
- No official per-model price table was fetched. Pricing is deferred to claude.com/pricing and the platform pricing page.
- The "Prompt caching is everything" blog post was not read directly.

## 5. MCP servers commonly used in dev automation

### Takeaway
Official guidance is to connect MCP only where no good CLI exists, since CLI tools are more context-efficient. MCP tool schemas are now deferred by default via tool search, which reduces idle cost. Community rankings consistently list GitHub, Playwright or Chrome DevTools (browser), Context7 (up-to-date library docs), databases (Postgres), Sentry, Linear/Notion/Slack, and Figma.

### Cited Findings
- Adding servers: `claude mcp add --transport http <name> <url>` (e.g., Notion at `https://mcp.notion.com/mcp`). Project-scoped servers go in a committed `.mcp.json`. Precedence is local > project > user. `/mcp` shows status. — [Best practices](https://code.claude.com/docs/en/best-practices); [Features overview](https://code.claude.com/docs/en/features-overview)
- Tool search is on by default, so idle MCP tools cost only names and instructions. Even so, "Prefer CLI tools when available: Tools like `gh`, `aws`, `gcloud`, and `sentry-cli` are still more context-efficient than MCP servers". — [Costs](https://code.claude.com/docs/en/costs)
- Hooks can call MCP tools directly (`type: "mcp_tool"`). For example, a post-edit hook can send a Slack notification when critical files change. — [Hooks reference](https://code.claude.com/docs/en/hooks); [Features overview](https://code.claude.com/docs/en/features-overview)
- Routines use claude.ai connectors, not local MCP servers; a committed `.mcp.json` also works. All connectors are included by default, and Claude can use their write tools without asking. — [Routines](https://code.claude.com/docs/en/routines.md)
- MCP-tool permission behavior:
  - MCP tools marked `requiresUserInteraction` always prompt, even in `dontAsk` and Desktop tasks, which stalls unattended runs.
  - The `mcp__<server>__<tool>` naming is used in hooks, permissions and `allowedTools`.
  
  — [Headless](https://code.claude.com/docs/en/headless); [Desktop scheduled tasks](https://code.claude.com/docs/en/desktop-scheduled-tasks.md)
- Community rankings for 2026 commonly list:
  - GitHub MCP
  - Context7 (fresh library docs)
  - Playwright (browser automation and E2E)
  - Chrome DevTools MCP (console, network, DOM)
  - PostgreSQL
  - Sentry
  - Firecrawl
  - Slack and Notion
  - Composio as a multi-SaaS gateway
  
  — [CodeConductor (2026)](https://codeconductor.ai/blog/claude-code-mcp-servers/); [Composio blog](https://composio.dev/blog/best-mcp-servers-claude-code-codex); [heyuan110 (2026-03-05)](https://www.heyuan110.com/posts/ai/2026-03-05-best-mcp-servers-claude-code/) (aggregator/vendor blogs; Composio is self-promotional; treat rankings as opinion)
- The same community sources warn that "every connected server spends context on every turn, so the useful question is which ones earn their cost". — [CodeConductor (2026)](https://codeconductor.ai/blog/claude-code-mcp-servers/)

### Inferences
- For this Unity project, the high-value integrations are already CLI-based: `gh`, the Unity CLI connected through `com.unity.pipeline`, Codex CLI and ACE-Step. That fits the official "CLI first" advice. MCP is worth adding mainly for browser/visual verification (Playwright or Chrome DevTools-type tools, already present via browser-use and claude-in-chrome) and for doc lookup (Context7 is already installed as a plugin).

### Gaps
- No authoritative usage statistics for MCP servers were found; the rankings come from vendor and aggregator blogs.
- Unity-specific MCP servers (e.g., Unity's own MCP via the Unity CLI) were not researched in this pass.
- The official `managed-mcp` and `mcp` pages were not fetched in full, so remote-MCP OAuth details and the tool-search threshold config are not covered.
