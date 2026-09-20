/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen
 * 
 * 
 * Reads and analyzes the process's memory at the exact moment of a crash.
 * Looks at the faulting instruction bytes, the memory region containing
 * the crash address, and general memory layout patterns. This helps
 * distinguish between corrupted code, intact code that dereferenced a
 * bad pointer, and code pages that were paged out or unmapped.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TestApp
{
	internal static class MemoryInspector
	{
		// Summary of a single virtual memory region.
		public struct RegionInfo
		{
			public bool Success;
			public IntPtr BaseAddress;
			public IntPtr AllocationBase;
			public long RegionSize;
			public string State;
			public string Protect;
			public string Type;
			public string Description;
		}

		// Wraps VirtualQueryEx to describe the region that contains the
		// given address.
		public static RegionInfo DescribeRegion(IntPtr hProcess, IntPtr address)
		{
			RegionInfo info = new RegionInfo();
			info.Success = false;
			try
			{
				NativeMethods.MEMORY_BASIC_INFORMATION mbi;
				IntPtr result = NativeMethods.VirtualQueryEx(
					hProcess, address, out mbi,
					(uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.MEMORY_BASIC_INFORMATION)));
				if (result == IntPtr.Zero) return info;

				info.Success = true;
				info.BaseAddress = mbi.BaseAddress;
				info.AllocationBase = mbi.AllocationBase;
				info.RegionSize = mbi.RegionSize.ToInt64();
				info.State = StateToString(mbi.State);
				info.Protect = ProtectToString(mbi.Protect);
				info.Type = TypeToString(mbi.Type);
				info.Description = DescribeCombination(mbi.State, mbi.Protect, mbi.Type);
			}
			catch
			{
			}
			return info;
		}

		// Wraps ReadProcessMemory. Returns null on failure; bytesRead
		// carries the count of bytes actually read.
		public static byte[] ReadBytes(IntPtr hProcess, IntPtr address, int count, out int bytesRead)
		{
			bytesRead = 0;
			try
			{
				byte[] buffer = new byte[count];
				int read;
				if (NativeMethods.ReadProcessMemory(hProcess, address, buffer, count, out read))
				{
					bytesRead = read;
					return buffer;
				}
			}
			catch
			{
			}
			return null;
		}

		// Classic hex dump: 16 bytes per line, address on the left,
		// ASCII view on the right. Non-printable bytes render as '.'.
		public static string FormatHexDump(byte[] bytes, IntPtr baseAddress, int maxBytes)
		{
			if (bytes == null || bytes.Length == 0) return "(no data available)";
			StringBuilder sb = new StringBuilder();
			int n = Math.Min(bytes.Length, maxBytes);
			for (int i = 0; i < n; i += 16)
			{
				sb.Append((baseAddress.ToInt64() + i).ToString("X16")).Append("  ");
				int rowLen = Math.Min(16, n - i);
				for (int j = 0; j < rowLen; j++)
				{
					sb.Append(bytes[i + j].ToString("X2")).Append(' ');
					if (j == 7) sb.Append(' ');
				}
				// Pad the hex column so the ASCII column lines up.
				for (int j = rowLen; j < 16; j++)
				{
					sb.Append("   ");
					if (j == 7) sb.Append(' ');
				}
				sb.Append(" |");
				for (int j = 0; j < rowLen; j++)
				{
					byte b = bytes[i + j];
					if (b >= 0x20 && b < 0x7F) sb.Append((char)b);
					else sb.Append('.');
				}
				sb.Append('|');
				sb.AppendLine();
			}
			return sb.ToString();
		}

		// Recognizes a few well-known byte patterns in the crash region.
		// Each pattern has a specific meaning when seen at a crash site.
		public static string DetectPattern(byte[] bytes, int count)
		{
			if (bytes == null || count == 0) return null;
			int n = Math.Min(bytes.Length, count);

			bool allZero = true;
			bool allInt3 = true;
			bool allNop = true;
			bool allFF = true;
			int nopCount = 0;

			for (int i = 0; i < n; i++)
			{
				byte b = bytes[i];
				if (b != 0x00) allZero = false;
				if (b != 0xCC) allInt3 = false;
				if (b != 0x90) allNop = false;
				if (b != 0xFF) allFF = false;
				if (b == 0x90) nopCount++;
			}

			if (allZero) return "All zero bytes - the memory was never written to, or was cleared. In the context of an access violation, this typically means the code expected data here that was never produced.";
			if (allInt3) return "All 0xCC bytes (INT3 instructions) - this is the standard pattern for uninitialized or deliberately-fenced memory. It strongly suggests the code jumped to a location that was never supposed to be executed.";
			if (allNop) return "All NOP instructions (0x90) - this looks like a NOP sled, often used as padding in packed/protected executables.";
			if (allFF) return "All 0xFF bytes - unusual. May indicate uninitialized memory or a corrupt structure.";
			// Mixed region but dominated by NOPs: still a packing signal.
			if (nopCount > n / 2) return "The region is dominated by NOP instructions (" + nopCount + " out of " + n + "). This is a strong indicator of packed/protected code or code that has been patched at runtime.";

			return null;
		}

		// MEM_* state flag to string.
		private static string StateToString(uint state)
		{
			switch (state)
			{
				case 0x1000: return "MEM_COMMIT";
				case 0x2000: return "MEM_RESERVE";
				case 0x10000: return "MEM_FREE";
				default: return "0x" + state.ToString("X");
			}
		}

		// PAGE_* protection flag to string.
		private static string ProtectToString(uint protect)
		{
			switch (protect)
			{
				case 0x01: return "PAGE_NOACCESS";
				case 0x02: return "PAGE_READONLY";
				case 0x04: return "PAGE_READWRITE";
				case 0x08: return "PAGE_WRITECOPY";
				case 0x10: return "PAGE_EXECUTE";
				case 0x20: return "PAGE_EXECUTE_READ";
				case 0x40: return "PAGE_EXECUTE_READWRITE";
				case 0x80: return "PAGE_EXECUTE_WRITECOPY";
				case 0x100: return "PAGE_GUARD";
				case 0x400: return "PAGE_NOCACHE";
				case 0x00: return "PAGE_NOACCESS (0)";
				default: return "0x" + protect.ToString("X");
			}
		}

		// MEM_* type flag to string.
		private static string TypeToString(uint type)
		{
			switch (type)
			{
				case 0x20000: return "MEM_PRIVATE";
				case 0x40000: return "MEM_MAPPED";
				case 0x1000000: return "MEM_IMAGE";
				default: return type == 0 ? "(none)" : "0x" + type.ToString("X");
			}
		}

		// Turns the (state, protect, type) triple into a plain-language
		// explanation of what the crash likely means.
		private static string DescribeCombination(uint state, uint protect, uint type)
		{
			if (state == 0x10000) return "This memory is FREE - nothing is mapped here. The program tried to access an address that belongs to no allocation at all. This is one of the clearest signals that the pointer was wild (uninitialized or corrupted).";

			if (state == 0x2000) return "This memory is RESERVED but not committed. The program accessed a page that exists in the address space but has no physical backing. Usually a pointer that was set but not fully initialized.";

			if (state == 0x1000)
			{
				if ((protect & 0x100) != 0) return "This region is marked PAGE_GUARD - the program hit a stack guard page. This is what normally causes a stack-overflow crash.";
				if (type == 0x1000000) return "This is a memory-mapped IMAGE (code or data from a loaded executable/DLL).";
				if (type == 0x40000) return "This is a memory-mapped file or shared section.";
				if (type == 0x20000) return "This is PRIVATE memory - either heap or stack. In the context of an access violation, this usually means a pointer into a heap block or stack frame that was already freed or that was never correctly initialized.";
			}

			return null;
		}
	}
}