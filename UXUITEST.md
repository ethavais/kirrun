# Visual UI/UX Testing & Verification Protocol

This document defines the visual testing process for the Kirun project. All UI changes must be verified strictly by capturing and reviewing browser screenshots. Analyzing layouts from source code alone is insufficient, as it does not guarantee browser rendering accuracy.

---

## 1. Core Principles

- **Visual Evaluation Only**: Always analyze layouts using actual rendered screenshots. Do not assume styling is correct based solely on the written CSS/HTML.
- **Cache Busting**: Append timestamp query parameters to static asset imports (e.g. `app.css?v=ticks`) to ensure changes are loaded immediately by the browser.
- **Clean Builds**: When styling does not appear to reflect, run a full `dotnet clean` to wipe MSBuild static assets caches before relaunching.
- **Interactive Verification**: Use browser subagents to perform clicks, hovers, and data expanding, then capture screenshots in those specific states.

---

## 2. Overall Test Protocol

For every UI edit or feature addition, follow these steps:

1. **Clean & Rebuild**:
   ```powershell
   dotnet clean src/Kirun.App/Kirun.App.csproj
   dotnet build src/Kirun.App/Kirun.App.csproj
   ```
2. **Launch Application**:
   Run the dev server in the background.
3. **Execute Browser Subagent**:
   Instruct the browser subagent to:
   - Navigate to the page.
   - Wait for the DOM and styles to load.
   - Perform actions (click info buttons, hover category stars, etc.).
   - Capture high-resolution screenshots of each state.
4. **Screenshot Review**:
   - Inspect the captured images.
   - Check alignment, margins, text truncation, backgrounds, and borders.
   - Look for unintended shapes (such as browser-default oval backgrounds on buttons).
5. **Adjust & Repeat**:
   If any rendering issue is detected, modify the CSS/HTML and repeat the loop.
6. **Publish Walkthrough**:
   Document the final tested visual states in `walkthrough.md` with links to the verified screenshots and action recordings.

---

## 3. UI Quality Checklist

- **Favicon**: Verify the page tab loads the custom `favicon.ico` (copied from `data/emew.ico`).
- **Star Button UI**: Pin/unpin buttons must be clean, transparent circular buttons. They must have no default grey background or oval shape.
- **Details Drawer**:
  - Toggled via a circular `ⓘ` icon.
  - Sits inline next to badges when closed.
  - Opens as a full-width block below the header badges, pushing sibling rows down cleanly without layout squishing.
- **Port Badges**:
  - Show port numbers as green badges in the card header.
  - Do not duplicate port listings below the header.
- **Command Line**:
  - Resolve and display full execution command lines within the expanded details panel.
  - Fall back cleanly to `N/A` if no command line is available.
