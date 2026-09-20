# CrashTrace

**A crash diagnostic tool for Windows programs.**

CrashTrace launches a program under a debugger, watches it from the moment it starts, and when something goes wrong, produces a detailed report explaining what happened. It works with both 32-bit and 64-bit Windows executables.

This README is split into two parts. If you just want to use the tool, read Part 1. If you want to understand or modify the code, jump to Part 2.

---

## Part 1 - User Guide

### What this tool actually does

If you have a program that opens and closes by itself, or complains about a missing library, or crashes before its window even appears - CrashTrace is like a security camera pointed at that program. It starts the program, records everything that happens inside it, and when it crashes, writes a full report.

Unlike other tools that just sit and watch, CrashTrace is a real debugger. It uses the same debugging API that Windows itself provides to development tools like Visual Studio. This means it sees things that other tools miss.

### What "supervised launch" means

You don't start the program yourself. CrashTrace starts it for you, and stays attached to it from the very first instruction. While the program runs, the tool watches:

- Every system file it loads
- Every thread it creates
- Every exception it raises (including ones the program handles internally and continues from)
- Whether it exits normally, crashes, or hangs

If the program shuts down, CrashTrace already knows why.

### What's in the report

When the session ends, CrashTrace writes two files next to the target executable, both timestamped:

- **A plain text report (`.txt`)** - easy to read, easy to paste into a support forum
- **A Markdown report (`.md`)** - formatted with headings, tables and code blocks, useful if you want to share it on GitHub

The report contains:

- **Result**: Did it crash, exit on its own, or hang?
- **If it crashed**: The status code (like `0xC0000005` for an access violation), the faulting module, the offset inside that module, and a short explanation in plain language
- **CPU registers** at the moment of the crash
- **Memory around the crash site** - a hex dump, plus the state of the memory page (was it free, committed, read-only, executable?)
- **A mini-disassembly** of the instructions right before and after the crash
- **A screenshot** of the target window at the moment of the crash
- **A minidump (`.dmp`)** you can open in WinDbg or Visual Studio for deeper analysis. Note: this file can be large - often tens or hundreds of megabytes, depending on the size of the process.
- **The list of missing dependencies** if any were detected
- **Threads that were active** at crash time
- **All DLLs loaded** during the session
- **Related Windows Event Log entries**
- **Any Windows Error Reporting (WER) reports** that mention the target

### Games and anti-cheat

> **Important:** If the target uses kernel-level anti-cheat (EasyAntiCheat, BattlEye, Vanguard, XignCode, GameGuard, and similar systems), this tool will not work on it. In some cases the game will refuse to launch, and in worse cases your game account may be banned for using a debugger. CrashTrace warns you about this before starting.

For games without kernel anti-cheat, the tool works normally. Both 32-bit and 64-bit games are supported.

### The other two modes

Besides the main "launch and monitor" mode, the tool has two buttons that do purely static analysis:

**DLL Scan** - you pick an EXE or DLL file, and the tool reads its import table without ever running it. It tells you which libraries the program needs and whether each one exists on your disk. It also reads .NET assembly references, if the target is a managed assembly.

**Can I Run It?** - same idea, but focused on system requirements. The tool reads the file and reports:

- The Windows version the program was built for
- The architecture (x86 / x64 / ARM)
- Which Visual C++ runtimes it needs
- Which DirectX components it uses
- Whether it requires administrator privileges (from the manifest)
- Whether it's signed, packed, or has high-entropy sections
- Any DRM or anti-debug strings it finds in the binary

Neither mode runs the program or touches your system. Everything is read-only.

### What it can't do

Being honest about limitations:

- It cannot diagnose stutter, low FPS, or performance problems. It is not a profiler.
- It cannot diagnose the *cause* of audio dropouts or black screens. It does capture a screenshot of the target window at crash time, so if the screen was black at that exact moment, you'll see that - but the tool won't tell you why.
- It does not fix anything. It only diagnoses.
- It cannot catch errors that the target program handles internally and recovers from, unless those errors eventually lead to a crash. First-chance exceptions are logged, but they are not analyzed as the cause of the crash on their own.
- It cannot diagnose network disconnects or lag.
- It does not attach to already-running processes. It only launches new ones.
- It does not work with kernel-level anti-cheat or DRM systems.

### How to use it

1. Right-click the executable and choose **Run as Administrator**. The tool needs debug privileges that only an administrator account has by default.
2. Click **Select** and pick the program you want to test.
3. Click **Start**.
4. Wait for the problem to happen. This could be seconds or minutes depending on the program.
5. If the program crashes or exits, the report appears next to the target executable's folder. There's also a **Copy Analysis** button that puts the whole report on your clipboard.

If you want to stop the session early, click **Stop**. The tool will first try to close the program gracefully, then force-close it after a few seconds.

### Using AI to read the report

The report contains a lot of technical detail. You don't need to understand all of it. The easiest path:

1. Open the `.md` file (it's the most readable version).
2. Copy its entire contents.
3. Paste it into an AI assistant like ChatGPT, Claude, or Gemini.
4. Ask something like: *"Read this crash report and explain in simple terms what went wrong and how I can fix it."*

The report was designed to be self-contained. The AI has everything it needs to give you a useful answer without asking you follow-up questions.

---

## Part 2 - For Developers

### Architecture

CrashTrace is a Windows Forms application written in C# targeting .NET Framework 4.5. It is a user-mode debugger built on the official Windows debugging API:

It is a real debugger, not a passive monitor. It receives every event Windows sends to a debugger: DLL load and unload, thread creation and exit, exceptions (both first-chance and second-chance), and debug string output.

The main executable is built as **x64**. The `RootNamespace` is `TestApp` (a legacy name), but the output assembly is `CrashTrace.exe`.

### What it reads from the target

- **CPU registers** - `EAX..EDI` + `ESP/EBP/EIP` for 32-bit, `RAX..R15` + `RSP/RBP/RIP` for 64-bit
- **Stack dump** - 4 KB starting at `ESP`/`RSP`
- **Memory region info** - via `VirtualQueryEx`, capturing the page state, protection flags, and type
- **Instruction bytes** at the crash address, for the mini-disassembler
- **PEB** - for 32-bit targets, to walk the environment block
- **IAT** - not currently, but `ApiHookDetector` checks the prologue bytes of a fixed set of exported functions

### Disassembly

There are two decoders:

- **BasicDisassembler** - a hand-written decoder for a subset of x86/x64 opcodes. No external dependency. It doesn't cover the whole instruction set, but it covers the ones most likely to appear at a crash site.
- **DisassemblyHelper** - wraps SharpDisasm (a C# port of udis86). It reads 96 bytes before the crash address and 128 bytes after, tries different starting offsets until the disassembly lands exactly on the crash address, and then decodes 8 instructions before the fault + the faulting instruction itself + 8 instructions after.

If SharpDisasm fails for any reason, the tool falls back to BasicDisassembler. The report indicates which engine produced the output.

### Reading .NET exceptions

For managed (.NET) targets, CrashTrace reads the exception object from the crashing thread using ClrMD (`Microsoft.Diagnostics.Runtime`). This gives the exception type, message, and managed stack trace.

There's a constraint here worth understanding: ClrMD loads a native DLL called the **DAC** (Data Access Component). The DAC must match the target process's architecture exactly. If the host is 64-bit and the target is 32-bit, the in-process load fails.

To work around this, the tool ships two helper executables:

- `TestAppHelper32.exe` - built as x86, for 32-bit targets
- `TestAppHelper64.exe` - built as x64, for 64-bit targets

The main process (always x64) tries to read the exception in-process first. If that fails - for example, because the target is 32-bit - it launches the matching helper, passes it the PID, the crashing thread ID, and a temp file path. The helper reads the exception and writes the result as a key-value text file. The main process reads that file back and populates the report fields.

### Module classification

Two static lists in `ModuleClassifier`:

- **SuspiciousModules** - overlays and hooks known to conflict with games (Discord overlay, MSI Afterburner, RivaTuner, Xbox Game Bar, OBS hooks, NVIDIA's capture, etc.)
- **AntiCheatModules** - EasyAntiCheat, BattlEye, Vanguard, XignCode, GameGuard, PunkBuster, and others. These are informational only - their presence is not a problem by itself, but it explains some crashes.

There's also a separate check for Windows compatibility shims (`aclayers.dll`, `acgenral.dll`, `acspecfc.dll`) that appear in the loaded module list when AppCompat is active.

### PE inspection

Five classes handle static PE analysis without running the file:

- **`PeImportReader`** - reads the Import Directory. Handles both PE32 and PE32+ layouts.
- **`PeHeaderReader`** - reads `Machine`, `Subsystem`, `DllCharacteristics`, `EntryPointRva`, the section table, and detects managed assemblies via the CLR runtime header directory.
- **`EntropyAnalyzer`** - computes Shannon entropy per section. Entropy above 7.5 usually means the section is packed or encrypted.
- **`StringExtractor`** - pulls printable ASCII strings out of the binary. It's strict on purpose: it rejects runs of repeated characters, requires a high letter-to-total ratio, and demands at least one word of three consecutive letters. This keeps the output focused.
- **`DllScanner`** - ties all of the above together. Given a file, it returns architecture, .NET status, direct imports, .NET assembly references, and whether each dependency exists on disk.

### Dependency analysis

Two passes while the target is running:

1. **Direct imports**: Every DLL in the target's import table should appear in the loaded-modules map. Anything that doesn't load is checked against the disk. If it exists on disk but didn't load, it's likely a delayed load or an optional dependency. If it doesn't exist on disk at all, it's flagged as a real missing dependency.
2. **Indirect dependencies**: Every DLL that loaded from the same folder as the target gets its own import table read, and missing entries there are also flagged. This catches cases where the target loads a game-specific DLL that itself needs a missing runtime.

The report distinguishes between confirmed missing DLLs, indirect ones, and ones that exist on disk but didn't load.

### Crash minidump

When a fatal crash is caught, before the process handle is closed, the tool writes a minidump using `MiniDumpWriteDump` with these flags:

The `.dmp` file can be opened in WinDbg or Visual Studio. It often ends up being tens or hundreds of megabytes. That's normal.

### Hang detection

A dedicated thread checks the target's top-level window once per second. It uses two checks:

- `IsHungAppWindow` (the fast path)
- `SendMessageTimeout` with `WM_NULL` and `SMTO_ABORTIFHUNG`, with a 700 ms timeout (the fallback)

If the window stops responding, a hang is recorded. If the hang lasts longer than 90 seconds, the watchdog forces the target to close so the report can still be generated.

### Watchdog

A separate thread watches the debug loop itself. It checks:

- Is the target still alive (`GetExitCodeProcess`)
- Has the debug loop received any events recently (`lastActivityTick`)

If no activity happens for 60 seconds, or if the target has exited without the loop noticing, the watchdog forces the loop to exit. This prevents the tool from hanging forever if a deadlock occurs.

### Reports

Two formats are produced, both written once at the end of the session using `File.WriteAllText`:

- **`.txt`** - flat text. Structured with `====` separators. Easy to grep, easy to paste into a forum.
- **`.md`** - Markdown. Same content, but with headings, tables, and code fences. Better for sharing on GitHub or Discord.

During the session, log lines are accumulated in memory (`allLogLines`). The report is written once, at the end, not incrementally.

Symbols: CrashTrace supports symbol resolution through `dbghelp.dll`, but it is **fully offline by design**. It never contacts Microsoft's symbol server. It only looks for PDB files that already exist locally (in `C:\Symbols`, `%LOCALAPPDATA%\TestAppSymbols`, `%PROGRAMDATA%\Microsoft\Windows\Debug\Symbols`, or any local path set in `_NT_SYMBOL_PATH`). If no local PDB is found, the report shows module+offset instead of function names. This is still useful - module+offset is stable across runs.

### Limitations

- It is not a profiler. It cannot help with stutter, low FPS, or frame-time spikes.
- It is not RenderDoc. It cannot capture frames or graphics API calls.
- It does not catch errors the target program handles internally and recovers from, unless those errors eventually cause a crash. First-chance exceptions are logged, but they are not analyzed as the cause of the crash on their own.
- Symbol resolution is offline-only. No Microsoft symbol server access, no internet connection at any point.
- It does not work with kernel-level anti-cheat or DRM.
- It only launches new processes. It does not attach to already-running ones.

---

## Summary

CrashTrace is a small user-mode debugger focused on one job: figure out why a Windows program stopped working. It captures the crash moment in detail, analyzes dependencies and PE structure, reads .NET exceptions, and produces a report that both a human and an AI can understand.

The tool is offline by design. It never sends data anywhere.

---

## License

MIT License. See `LICENSE` for details.
