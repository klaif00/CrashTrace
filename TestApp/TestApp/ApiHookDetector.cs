/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 *Developed by Limen
 * Detects whether well-known Windows APIs have been hooked (patched in
 * memory) by third-party software. Hooks on CreateFile, LoadLibrary,
 * VirtualAlloc, etc. are a common cause of "the program crashes only
 * on my machine" bugs. We check the first few bytes of each function
 * for the classic "JMP" prologue that hooks install.
 *
 * To avoid false positives we only look at a small, well-known set of
 * functions where the standard prologue is stable and documented.
 */
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace TestApp
{
	internal static class ApiHookDetector
	{
		// One detection result per function that was found to be hooked.
		public struct HookInfo
		{
			public string Module;
			public string Function;
			public bool Hooked;
			public string Reason;
		}

		// Only these modules are scanned. Keeping the list small avoids
		// false positives from modules with unusual (but legitimate) prologues.
		private static readonly string[] TargetModules = new string[]
		{
			"kernel32.dll", "kernelbase.dll", "ntdll.dll", "user32.dll",
			"advapi32.dll", "ws2_32.dll", "wininet.dll"
		};

		// Only these exports are checked. They are the ones most commonly
		// targeted by hooking frameworks.
		private static readonly string[] TargetFunctions = new string[]
		{
			"CreateFileW", "CreateFileA",
			"LoadLibraryW", "LoadLibraryA", "LoadLibraryExW",
			"GetProcAddress",
			"VirtualAlloc", "VirtualProtect",
			"RegOpenKeyExW", "RegQueryValueExW",
			"WSAStartup", "connect", "send", "recv",
			"InternetOpenW", "InternetConnectW"
		};

		// Local P/Invoke. We resolve the address locally, then read the
		// actual bytes from the target process to check the prologue.
		[DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
		private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

		// Walks every module/function pair, reads the first 8 bytes from
		// the target process and reports the ones that look hooked.
		public static List<HookInfo> DetectHooks(IntPtr targetProcess)
		{
			List<HookInfo> results = new List<HookInfo>();
			try
			{
				foreach (string modName in TargetModules)
				{
					IntPtr hMod = IntPtr.Zero;
					try
					{
						// Load the module locally just to resolve the function
						// addresses; we only read the local copy to keep this
						// safe. Note: if a hook was installed globally (in the
						// local process as well), we'd see it here too - but
						// we also cross-check against the target via ReadProcessMemory.
						hMod = NativeMethods.GetModuleHandle(modName);
						if (hMod == IntPtr.Zero) continue;

						foreach (string fn in TargetFunctions)
						{
							IntPtr addr = GetProcAddress(hMod, fn);
							if (addr == IntPtr.Zero) continue;

							byte[] buffer;
							bool hooked;

							// Try to read from the target process first
							int bytesRead = 0;
							buffer = MemoryInspector.ReadBytes(targetProcess, addr, 8, out bytesRead);
							hooked = IsHooked(buffer, bytesRead);

							// Build a result record. Only hooked entries are kept.
							HookInfo info = new HookInfo();
							info.Module = modName;
							info.Function = fn;
							info.Hooked = hooked;
							info.Reason = hooked ? DescribeHook(buffer, bytesRead) : null;

							if (hooked) results.Add(info);
						}
					}
					catch
					{
					}
				}
			}
			catch
			{
			}
			return results;
		}

		// Pattern matcher for the classic hook prologues.
		private static bool IsHooked(byte[] bytes, int length)
		{
			if (bytes == null || length < 5) return false;

			// The most common hook is a "jmp rel32" instruction: E9 xx xx xx xx
			if (bytes[0] == 0xE9) return true;

			// "jmp rel8": EB xx
			if (bytes[0] == 0xEB) return true;

			// "push imm32; ret": 68 xx xx xx xx C3
			if (bytes[0] == 0x68 && length >= 6 && bytes[5] == 0xC3) return true;

			// "mov edi, edi; push ebp; mov ebp, esp" (hotpatch prologue, NOT a hook)
			// 8B FF 55 8B EC - do not treat this as a hook
			// We deliberately do NOT check this pattern, so no false positives.

			return false;
		}

		// Turns the raw byte pattern into a short human-readable label.
		private static string DescribeHook(byte[] bytes, int length)
		{
			if (bytes == null || length < 2) return null;
			if (bytes[0] == 0xE9) return "JMP rel32 hook (E9)";
			if (bytes[0] == 0xEB) return "JMP rel8 hook (EB)";
			if (bytes[0] == 0x68) return "PUSH/RET hook (68 ... C3)";
			return "Unknown patch pattern";
		}
	}
}