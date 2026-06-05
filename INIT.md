# INIT.md — AI-First Project Bootstrap

> **This is the FIRST file an AI agent should read after cloning this repository.**
> It contains step-by-step instructions for the AI to automatically set up the entire development environment for the user.

**Important Note for Cloning:**
To use this AI Harness version of the template, you must clone the `iris` branch using degit:
```bash
npx degit ethavais/egenesis#iris
or
npx degit ethavais/egenesis#iris --force
```

---

## For AI Agents: Auto-Setup Instructions

When you read this file, execute ALL steps below **in order**, without asking the user for confirmation. This is a one-time bootstrap.

### Step 1: Read Project Rules

Read `AGENTS.md` in the project root. This file defines your coding standards, available agents, and workflows. Follow it for all future work in this project.

### Step 2: Read Evolution Rules

Read `.ecc/EVOLUTION.md`. This contains lessons learned from past failures. Every rule in that file is **non-negotiable** — never repeat a documented mistake.

### Step 3: IDE Auto-Configuration

The `.ecc/ide/` folder contains pre-configured settings for various AI IDEs (`.cursor`, `.claude`, `.gemini`, `.zed`, etc.).
Identify which IDE you are currently running in. Copy the corresponding folder to the project root. For example, if running in Cursor:
```powershell
Copy-Item -Recurse -Force ".\.ecc\ide\.cursor" ".\"
```

**AGENT INSTRUCTIONS (IDE):**
If you are operating in a VS Code-based environment, you MUST update (or create) `.vscode/settings.json` to ensure the AI Harness folders are hidden in the explorer to keep the workspace clean and focused. **IMPORTANT:** Do not blindly overwrite existing settings; carefully merge the `files.exclude` keys from the `.ecc/ide/` template with the project's existing exclusions.

At a minimum, ensure the following core harness folders are set to `true` to exclude/hide them:
```json
{
    "files.exclude": {
        "**/.git": true,
        "**/.code-review-graph": true,
        "**/.ecc": true
        // ... (Keep or merge any other specific exclusions defined in the .ecc/ide template)
    }
}
```

### Step 4: Install code-review-graph MCP

This tool builds a structural knowledge graph of the codebase for smarter AI-assisted code reviews.

```bash
pip install code-review-graph
code-review-graph install
```

**CRITICAL:** After install, delete any auto-generated files like `GEMINI.md` or `CLAUDE.md` — all instructions are already consolidated in `AGENTS.md`.

Read `.ecc/GRAPHS.html` for detailed MCP usage documentation.

### Step 5: Configure Git Ignore

To prevent accidentally committing thousands of files from `.ecc/` or `.code-review-graph/` in the initial bootstrap, we must ignore them **before** running `git add .` in the graph build step.

```powershell
# Automatically add AI Harness directories to .gitignore if using git
if (Test-Path .git) {
    $ignores = @(".ecc/", ".code-review-graph/")
    $existing = @()
    if (Test-Path .gitignore) {
        $existing = Get-Content .gitignore
    } else {
        New-Item -ItemType File -Path .gitignore -Force | Out-Null
    }
    $needed = @()
    foreach ($ignore in $ignores) {
        if ($existing -notcontains $ignore) {
            $needed += $ignore
        }
    }
    if ($needed.Count -gt 0) {
        if ($existing.Count -gt 0 -and $existing[-1] -ne "") {
            Add-Content -Path .gitignore -Value ""
        }
        Add-Content -Path .gitignore -Value "# AI Harness Tools"
        foreach ($item in $needed) {
            Add-Content -Path .gitignore -Value $item
        }
    }
}
```

### Step 6: Build the Knowledge Graph

```bash
git add .
code-review-graph build
code-review-graph status
```

**VERIFY:** The `status` output must show non-zero Nodes, Edges, and Files. If it shows 0, the files were not git-tracked. Run `git add .` first. (See EVOLUTION.md RULE-001)

**AGENT INSTRUCTIONS (CRITICAL):**
When you complete this step, you **MUST** print the complete output of `code-review-graph status` to the user and guide them on how it works:
1. **Verification**: Confirm the exact number of nodes, edges, and files indexed.
2. **How we use it**: Explain that you (the AI) will prioritize the `code-review-graph` MCP tools (like `query_graph`, `semantic_search_nodes`, `detect_changes`, `get_impact_radius`) for code exploration and refactoring because it is faster, cheaper, and provides structural context (callers/callees, imports, tests) that simple grep scans cannot.
3. **User Reference**: Remind the user they can view the detailed [.ecc/GRAPHS.html](file:///.ecc/GRAPHS.html) interactive graph user guide.

*** After all, initial commit ***

```powershell
git commit -m "chore: apply AI harness and update gitignore"
```

### Step 7: Confirm to User

Tell the user:
- IDE configuration has been extracted from `.ecc/ide`
- `.gitignore` has been correctly updated
- code-review-graph MCP is installed and the knowledge graph is built
- Show the `code-review-graph status` output as proof
- This `INIT.md` file can now be deleted and start coding
