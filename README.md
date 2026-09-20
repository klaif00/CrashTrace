# CrashTrace

A small user-mode debugger for Windows that helps you figure out why a program crashed, exited on its own, or hung.

---

## What it does

CrashTrace launches a target program under the Windows debugging API, watches every event from the first instruction, and when something goes wrong, produces a detailed report explaining what happened.

The report covers:

- The type of failure (crash, silent exit, abnormal exit, or hang)
- The exception code and meaning
- The faulting module, offset, CPU registers, and memory state at the crash site
- A mini-disassembly of the instructions around the fault
- A screenshot and a minidump of the moment of the crash
- A list of missing DLLs and runtime dependencies
- Loaded modules, active threads, Windows Event Log entries, and WER reports

It also includes two standalone static analysis tools: **DLL Scan** and **Can I Run It?** - both inspect a file without ever running it.

CrashTrace is **fully offline by design**. It never connects to the internet and never sends any data anywhere.

---

## Development environment

| Component | Version |
|-----------|---------|
| Language | C# |
| Framework | .NET Framework 4.5 |
| UI | Windows Forms |
| IDE | Visual Studio 2010+ / SharpDevelop 4.4+ |
| Target platform | Windows (x64 host) |

External libraries used:

- **SharpDisasm** - x86/x64 disassembler (C# port of udis86)
- **Microsoft.Diagnostics.Runtime (ClrMD)** - reads managed exceptions from .NET processes

The tool ships with two helper executables to handle .NET exception reading across architectures:

- `TestAppHelper32.exe` (x86) - for 32-bit managed targets
- `TestAppHelper64.exe` (x64) - for 64-bit managed targets

---

## Documentation

The repository includes two extended guides under the `docs/` folder:

- [English overview](README/README_EN.md)
- [نظرة عامة بالعربية](README/README_AR.md)

The English guide is split into a user-facing part and a developer-facing part. The Arabic guide mirrors it section by section.

---

## Credits

Special thanks to the **Microsoft.Diagnostics.Runtime** (ClrMD) team for the library that makes managed exception analysis possible. Without it, reading .NET exceptions from a 32-bit or 64-bit target would require building and maintaining a custom CLR metadata reader.

Thanks also to the SharpDisasm contributors for providing a clean C# disassembler that works offline.

---

## License

MIT License. See [LICENSE](LICENSE) for the full text.
