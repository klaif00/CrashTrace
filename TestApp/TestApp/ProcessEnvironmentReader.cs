/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen 
 * 
 * 
 * 
 * Reads the environment block of the target process. This requires
 * walking the PEB (Process Environment Block) and RTL_USER_PROCESS_
 * PARAMETERS, both of which live at known offsets in the target's
 * address space. For WOW64 processes (32-bit on 64-bit Windows), we
 * query the 32-bit PEB using NtQueryInformationProcess with the
 * ProcessWow64Information class.
 */
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace TestApp
{
	internal static class ProcessEnvironmentReader
	{
		// Public entry point. Picks the right path depending on whether
		// the target is running under WOW64 or is native.
		public static List<string> ReadEnvironment(IntPtr hProcess, bool is32BitOn64)
		{
			List<string> result = new List<string>();
			try
			{
				if (is32BitOn64)
					ReadWow64Environment(hProcess, result);
				else
					ReadNativeEnvironment(hProcess, result);
			}
			catch
			{
			}
			return result;
		}

		// WOW64 path: get the 32-bit PEB via NtQueryInformationProcess,
		// then follow ProcessParameters -> Environment.
		private static void ReadWow64Environment(IntPtr hProcess, List<string> result)
		{
			try
			{
				// Ask the OS for the 32-bit PEB address.
				IntPtr peb32 = IntPtr.Zero;
				uint returned;
				int status = NativeMethods.NtQueryInformationProcess(
					hProcess, NativeMethods.ProcessWow64Information,
					ref peb32, (uint)IntPtr.Size, out returned);
				if (status != 0 || peb32 == IntPtr.Zero) return;

				// 32-bit PEB layout: ProcessParameters at offset 0x10
				IntPtr processParameters32;
				if (!ReadPointer32(hProcess, new IntPtr(peb32.ToInt64() + 0x10), out processParameters32)) return;

				// 32-bit RTL_USER_PROCESS_PARAMETERS: Environment at offset 0x48
				IntPtr environmentPtr;
				if (!ReadPointer32(hProcess, new IntPtr(processParameters32.ToInt64() + 0x48), out environmentPtr)) return;

				ReadUnicodeStringAt(hProcess, environmentPtr, 65536, result);
			}
			catch
			{
			}
		}

		// Native 64-bit path is not implemented. The 64-bit PEB is
		// reachable through the TEB, which needs a different query path.
		private static void ReadNativeEnvironment(IntPtr hProcess, List<string> result)
		{
			result.Add("(environment reading is currently supported only for 32-bit targets on 64-bit Windows)");
		}

		// Reads a 4-byte pointer from the target (32-bit PEB is always
		// 32-bit pointers, even when read by a 64-bit debugger).
		private static bool ReadPointer32(IntPtr hProcess, IntPtr address, out IntPtr value)
		{
			value = IntPtr.Zero;
			try
			{
				int read;
				byte[] buf = MemoryInspector.ReadBytes(hProcess, address, 4, out read);
				if (buf == null || read < 4) return false;
				uint v = BitConverter.ToUInt32(buf, 0);
				value = new IntPtr((long)v);
				return v != 0;
			}
			catch
			{
				return false;
			}
		}

		// Reads the double-null-terminated Unicode environment block and
		// splits it into individual "NAME=VALUE" lines. The 4096-char
		// cap per line and the 65536-char total cap protect against
		// malformed memory.
		private static void ReadUnicodeStringAt(IntPtr hProcess, IntPtr address, int maxChars, List<string> result)
		{
			try
			{
				int read;
				byte[] buf = MemoryInspector.ReadBytes(hProcess, address, maxChars * 2, out read);
				if (buf == null || read < 4) return;

				StringBuilder sb = new StringBuilder();
				for (int i = 0; i + 1 < read; i += 2)
				{
					char c = (char)(buf[i] | (buf[i + 1] << 8));
					if (c == 0)
					{
						if (sb.Length > 0)
						{
							result.Add(sb.ToString());
							sb.Length = 0;
						}
						// Two nulls in a row = end of environment block
						if (i + 3 < read && buf[i + 2] == 0 && buf[i + 3] == 0) break;
					}
					else
					{
						sb.Append(c);
						if (sb.Length > 4096) sb.Length = 0; // sanity
					}
				}
			}
			catch
			{
			}
		}
	}
}