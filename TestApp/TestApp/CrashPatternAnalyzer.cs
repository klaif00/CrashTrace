/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen
 * Analyzes a crash report using general-purpose heuristics and pattern
 * recognition. Nothing here is tied to a specific program name - the
 * analysis is based on observable patterns in the crash itself:
 * timing, module of fault, access type, address range, modules loaded
 * immediately before, and the interplay with compatibility shims and
 * packers. This makes the tool useful for ANY program, not just a
 * handful of hardcoded examples.
 */
using System;
using System.Collections.Generic;

namespace TestApp
{
	internal static class CrashPatternAnalyzer
	{
		// Common Windows modules and the kind of crash they usually
		// indicate when they appear as the faulting module. This is not
		// game-specific - these modules are used by virtually every
		// Windows program.
		private static readonly string[] GraphicsModules = new string[]
		{
			"d3d9", "d3d10", "d3d11", "d3d12", "dxgi", "d2d1", "dwrite",
			"opengl32", "vulkan", "nvoglv", "ig", "aticfx", "amdxc",
			"nvwgf2um", "igdumdim"
		};

		private static readonly string[] AudioModules = new string[]
		{
			"dsound", "xaudio", "mss32", "mss64", "fmod", "openal", "xa2",
			"winmm", "audioeng", "wdmaud"
		};

		private static readonly string[] NetworkModules = new string[]
		{
			"ws2_32", "wininet", "winhttp", "wsock32", "mswsock", "iphlpapi"
		};

		private static readonly string[] SecurityModules = new string[]
		{
			"apphelp", "aclayers", "acgenral", "acspecfc", "ntdll", "kernelbase"
		};

		// Entry point. Runs every analysis pass and returns the combined
		// list of human-readable hints, in the order they were produced.
		public static List<string> Analyze(CrashReportData d)
		{
			List<string> hints = new List<string>();
			if (d == null) return hints;

			AnalyzeTiming(d, hints);
			AnalyzeFaultingModule(d, hints);
			AnalyzeAccessPattern(d, hints);
			AnalyzePrecedingModules(d, hints);
			AnalyzeExceptionInterplay(d, hints);
			AnalyzeDelayLoadedImports(d, hints);
			AnalyzeArchitectureSignals(d, hints);
			AnalyzeStackShape(d, hints);
			AnalyzeResourcePressure(d, hints);

			return hints;
		}

		// ------------------------------------------------------------------
		// Timing patterns
		// ------------------------------------------------------------------
		private static void AnalyzeTiming(CrashReportData d, List<string> hints)
		{
			if (d.ResultLabel != "CRASH" && d.ResultLabel != "ABNORMAL EXIT") return;
			if (d.SecondsUntilCrash < 0) return;

			// Very early crashes almost always mean initialization failed.
			if (d.SecondsUntilCrash < 3)
			{
				hints.Add("The crash happened within the first few seconds. Crashes this early " +
					"almost always point to an initialization failure: a required component " +
					"could not be set up, a configuration or data file was missing, or the " +
					"program detected hardware/software it could not work with.");

				if (d.HasCompatibilityShim)
				{
					hints.Add("Windows applied compatibility shims AND the crash was immediate. " +
						"This combination often means the shims themselves are interfering. " +
						"Try disabling all compatibility settings for this program and re-testing.");
				}
			}
			// Early startup window: graphics/audio/asset init.
			else if (d.SecondsUntilCrash < 15)
			{
				hints.Add("The crash occurred during early startup. This is a typical window " +
					"for failures in graphics initialization, audio device setup, or asset loading.");
			}
			// Long sessions point at leaks rather than one-shot bugs.
			else if (d.SecondsUntilCrash > 1800)
			{
				hints.Add("The crash happened after a long running session (over 30 minutes). " +
					"Long-running crashes are often caused by resource leaks (memory, handles, " +
					"GDI objects) rather than a one-shot bug, so look at the resource counters.");
			}
		}

		// ------------------------------------------------------------------
		// Faulting module patterns
		// ------------------------------------------------------------------
		private static void AnalyzeFaultingModule(CrashReportData d, List<string> hints)
		{
			if (string.IsNullOrEmpty(d.ModuleName)) return;
			string mod = System.IO.Path.GetFileName(d.ModuleName).ToLowerInvariant();

			if (MatchesAny(mod, GraphicsModules))
			{
				hints.Add("The crash occurred inside a graphics module (" + mod + "). This is " +
					"usually caused by an outdated, buggy, or unstable GPU driver, GPU overheating, " +
					"or an incompatible DirectX runtime. Update your graphics driver to the latest " +
					"version, and if the problem persists, try a clean driver reinstall.");
			}

			if (MatchesAny(mod, AudioModules))
			{
				hints.Add("The crash occurred inside an audio module (" + mod + "). Try disabling " +
					"audio enhancements in Windows Sound settings, updating the audio driver, or " +
					"switching the output device to a different one. Some audio DLLs also require " +
					"the legacy DirectX End-User Runtime.");
			}

			if (MatchesAny(mod, NetworkModules))
			{
				hints.Add("The crash occurred inside a network module (" + mod + "). This often " +
					"indicates the program could not reach a server, or a network driver is " +
					"misbehaving. Check your firewall and antivirus for blocked connections.");
			}

			// Faults recorded inside core Windows modules are usually
			// misleading - the real bug is in the caller.
			if (mod == "ntdll.dll" || mod == "kernelbase.dll" || mod == "kernel32.dll")
			{
				hints.Add("The fault was recorded inside a core Windows module. This is usually " +
					"misleading - the actual bad pointer or bad argument was almost certainly " +
					"passed in from the program itself, and the crash only became visible once " +
					"Windows tried to service the invalid request. Look at the call stack below " +
					"the top frame for the real culprit.");
			}

			// Compatibility shim modules are part of Windows' AppCompat layer.
			if (mod == "apphelp.dll" || mod == "aclayers.dll" || mod == "acgenral.dll" ||
				mod == "acspecfc.dll")
			{
				hints.Add("The crash involves Windows compatibility shim modules. Windows is " +
					"trying to apply compatibility fixes to this program, and that mechanism " +
					"itself has become part of the problem. Disabling compatibility settings " +
					"in the file's Properties dialog is worth trying.");
			}
		}

		// ------------------------------------------------------------------
		// Access violation pattern
		// ------------------------------------------------------------------
		private static void AnalyzeAccessPattern(CrashReportData d, List<string> hints)
		{
			if (!d.HaveAccessInfo) return;

			bool isWrite = d.AccessKind == "write";
			bool isExecute = d.AccessKind != null && d.AccessKind.StartsWith("execute");

			if (d.LikelyNullPointer)
			{
				if (isWrite)
				{
					hints.Add("The program tried to WRITE through a pointer that was never set. " +
						"This almost always means a subsystem that should have produced an object " +
						"(a renderer, an audio device, a network session) failed silently and " +
						"returned null, and the code did not check for that before using it.");
				}
				else
				{
					hints.Add("The program tried to READ from a pointer that was never set. This is " +
						"the classic 'null pointer dereference' bug. It usually means an object was " +
						"expected to have been created earlier in the program's startup sequence, " +
						"but that earlier step did not complete.");
				}
			}
			// DEP-style violation: execution attempted on non-executable memory.
			else if (isExecute)
			{
				hints.Add("The program tried to EXECUTE memory that is not marked as executable. " +
					"This is a DEP (Data Execution Prevention) violation. Common causes: " +
					"code that has been encrypted/packed and failed to decrypt correctly, a " +
					"security product injecting into the process, or a corrupted executable.");
			}
			// Non-null write faults: overflows, use-after-free, read-only regions.
			else if (isWrite && !d.LikelyNullPointer)
			{
				hints.Add("The program tried to WRITE to memory it does not own. This is typical " +
					"of a buffer overflow, a use-after-free, or writing to a constant/read-only " +
					"region (often attempted by DRM or anti-tamper code).");
			}
		}

		// ------------------------------------------------------------------
		// What was loaded right before the crash
		// ------------------------------------------------------------------
		private static void AnalyzePrecedingModules(CrashReportData d, List<string> hints)
		{
			if (d.AllLogLines == null || d.AllLogLines.Count == 0) return;

			// Find the last few DLL loads before the crash
			List<string> lastLoaded = new List<string>();
			for (int i = d.AllLogLines.Count - 1; i >= 0 && lastLoaded.Count < 3; i--)
			{
				string line = d.AllLogLines[i];
				if (line.IndexOf("[!!! FATAL CRASH !!!]", StringComparison.OrdinalIgnoreCase) >= 0) continue;
				if (line.IndexOf("[DLL] Loaded:", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					int idx = line.IndexOf("[DLL] Loaded:", StringComparison.OrdinalIgnoreCase) + 13;
					if (idx > 0 && idx < line.Length)
					{
						lastLoaded.Add(line.Substring(idx).Trim());
					}
				}
			}

			if (lastLoaded.Count > 0)
			{
				hints.Add("The last module(s) loaded before the crash were: " +
					string.Join(", ", lastLoaded.ToArray()) + ". If the crash is deterministic, " +
					"this often means the fault occurred while that module was being initialized - " +
					"a missing dependency of it, or an incompatibility with the rest of the program.");
			}
		}

		// ------------------------------------------------------------------
		// Sequence of exceptions before the fatal one
		// ------------------------------------------------------------------
		private static void AnalyzeExceptionInterplay(CrashReportData d, List<string> hints)
		{
			if (d.ResultLabel != "CRASH") return;

			// Detect if the program was already throwing non-fatal exceptions right
			// before it crashed. This pattern means the code had already gotten into
			// a bad state but was trying to recover; the crash is a consequence of
			// that earlier failure, not a fresh problem.
			// Certain exception codes are filtered out (breakpoint, etc.) so they
			// do not inflate the count.
			int nonFatalCount = 0;
			if (d.AllLogLines != null)
			{
				foreach (string line in d.AllLogLines)
				{
					if (line.IndexOf("[Exception - Non-fatal]", StringComparison.OrdinalIgnoreCase) >= 0 &&
						line.IndexOf("0x80000003", StringComparison.OrdinalIgnoreCase) < 0 &&
						line.IndexOf("0x4000001F", StringComparison.OrdinalIgnoreCase) < 0 &&
						line.IndexOf("0x80000004", StringComparison.OrdinalIgnoreCase) < 0)
					{
						nonFatalCount++;
					}
				}
			}

			if (nonFatalCount >= 3)
			{
				hints.Add("The program threw several non-fatal exceptions before the fatal one. " +
					"This pattern - repeated recoverable errors followed by a final crash - is " +
					"very common when the program is missing a resource (a file, a data table, " +
					"a registry key) and keeps trying to fall back, until eventually a code path " +
					"that has no fallback is hit.");
			}
		}

		// ------------------------------------------------------------------
		// Delay-loaded import analysis
		// ------------------------------------------------------------------
		private static void AnalyzeDelayLoadedImports(CrashReportData d, List<string> hints)
		{
			if (d.DelayLoadedDlls == null || d.DelayLoadedDlls.Count == 0) return;
			if (d.ResultLabel != "CRASH") return;

			// A delay-loaded DLL that is missing will not cause a "DLL Not Found"
			// error at startup - instead, the program will crash the first time
			// it tries to actually CALL a function inside it.
			hints.Add("The executable uses delay-loaded imports: " +
				string.Join(", ", d.DelayLoadedDlls.ToArray()) + ". These DLLs do not load at " +
				"startup - they load lazily on first use. If one of them is missing or " +
				"corrupted, the crash happens the moment the code first calls into it, not " +
				"when the program starts. This is a common cause of crashes that only occur " +
				"after a specific user action.");
		}

		// ------------------------------------------------------------------
		// Architecture-related signals
		// ------------------------------------------------------------------
		private static void AnalyzeArchitectureSignals(CrashReportData d, List<string> hints)
		{
			if (string.IsNullOrEmpty(d.Architecture)) return;

			// 32-bit program on 64-bit Windows is the normal case for old games -
			// but it's worth calling out when the address pattern is unusual for it.
			if (d.Architecture.StartsWith("x86") && d.LikelyNullPointer)
			{
				// Nothing extra needed; the null pointer hint already covers it.
			}
		}

		// ------------------------------------------------------------------
		// Stack shape analysis
		// ------------------------------------------------------------------
		private static void AnalyzeStackShape(CrashReportData d, List<string> hints)
		{
			if (d.CallStack == null || d.CallStack.Count == 0) return;

			// Deep stacks are a strong indicator of runaway recursion.
			if (d.CallStack.Count >= 30)
			{
				hints.Add("The captured call stack is unusually deep (" + d.CallStack.Count +
					" frames). When combined with the crash details, this often indicates " +
					"either infinite recursion or an event-handler loop, where the program " +
					"keeps re-entering the same code path.");
			}

			// Detect if the crash location is the same as an earlier frame - sign of
			// recursive self-call with no base case.
			if (d.CallStack.Count >= 4)
			{
				string first = StripOffset(d.CallStack[0]);
				string last = StripOffset(d.CallStack[d.CallStack.Count - 1]);
				if (!string.IsNullOrEmpty(first) && first == last && d.CallStack.Count >= 6)
				{
					hints.Add("The top and bottom of the call stack are inside the same module " +
						"with no clear transition - this is a common signature of runaway " +
						"recursion. If you have a mod or addon installed, try removing it.");
				}
			}
		}

		// Drops the "+0x1234" offset from a frame string, if present.
		private static string StripOffset(string frame)
		{
			if (string.IsNullOrEmpty(frame)) return null;
			int plus = frame.IndexOf('+');
			if (plus > 0) return frame.Substring(0, plus);
			return frame;
		}

		// ------------------------------------------------------------------
		// Resource pressure
		// ------------------------------------------------------------------
		private static void AnalyzeResourcePressure(CrashReportData d, List<string> hints)
		{
			if (d.ResultLabel != "CRASH") return;

			bool memoryLow = d.AvailableSystemMemoryMb >= 0 && d.AvailableSystemMemoryMb < 300;
			bool handlesHigh = d.CrashHandleCount > 9000;
			bool gdiHigh = d.CrashGdiObjects > 9000;
			bool userHigh = d.CrashUserObjects > 9000;

			// Only warn when both memory is low AND at least one object
			// counter is near the Windows per-process limit.
			if (memoryLow && (handlesHigh || gdiHigh || userHigh))
			{
				hints.Add("The system was under significant resource pressure at the moment " +
					"of the crash (low free memory, and high kernel/GDI/USER object usage). " +
					"The crash may be a symptom of resource exhaustion rather than a specific " +
					"bug in the program. Close other applications and try again.");
			}
		}

		// ------------------------------------------------------------------
		// Helper
		// ------------------------------------------------------------------
		// Case-insensitive substring match against a list of tokens.
		private static bool MatchesAny(string name, string[] list)
		{
			if (string.IsNullOrEmpty(name)) return false;
			foreach (string s in list)
			{
				if (name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) return true;
			}
			return false;
		}
	}
}