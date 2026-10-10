1. Separate UI elements from business logic behavior
2. Document functions that you create
3. Name variables reasonable names, so that logic could be followed along
4. Keep separate classes in their separate files
5. Use a `tmp/` folder in the project root (`./tmp/`) as the scratch directory for temporary files, downloads, and tool output; never the WSL system `/tmp`.







For versioning control you as an agent will use use preconfigured "gh" (access with granular permissions). 

Make sure you follow the set development steps:

1\. Ensure you're on the latest master branch

2\. Implement user's request and verify - do not commit if tests fail. Do not attempt to circumvent the failure just to get pass result - that is not a test fix

3\. Create a new "feature/" branch

4\. stage relevant files

5\. create commit for the changes

6\. User may ask to add some small refinements and fixes - create new commits for those

7\. Once user is satisfied - ensure all changes are committed and do the push

8\. Create PR - give title and subscription point by point, keep it concise and to the point

9\. User may make a comment in github directly - if something needs to be changed, then they can ask you to check the PR comments and act on the problem described in the PR comment

## Lessons Learned

### 2026-10-11 - WSL bash vs. native-Windows permission paths (`/mnt` false "external directory" prompts)

**Symptom.** The bash tool flags the project itself as an *external directory* (`external_directory` / `external_directory_read`) when commands reference it as `/mnt/d/projects/mail_integrator/...`. The agent then went looking through `/mnt/c`, `/mnt/d`, etc., and every such command raised more permission prompts.

**Root cause (two path worlds in one session).**
- Pi is a native **Windows** process: the session record says `cwd: "D:\\projects\\mail_integrator"`, so `process.platform === "win32"` and `@gotgenes/pi-permission-system` selects the **win32 path flavor** (`src/path/path-flavor.ts` -> `pathFlavorForPlatform`).
- The **bash** tool is not Git Bash: Git for Windows is not installed, so pi falls back to `bash.exe` on `PATH` = `C:\Windows\System32\bash.exe`, the WSL launcher. Bash therefore runs **inside Ubuntu**, where the project is `/mnt/d/projects/mail_integrator` and Windows drives are mounted under `/mnt/<letter>/...`.
- On win32 the permission system assumes **Git Bash / MSYS** mount semantics for POSIX-shaped bash tokens. `classifyWin32BashToken` only translates single-letter mounts (`/c/x`, `/cygdrive/c/x`) to `C:\x`. WSL's `/mnt/d/...` spelling does not match that, so it is classified `posix-absolute`, kept literal, and never mapped to `D:\projects\mail_integrator`. Containment against the cwd `D:\projects\mail_integrator` then fails, so `external_directory: ask` fires - even for the project's own directory.
- The built-in file tools (`read`/`edit`/`write`/`ls`/`find`/`grep`) are **not** affected: they run their `path` argument through `toolShellPath` / `normalizeWindowsShellPath`, which *does* recognize `/mnt/c/...`. That is why `read` on a Windows path is auto-allowed while the equivalent bash command is flagged.
- Bash spelling trap that started the detour: `C:/...` and `C:\...` do not resolve in WSL, `/c/...` does not exist in WSL (that is MSYS/Git-Bash only), and only `/mnt/c/...` works - which is exactly the spelling the permission system misreads.

**Rules for future agent sessions.**
1. Treat the bash tool as **Linux/WSL**, not Windows. Its cwd is already `/mnt/d/projects/mail_integrator`; prefer relative paths so no external path is involved.
2. Never use `C:/...`, `C:\...`, or `/c/...` inside the bash tool - they will not resolve. Only `/mnt/c/...` works in WSL.
3. For project files use the built-in `read`/`edit`/`write`/`ls`/`find`/`grep` tools with Windows paths (`D:\projects\mail_integrator\...`); pi translates them correctly and keeps them inside cwd.
4. Use the project-root scratch dir `./tmp/` (git-ignored) for temporary files, downloads, and tool output, and set `TMPDIR=./tmp` for tools that honor it. Do not use the WSL system `/tmp`: it is outside the project (so it trips the `external_directory` gate) and is wiped when WSL restarts.
5. If a bash command genuinely needs `/mnt/...`, expect an `external_directory` prompt. That prompt is this flavor mismatch, not a real out-of-project access. Do not "work around" it by exploring `/mnt`; use the built-in tools or ask the user.
6. This is an environment-level defect in `@gotgenes/pi-permission-system` (win32 flavor + WSL bash), not a repo bug. Real fixes live outside the code: install Git for Windows / set `shellPath` to Git Bash so pi's assumed MSYS mounts match, or add an explicit project allow rule such as `"external_directory": { "/mnt/d/projects/mail_integrator/**": "allow" }`.

