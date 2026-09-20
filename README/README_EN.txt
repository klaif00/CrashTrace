==================================================
 Crash Diagnostic Tool - Overview
==================================================

This file has two parts:
- Part 1: For the general user (non-technical)
- Part 2: For developers / technical readers


==================================================
 Part 1: For the General User
==================================================


--- 1) What does this tool actually do? ---

If you have a game or program that opens and closes
by itself, or says "missing library", or crashes
before showing its window - this tool acts like a
security camera. It launches the program, watches
everything that happens inside it, and if it
crashes, produces a full report explaining what
happened.


--- 2) What does "launch it under supervision" mean? ---

The program is not started by you. The tool starts
it, and stays right next to it from the very first
moment. While the program runs, the tool sees every
move it makes:
- Every system file it loads
- Every thread it creates
- Every error that occurs (even ones the program
  handled itself)
- If it closes suddenly, it knows why


--- 3) What does the report contain? ---

When the program crashes, the tool produces a
report that tells you:

- Type of problem: crash? self-exit? hang?
- If it crashed: why (invalid memory access,
  missing file, etc.)
- Where exactly (which file inside the program)
- If a file was missing: its name and the fix
- Memory snapshot at the crash moment
- Screenshot at the crash moment

Everything is saved as:
- A plain text file (.txt)
- A formatted markdown file (.md)


--- 4) Does it work with games? ---

Important limitations:
- If the game uses anti-cheat (Valorant, BattlEye,
  etc.), the tool will not work on it. In some
  cases the game may refuse to launch or your
  account may be banned. The tool warns you about
  this before starting.

- If the game is old (32-bit), the tool works fine.

- If the game is modern (64-bit), the tool works
  fine too.


--- 5) Are there other modes besides launching? ---

Yes, there are 3 buttons in the tool:

- "DLL Scan" button: you pick an EXE file, and the
  tool reads all libraries it requires and tells
  you which are missing.

- "Can I Run It?" button: the tool inspects the
  file and tells you what Windows version, which
  DirectX, and which runtimes it needs, and warns
  you if your system cannot run it.

- Main button: launches the program under
  supervision.

All of this works **offline**. The tool never
connects to any server or sends any data about
you.


--- 6) What it does NOT do ---

Important to be aware of:
- It cannot diagnose stutter or low FPS
- It cannot diagnose audio cut-outs or black screens
- It cannot see errors the program handles itself
  and continues running from
- It cannot diagnose network issues (disconnect,
  lag)
- It does not fix anything, it only diagnoses


--- 7) How to use it best ---

1. Run it as Administrator
2. Select the program to test
3. Click Start
4. Wait for the problem to happen
5. If the program closes, the report appears next
   to it


--- 8) After the report is generated ---

The report contains a lot of technical info. The
easiest way to use it: paste it into an AI (like
ChatGPT) and ask:

  "Read this report and tell me what the
   problem is and how to fix it"

The AI can understand the report without you
being a programmer. The tool puts all information
the AI needs into the report.


==================================================
 Part 2: For Developers
==================================================


--- 1) Architectural overview ---

A Windows Forms app (C#, .NET Framework 4.5) that
acts as a user-mode debugger using the official
Windows debugging API:

  CreateProcess + DEBUG_ONLY_THIS_PROCESS
  WaitForDebugEvent + ContinueDebugEvent

It is a full debugger, not just a monitor. It
receives every event Windows sends to a debugger
(DLL load/unload, thread create/exit, exceptions,
output debug string).


--- 2) What it reads from memory ---

- Registers (EAX..EDI + ESP/EBP/EIP for 32-bit,
  RAX..R15 + RSP/RBP/RIP for 64-bit)

- Stack dump (4 KB around ESP/RSP)

- Memory region info (VirtualQueryEx + page state,
  protection, type)

- Instruction bytes at crash address (for
  disassembly)

- PEB (for 32-bit targets, to read environment
  variables)

- IAT (not currently, but ApiHookDetector attempts)


--- 3) Disassembly ---

Two layers:
- BasicDisassembler: hand-written limited opcode
  decoder, built-in
- DisassemblyHelper + SharpDisasm: full-featured
  disassembler via SharpDisasm (udis86 port for
  C#)

The helper reads 40 bytes before the crash and 48
after, tries to find an offset from which
instructions decode cleanly to land exactly on
the crash address, then decodes 8 instructions
before + the crash + 8 after.


--- 4) .NET Exception analysis ---

To read .NET exception details (type + message +
stack), the tool uses ClrMD
(Microsoft.Diagnostics.Runtime).

Architectural constraint: ClrMD uses a native DLL
called DAC which must match the target process
architecture exactly. So if the host is 64-bit
and the target is 32-bit, ClrMD fails.

Solution: helper process. We ship:
- TestAppHelper32.exe (x86) for 32-bit targets
- TestAppHelper64.exe (x64) for 64-bit targets

The main tool (x64) when it sees a 32-bit target
launches TestAppHelper32.exe passing PID, TID and
a temp file path. The helper reads the exception
and writes the result into a text file; the main
tool reads it back and populates the fields.


--- 5) Module classification ---

- SuspiciousModules: overlays (Discord, MSI AB,
  RivaTuner, GameBar, etc.)

- AntiCheatModules: EasyAntiCheat, BattlEye,
  Vanguard, XignCode, GameGuard, etc.

- Compatibility Shims detection: looks for
  aclayers.dll / acgenral.dll / acspecfc.dll in
  the loaded module list.


--- 6) PE inspection ---

- PeImportReader: reads the Import Directory
  (supports both PE32 and PE32+)

- PeHeaderReader: reads Machine, Subsystem,
  DllCharacteristics, EntryPointRva, Sections

- EntropyAnalyzer: computes Shannon entropy per
  section (high value = compressed/encrypted)

- StringExtractor: extracts ASCII strings
  (filtered to avoid noise)

- DllScanner: complete static analysis using the
  above


--- 7) Dependency analysis ---

Two passes:
1. PE imports: any DLL in the import table must
   load. The tool compares this list against
   actually-loaded modules. Anything not loaded is
   considered missing.

2. Indirect dependencies: reads imports of every
   DLL loaded from the same folder as the target
   to find deeper missing dependencies.


--- 8) Crash Dump ---

Uses MiniDumpWriteDump with selected flags:
- MiniDumpWithDataSegs
- MiniDumpWithHandleData
- MiniDumpWithUnloadedModules
- MiniDumpWithProcessThreadData
- MiniDumpWithThreadInfo
- MiniDumpWithIndirectlyReferencedMemory

Saved next to the report as .dmp; openable in
WinDbg or Visual Studio.


--- 9) Hang detection ---

A dedicated thread pings the target's top-level
window every second via SendMessageTimeout with
SMTO_ABORTIFHUNG and IsHungAppWindow. If the
window is unresponsive for over a second, a Hang
is recorded. The watchdog in DebugLoop forces the
target to close after 90 seconds of Hang so the
report can still be produced.


--- 10) Watchdog ---

A watchdog thread checks:
- Is the target alive (GetExitCodeProcess)
- Is the debug loop receiving events
  (lastActivityTick)
- If there is no activity for over 60 seconds, it
  forces the loop to exit

This prevents the tool itself from hanging
forever if a deadlock occurs.


--- 11) Reports ---

Three formats:
- .txt: flat text report, built via StringBuilder
  and File.AppendAllText, written live during
  runtime + final analysis at the end

- .md: Markdown, same content, formatted

- .json: full data structure, useful for
  programmatic analysis


--- 12) Explicit limitations ---

- Does not solve performance issues (not a
  profiler)
- Does not solve graphics issues (not RenderDoc)
- Does not catch errors handled internally by the
  target program unless visible as an exception
- Symbols are not supported (offline only, no
  Microsoft symbol server)
- Does not work with kernel-level anti-cheat
- Does not attach to running processes (launch
  only)


==================================================
 Summary
==================================================

This tool is a compact debugger that does:
- Launches the target under supervision
- Captures all events
- Disassembles and dissects the crash moment
- Analyzes PE + dependencies
- Reads .NET exceptions via helper processes

The result is a detailed report, and an AI can
read it and explain it to the general user.

==================================================
 End of file
==================================================