/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * 
 * Developed by Limen 
 * 
 * 
 * Turns the raw facts collected during a debugging session into a
 * short, focused list of concrete suggestions the user can actually
 * act on. The engine is deliberately prioritized: if we know exactly
 * what is wrong (a specific missing DLL, a specific crash pattern),
 * we only show advice for that. Generic advice is a last resort.
 */
using System;
using System.Collections.Generic;
using System.IO;

namespace TestApp
{
	internal static class RecommendationEngine
	{
		// Main entry point. Builds a prioritized list of suggestions
		// based on what the session actually detected.
		public static List<string> BuildRecommendations(CrashReportData d)
		{
			List<string> r = new List<string>();
			if (d == null) return r;

			// PRIORITY 1: If we know which DLL is missing, focus only on that.
			if (d.ConfirmedMissingDlls.Count > 0 || d.IndirectMissingDlls.Count > 0)
			{
				BuildDllNotFoundRecommendations(d, r);
				return r;
			}

			// PRIORITY 2: If it was a crash, build crash-specific advice.
			if (d.ResultLabel == "CRASH")
			{
				BuildCrashRecommendations(d, r);
				AppendSupplementaryAdvice(d, r);
				return r;
			}

			// PRIORITY 3: ABNORMAL EXIT without a clear missing DLL.
			if (d.ResultLabel == "ABNORMAL EXIT")
			{
				BuildAbnormalExitRecommendations(d, r);
				AppendSupplementaryAdvice(d, r);
				return r;
			}

			// PRIORITY 4: Stopped or clean exit.
			if (d.ResultLabel == "STOPPED")
			{
				r.Add("The program was stopped manually by you. No crash occurred during the session.");
				if (d.HangDetected)
				{
					r.Add("The window was unresponsive at some point before the stop. If the program is slow to respond, check for high CPU/disk usage from other programs.");
				}
			}
			else if (d.ResultLabel == "CLEAN EXIT")
			{
				r.Add("The program closed normally, with no crash or error. If you observed a problem, it likely happened in a different component or after the program had already exited.");
			}

			return r;
		}

		// Missing-DLL path: the highest-signal case, because we can name
		// exactly what is absent and suggest the right redistributable.
		private static void BuildDllNotFoundRecommendations(CrashReportData d, List<string> r)
		{
			if (d.ConfirmedMissingDlls.Count > 0)
			{
				r.Add("The program cannot start because the following required DLLs are missing from disk:");
				foreach (string m in d.ConfirmedMissingDlls)
					r.Add("    " + m);
				r.Add("Place these files next to the program's executable (usually the same folder as the .EXE), then run the test again. Make sure the DLL architecture matches the program (32-bit vs 64-bit).");
			}

			if (d.IndirectMissingDlls.Count > 0)
			{
				r.Add("Additionally, one of the program's own DLLs depends on these files, which are also missing:");
				foreach (string m in d.IndirectMissingDlls)
					r.Add("    " + m);
			}

			// Classify every missing DLL by family so we can recommend the
			// right redistributable instead of a generic "reinstall".
			bool mentionsSteam = false;
			bool mentionsSound = false;
			bool mentionsDirectX = false;
			bool mentionsVC = false;
			bool mentionsPhysX = false;
			bool mentionsOpenAL = false;
			bool mentionsOther = false;

			List<string> allMissing = new List<string>();
			allMissing.AddRange(d.ConfirmedMissingDlls);
			allMissing.AddRange(d.IndirectMissingDlls);

			foreach (string dll in allMissing)
			{
				string lower = dll.ToLowerInvariant();
				string baseName = Path.GetFileNameWithoutExtension(lower);

				if (baseName.StartsWith("steam_api") || baseName.StartsWith("steamclient") || baseName.StartsWith("tier0") || baseName.StartsWith("vstdlib"))
					mentionsSteam = true;
				else if (baseName.StartsWith("mss") || baseName.StartsWith("fmod") || baseName.StartsWith("openal"))
					mentionsSound = true;
				else if (baseName.StartsWith("d3dx") || baseName.StartsWith("xinput") || baseName.StartsWith("xaudio") || baseName.StartsWith("x3daudio") || baseName.StartsWith("d3dcompiler"))
					mentionsDirectX = true;
				else if (baseName.StartsWith("msvcr") || baseName.StartsWith("msvcp") || baseName.StartsWith("vcruntime") || baseName.StartsWith("concrt"))
					mentionsVC = true;
				else if (baseName.StartsWith("physx") || baseName.StartsWith("nxcooking") || baseName.StartsWith("nvtt"))
					mentionsPhysX = true;
				else if (baseName.StartsWith("openal"))
					mentionsOpenAL = true;
				else
					mentionsOther = true;
			}

			if (mentionsSteam)
			{
				r.Add("Steam API is missing. If this is a Steam game, launch it from inside the Steam client - the client provides steam_api.dll automatically. If it is a standalone or repacked version, you will need to place the correct steam_api.dll next to the EXE (usually included with the game's own files).");
			}

			if (mentionsSound)
			{
				r.Add("A required audio library is missing. These libraries (Miles Sound System, FMOD, OpenAL) are almost always shipped with the game itself. Reinstall the game from its original source (installer or game client). Copying the DLL from an unrelated source is unlikely to work.");
			}

			if (mentionsDirectX)
			{
				r.Add("A DirectX 9 component is missing. Install the DirectX End-User Runtime (June 2010) from Microsoft's website. This is a small, one-time install that most older games require, and it does not conflict with newer DirectX versions.");
			}

			if (mentionsVC)
			{
				r.Add("A Visual C++ runtime is missing. Install the correct version from Microsoft's website. Note: the 32-bit (x86) and 64-bit (x64) versions are separate packages - most older games need the x86 one even on 64-bit Windows.");
			}

			if (mentionsPhysX)
			{
				r.Add("NVIDIA PhysX runtime is missing. Install the NVIDIA PhysX System Software from NVIDIA's website.");
			}

			if (mentionsOther)
			{
				r.Add("Some of the missing DLLs are not part of any standard redistributable. They are most likely custom files shipped with the program itself, so the safest fix is to reinstall the program from its original source.");
			}

			// Two file-level warnings worth calling out in this context:
			// a checksum mismatch or a packed executable can explain why
			// dependencies fail to load.
			if (!string.IsNullOrEmpty(d.PeChecksumWarning))
			{
				r.Add("Important: the EXE's stored checksum does not match its content. This means the EXE may be corrupted or modified. Re-download or verify the file before troubleshooting further, because a corrupted EXE can fail in unpredictable ways.");
			}

			if (!string.IsNullOrEmpty(d.PackedHint))
			{
				r.Add("Note: this executable appears to be packed or protected (" + d.PackedHint + "). Packed executables sometimes fail to load dependencies correctly. If the file came from an unofficial source, this can also indicate tampering.");
			}
		}

		// Crash path: matches the reported crash against a small set of
		// recognizable patterns, from most specific to most generic.
		private static void BuildCrashRecommendations(CrashReportData d, List<string> r)
		{
			bool handled = false;

			if (d.Meaning != null && d.Meaning.IndexOf("Access Violation", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				if (d.LikelyNullPointer)
				{
					r.Add("The program crashed on a NULL pointer. It tried to use a pointer or object that was never created. This is a bug inside the program's own code, so no system change or reinstall will fix it.");
					r.Add("Look for a newer version or patch of the program. If the crash is reproducible from a specific action (loading a save, opening a menu, starting a level), report that action to the developer - this gives them the exact reproduction path they need.");
					r.Add("If the program supports mods or custom content, try running it with a clean/default profile. Third-party content is a very common trigger for this kind of bug.");
					handled = true;
				}
				else if (d.LikelyDepViolation)
				{
					r.Add("This is a DEP (Data Execution Prevention) violation. The program tried to execute memory that Windows has marked as non-executable.");
					r.Add("Common causes are: a security product (antivirus, anti-cheat) injecting code into the process, a packed or protected executable that decrypts itself at runtime, or a JIT compilation bug. Try adding the program to your antivirus exclusions, and make sure your AV is up to date.");
					handled = true;
				}
				else if (d.LikelyWriteToReadOnly)
				{
					r.Add("The program attempted to write to read-only memory. This usually means an anti-tamper or DRM component tried to patch code that Windows has protected. It is not something you can fix by changing settings - the developer needs to update the program for your Windows version.");
					handled = true;
				}
			}

			if (!handled && d.Meaning != null && d.Meaning.IndexOf("Stack Overflow", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				r.Add("A stack overflow occurred, meaning infinite or excessively deep recursion. This is usually triggered by a specific input - a corrupted save file, a mod, or a specific in-game action.");
				r.Add("Try: disable all mods, clear the game's cache/save folder, run with default settings, and see if the crash still happens.");
				handled = true;
			}

			if (!handled && d.Meaning != null && d.Meaning.IndexOf("Heap Corruption", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				r.Add("Heap corruption detected. A buffer overflow or use-after-free happened somewhere in the program. This is almost always triggered by a specific input or action.");
				r.Add("The most effective troubleshooting step is to remove any mods or third-party plugins you have installed. If that does not help, reinstall the program cleanly.");
				handled = true;
			}

			if (!handled && d.Meaning != null &&
				(d.Meaning.IndexOf("Device Removed", StringComparison.OrdinalIgnoreCase) >= 0 ||
				 d.Meaning.IndexOf("Device Hung", StringComparison.OrdinalIgnoreCase) >= 0))
			{
				r.Add("The graphics driver crashed or the GPU stopped responding. This is often a driver or hardware stability issue, not a bug in the game itself.");
				r.Add("Steps to try:");
				r.Add("    1. Update your GPU driver to the latest version from NVIDIA/AMD/Intel.");
				r.Add("    2. If already up to date, try a clean driver reinstall (using DDU).");
				r.Add("    3. Remove any GPU overclocking - both manual and factory 'OC' profiles.");
				r.Add("    4. Check GPU temperatures under load - overheating causes this exact error.");
				r.Add("    5. If the system has multiple GPUs, force the game to use the discrete one.");
				handled = true;
			}

			if (!handled && d.Meaning != null && d.Meaning.IndexOf("Assertion Failure", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				r.Add("The program hit an internal assertion, which means its own self-check failed. In debug builds this is a controlled stop, but in a release build it means the code detected an unexpected state and shut down deliberately.");
				r.Add("Look through the program's own log files (if any) - they often print the exact assertion message right before the crash.");
				handled = true;
			}

			// Fallback by faulting module when the meaning alone was not
			// specific enough.
			if (!handled && !string.IsNullOrEmpty(d.ModuleName))
			{
				string mod = Path.GetFileName(d.ModuleName).ToLowerInvariant();

				if (mod.StartsWith("d3d") || mod.StartsWith("dxgi") || mod.Contains("nv") || mod.Contains("amd") || mod.Contains("ig") || mod.Contains("opengl") || mod.Contains("vulkan"))
				{
					r.Add("The crash happened inside a graphics module (" + mod + "). The most likely causes are an outdated or unstable GPU driver, or an incompatibility between the game's rendering code and your driver version.");
					r.Add("Try updating your GPU driver to the latest version. If you are already on the latest, try rolling back to an older known-good driver version - newer drivers sometimes introduce regressions.");
					handled = true;
				}
				else if (mod == "ntdll.dll" || mod == "kernelbase.dll" || mod == "kernel32.dll")
				{
					r.Add("The crash was reported inside a core Windows module. This is usually misleading: the actual problem is almost always an invalid argument or a bad pointer passed in from the program itself. Windows did not cause the crash, it just reported it.");
					r.Add("Look at the call stack above. The first frame inside the program's own code (not a Windows DLL) is where the real fault occurred.");
					handled = true;
				}
				else if (mod == "dsound.dll" || mod.StartsWith("xaudio") || mod.Contains("audio") || mod == "winmm.dll")
				{
					r.Add("The crash happened inside an audio module (" + mod + "). Try: update your audio driver, disable audio enhancements in Windows Sound settings, or switch to a different audio output device.");
					handled = true;
				}
			}

			if (!handled)
			{
				r.Add("The crash does not match a well-known pattern, so the most useful next step is to examine the call stack, CPU registers, and mini-disassembly sections above. The faulting instruction and the values in EAX/ECX/EDX at the moment of the crash usually reveal what the program was trying to do.");
				r.Add("If this crash is reproducible from a specific action in the program, that action is the most valuable piece of information for the developer.");
			}
		}

		// Abnormal-exit path: the program called ExitProcess with a
		// non-zero code, or was terminated by the OS. Match the status
		// code to a specific piece of advice.
		private static void BuildAbnormalExitRecommendations(CrashReportData d, List<string> r)
		{
			if (string.IsNullOrEmpty(d.Meaning))
			{
				r.Add("The program exited with status code " + d.StatusCodeHex + " and did not produce a fatal exception. This usually means it called ExitProcess itself because it detected a problem.");
				return;
			}

			if (d.Meaning.IndexOf("DLL Not Found", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				r.Add("A required library could not be loaded, but the specific missing filename was not determined (the import table could not be read). Run the test again after reinstalling the program, or check the program's own folder for any log files that might name the missing dependency.");
			}
			else if (d.Meaning.IndexOf("Bad Image Format", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				r.Add("A DLL was found but is the wrong architecture (32-bit vs 64-bit) or is corrupted. Check that all DLLs in the program's folder match the architecture of the main EXE.");
			}
			else if (d.Meaning.IndexOf("Entry Point Not Found", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				r.Add("An incompatible version of a library is present. A DLL in the program's folder is older or newer than what the EXE expects. Reinstalling the program, or replacing the affected DLL with the correct version, should fix this.");
			}
			else if (d.Meaning.IndexOf("DLL Initialization Failed", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				r.Add("A required DLL failed to initialize. This is often caused by a dependency of that DLL being missing, or by an access permission problem. Try running the program as Administrator once to rule out permissions.");
			}
			else if (d.Meaning.IndexOf("Fail Fast", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				r.Add("The program deliberately terminated itself via a fail-fast call, usually after detecting internal corruption or a state it could not recover from. This is almost always a bug in the program, not something you can fix by changing settings.");
			}
			else
			{
				r.Add("The program exited with status code " + d.StatusCodeHex + " (" + d.Meaning + "). Look for the program's own log files - they often explain why it decided to terminate.");
			}
		}

		// Cross-cutting advice that applies on top of a specific crash or
		// exit diagnosis. Only added when the corresponding fact is
		// actually true, to avoid noise.
		private static void AppendSupplementaryAdvice(CrashReportData d, List<string> r)
		{
			// Only add these if they are clearly relevant and not already covered.

			if (d.SuspiciousModules != null && d.SuspiciousModules.Count > 0)
			{
				r.Add("Third-party overlays or hooks were loaded alongside the program. Close them completely before running it again - Discord overlay, MSI Afterburner, RivaTuner, Fraps, OBS Game Capture, and similar tools are known to conflict with some games.");
			}

			if (d.ApiHooksDetected != null && d.ApiHooksDetected.Count > 0)
			{
				r.Add("Windows APIs inside the process have been patched by another program (see the API hooks section above). This is often an antivirus or a system utility - it is not necessarily a problem, but a buggy hook can cause crashes. Try temporarily disabling third-party security or monitoring tools to see if the crash stops.");
			}

			if (d.HasCompatibilityShim)
			{
				r.Add("Windows has applied compatibility shims to this program, meaning Microsoft already knows it has issues on this Windows version. You may want to try changing the compatibility mode of the EXE manually (Properties -> Compatibility).");
			}

			if (!string.IsNullOrEmpty(d.CompatibilityFlags))
			{
				r.Add("You have manually configured compatibility settings for this program (" + d.CompatibilityFlags + "). If problems persist, try removing them and running the program with default settings.");
			}

			// (No-op placeholder preserved: the two conditions above
			// already cover the case; kept to match the original code.)
			if (d.HasCompatibilityShim && !string.IsNullOrEmpty(d.CompatibilityFlags))
			{
				// (already covered by both branches above, no duplicate)
			}

			if (d.InDownloadsFolder)
			{
				r.Add("The program is located in the Downloads folder. This is a common source of problems because some antivirus tools partially quarantine or block executables there. Move the program to a dedicated folder (for example, C:\\Games\\...) and try again.");
			}

			if (d.AvailableSystemMemoryMb >= 0 && d.AvailableSystemMemoryMb < 500)
			{
				r.Add("System free memory was critically low at the moment of the crash (" + d.AvailableSystemMemoryMb + " MB). Close other applications and try again.");
			}

			if (d.FreeDiskSpaceMb >= 0 && d.FreeDiskSpaceMb < 1024)
			{
				r.Add("Free disk space on the target drive is very low (" + (d.FreeDiskSpaceMb / 1024) + " GB). Some programs need room for temporary files and can fail when the drive is nearly full.");
			}

			if (d.HangDetected)
			{
				r.Add("The program became unresponsive at least once during monitoring. If it eventually recovered, the cause is likely a slow disk, a blocked network call, or a background operation. If it never recovered, this may indicate an infinite loop or a deadlock inside the program.");
			}

			if (d.SteamApiLoaded && !d.SteamClientRunning)
			{
				r.Add("The program loaded the Steam API, but the Steam client does not appear to be running. Start Steam first, and launch the program from inside the Steam library - not directly from the EXE.");
			}
		}
	}
}