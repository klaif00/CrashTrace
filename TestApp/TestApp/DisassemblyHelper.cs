/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
* Developed by Limen
 * Wraps the SharpDisasm disassembler and provides a small, focused API
 * for building a readable mini-disassembly around a crash address.
 * If SharpDisasm is not available for any reason (DLL missing, wrong
 * version, unexpected runtime error), this helper silently falls back
 * to the BasicDisassembler that ships with the tool, so the report is
 * never left without a disassembly section.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace TestApp
{
	internal static class DisassemblyHelper
	{
		// Result of a build attempt. Engine names which decoder actually
		// produced the text, so the report can state it truthfully.
		public struct MiniResult
		{
			public bool Success;
			public string Text;
			public string Engine;
		}

		// Reads bytes around the crash site and produces a readable
		// disassembly, using SharpDisasm when possible.
		public static MiniResult Build(
			IntPtr hProcess,
			IntPtr crashAddress,
			bool is64Bit,
			int beforeLen,
			int afterLen)
		{
			MiniResult result = new MiniResult();
			result.Success = false;
			result.Engine = "none";
			result.Text = null;

			try
			{
				if (hProcess == IntPtr.Zero || crashAddress == IntPtr.Zero)
					return result;

				// Read a window of bytes that starts beforeLen bytes before
				// the crash address and extends afterLen bytes after it.
				IntPtr startAddr = new IntPtr(crashAddress.ToInt64() - beforeLen);
				int totalLen = beforeLen + afterLen;
				int bytesRead;
				byte[] bytes = MemoryInspector.ReadBytes(hProcess, startAddr, totalLen, out bytesRead);
				if (bytes == null || bytesRead < 8) return result;

				// Try SharpDisasm first (much more accurate).
				string sharp = null;
				try
				{
					sharp = BuildWithSharpDisasm(bytes, bytesRead, startAddr, crashAddress, is64Bit);
				}
				catch
				{
					sharp = null;
				}

				if (!string.IsNullOrEmpty(sharp))
				{
					result.Success = true;
					result.Text = sharp;
					result.Engine = "SharpDisasm (udis86)";
					return result;
				}

				// Fall back to the built-in basic decoder.
				string basic = null;
				try
				{
					basic = BuildWithBasicDisassembler(bytes, bytesRead, startAddr, crashAddress, beforeLen, is64Bit);
				}
				catch
				{
					basic = null;
				}

				if (!string.IsNullOrEmpty(basic))
				{
					result.Success = true;
					result.Text = basic;
					result.Engine = "BasicDisassembler (built-in fallback)";
					return result;
				}
			}
			catch
			{
			}

			return result;
		}

		// ------------------------------------------------------------------
		// SharpDisasm path
		// ------------------------------------------------------------------
		private static string BuildWithSharpDisasm(
			byte[] bytes, int bytesRead, IntPtr startAddr,
			IntPtr crashAddress, bool is64Bit)
		{
			long crashAddr = crashAddress.ToInt64();

			SharpDisasm.ArchitectureMode mode = is64Bit
				? SharpDisasm.ArchitectureMode.x86_64
				: SharpDisasm.ArchitectureMode.x86_32;

			// The byte at startAddr may not be the start of an instruction
			// (we read a fixed number of bytes backwards from the crash).
			// Try a few different starting offsets until we find one where
			// the disassembly aligns with the crash address.
			List<SharpDisasm.Instruction> aligned = null;

			for (int trial = 0; trial < 16; trial++)
			{
				if (trial >= bytesRead) break;

				try
				{
					byte[] slice = new byte[bytesRead - trial];
					Array.Copy(bytes, trial, slice, 0, slice.Length);
					ulong sliceAddr = (ulong)(startAddr.ToInt64() + trial);

					var disassembler = new SharpDisasm.Disassembler(slice, mode, sliceAddr, true);
					var list = new List<SharpDisasm.Instruction>();

					foreach (var insn in disassembler.Disassemble())
					{
						list.Add(insn);
						if (list.Count > 400) break;
					}

					if (list.Count < 2) continue;

					// Only accept the trial if one of the decoded instructions
					// lands exactly on the crash address.
					for (int i = 0; i < list.Count; i++)
					{
						if ((long)list[i].Offset == crashAddr)
						{
							aligned = list;
							break;
						}
					}

					if (aligned != null) break;
				}
				catch
				{
					continue;
				}
			}

			if (aligned == null) return null;

			int crashIndex = -1;
			for (int i = 0; i < aligned.Count; i++)
			{
				if ((long)aligned[i].Offset == crashAddr)
				{
					crashIndex = i;
					break;
				}
			}
			if (crashIndex < 0) return null;

			// Show a small window of instructions around the crash.
			const int beforeCount = 8;
			const int afterCount = 8;
			int start = Math.Max(0, crashIndex - beforeCount);
			int end = Math.Min(aligned.Count - 1, crashIndex + afterCount);

			StringBuilder sb = new StringBuilder();
			for (int i = start; i <= end; i++)
			{
				SharpDisasm.Instruction insn = aligned[i];
				string text;
				try
				{
					text = insn.ToString();
					if (string.IsNullOrEmpty(text)) text = "(unknown)";
				}
				catch
				{
					text = "(format error)";
				}

				// Mark the crashing instruction so it stands out in the report.
				string marker = (i == crashIndex) ? "      <-- FAULTING INSTRUCTION" : "";
				sb.AppendLine("  0x" + insn.Offset.ToString("X8") + "    " + text + marker);
			}

			return sb.ToString();
		}

		// ------------------------------------------------------------------
		// BasicDisassembler fallback path (the original logic)
		// ------------------------------------------------------------------
		private static string BuildWithBasicDisassembler(
			byte[] bytes, int bytesRead, IntPtr startAddr,
			IntPtr crashAddress, int beforeLen, bool is64Bit)
		{
			StringBuilder sb = new StringBuilder();
			long crashAddr = crashAddress.ToInt64();

			// Find a start offset that lands exactly on the crash address.
			// We try each possible byte offset in the "before" region and
			// keep the first one whose linear decode reaches beforeLen
			// without overrun. That offset is treated as the true
			// instruction boundary.
			int bestStart = beforeLen;
			for (int offset = 0; offset < beforeLen && offset < 12; offset++)
			{
				int pos = offset;
				bool valid = true;
				int safety = 0;
				while (pos < beforeLen && safety < 25)
				{
					byte[] slice = SubArray(bytes, pos, bytesRead - pos);
					BasicDisassembler.DecodedInstruction ins = BasicDisassembler.Decode(slice, is64Bit);
					if (!ins.Success || ins.Length <= 0) { valid = false; break; }
					pos += ins.Length;
					if (pos == beforeLen) break;
					if (pos > beforeLen) { valid = false; break; }
					safety++;
				}
				if (valid && pos == beforeLen) { bestStart = offset; break; }
			}

			// Instructions before the fault.
			if (bestStart < beforeLen)
			{
				int pos = bestStart;
				int count = 0;
				while (pos < beforeLen && count < 6)
				{
					byte[] slice = SubArray(bytes, pos, bytesRead - pos);
					BasicDisassembler.DecodedInstruction ins = BasicDisassembler.Decode(slice, is64Bit);
					if (!ins.Success) break;
					long addr = startAddr.ToInt64() + pos;
					sb.AppendLine("  0x" + addr.ToString("X8") + "    " + ins.FullText);
					pos += ins.Length;
					count++;
				}
			}

			// The faulting instruction itself.
			int crashInstructionLength = 1;
			{
				byte[] slice = SubArray(bytes, beforeLen, bytesRead - beforeLen);
				BasicDisassembler.DecodedInstruction ins = BasicDisassembler.Decode(slice, is64Bit);
				if (ins.Success)
				{
					sb.AppendLine("  0x" + crashAddr.ToString("X8") + "    " + ins.FullText + "      <-- FAULTING INSTRUCTION");
					crashInstructionLength = ins.Length > 0 ? ins.Length : 1;
				}
			}

			// Instructions after the fault.
			{
				int pos = beforeLen + crashInstructionLength;
				int count = 0;
				while (pos < bytesRead && count < 6)
				{
					byte[] slice = SubArray(bytes, pos, bytesRead - pos);
					BasicDisassembler.DecodedInstruction ins = BasicDisassembler.Decode(slice, is64Bit);
					if (!ins.Success) break;
					long addr = startAddr.ToInt64() + pos;
					sb.AppendLine("  0x" + addr.ToString("X8") + "    " + ins.FullText);
					pos += ins.Length;
					count++;
				}
			}

			return sb.ToString();
		}

		// Safe slice helper. Clamps the range so callers do not have to
		// worry about running off the end of the buffer.
		private static byte[] SubArray(byte[] source, int start, int length)
		{
			if (source == null) return null;
			if (start < 0) start = 0;
			if (start >= source.Length) return new byte[0];
			if (start + length > source.Length) length = source.Length - start;
			if (length <= 0) return new byte[0];
			byte[] result = new byte[length];
			Array.Copy(source, start, result, 0, length);
			return result;
		}
	}
}