/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen
 * Minimal x86/x64 instruction decoder. Not a full disassembler - just
 * enough to identify the type of the faulting instruction and produce
 * a readable mnemonic for the most common cases. The goal is to give
 * the user context like "the crash happened while executing a MOV that
 * read from [EAX+ESI*8]" rather than a hex blob.
 */
using System;
using System.Text;

namespace TestApp
{
	internal static class BasicDisassembler
	{
		// Result of a single decode attempt. Success tells the caller
		// whether the bytes were understood. FullText is ready to show
		// in the UI. Note carries the human-friendly explanation.
		public struct DecodedInstruction
		{
			public bool Success;
			public int Length;
			public string Mnemonic;
			public string Operands;
			public string FullText;
			public string Note;
		}

		// Decodes a single instruction starting at bytes[0].
		// Returns Success = false if the input is empty or decoding fails.
		public static DecodedInstruction Decode(byte[] bytes, bool is64Bit)
		{
			DecodedInstruction r = new DecodedInstruction();
			r.Success = false;
			if (bytes == null || bytes.Length < 1) return r;

			try
			{
				int pos = 0;

				// Skip legacy prefixes
				while (pos < bytes.Length && IsPrefix(bytes[pos])) pos++;
				if (pos >= bytes.Length) return r;

				// Skip REX prefix (x64 only, 0x40..0x4F)
				bool rexW = false;
				if (is64Bit && bytes[pos] >= 0x40 && bytes[pos] <= 0x4F)
				{
					rexW = (bytes[pos] & 0x08) != 0;
					pos++;
				}
				if (pos >= bytes.Length) return r;

				byte op = bytes[pos];

				// Simple, fixed-size opcodes with no operands or with a
				// single register/immediate operand.
				if (op == 0x90) { r.Length = pos + 1; r.Mnemonic = "NOP"; r.Operands = ""; r.Note = "No operation."; }
				else if (op == 0xCC) { r.Length = pos + 1; r.Mnemonic = "INT3"; r.Operands = ""; r.Note = "Debug breakpoint."; }
				else if (op == 0xC3) { r.Length = pos + 1; r.Mnemonic = "RET"; r.Operands = ""; r.Note = "Return from function."; }
				else if (op == 0xC2) { r.Length = pos + 3; r.Mnemonic = "RET"; r.Operands = "imm16"; r.Note = "Return and pop imm16 bytes."; }
				else if (op == 0xC9) { r.Length = pos + 1; r.Mnemonic = "LEAVE"; r.Operands = ""; r.Note = "Restore stack frame."; }
				else if (op == 0xF4) { r.Length = pos + 1; r.Mnemonic = "HLT"; r.Operands = ""; r.Note = "Halt - usually not executed in user mode."; }
				else if (op >= 0x50 && op <= 0x57) { r.Length = pos + 1; r.Mnemonic = "PUSH"; r.Operands = RegName(op - 0x50, is64Bit); r.Note = "Push register onto stack."; }
				else if (op >= 0x58 && op <= 0x5F) { r.Length = pos + 1; r.Mnemonic = "POP"; r.Operands = RegName(op - 0x58, is64Bit); r.Note = "Pop value from stack into register."; }
				else if (op == 0xE8) { r.Length = pos + 5; r.Mnemonic = "CALL"; r.Operands = "rel32"; r.Note = "Direct near call - the CPU pushes the return address and jumps to the target."; }
				else if (op == 0xE9) { r.Length = pos + 5; r.Mnemonic = "JMP"; r.Operands = "rel32"; r.Note = "Direct near jump."; }
				else if (op == 0xEB) { r.Length = pos + 2; r.Mnemonic = "JMP"; r.Operands = "rel8"; r.Note = "Short jump."; }
				else if (op >= 0x70 && op <= 0x7F) { r.Length = pos + 2; r.Mnemonic = "Jcc"; r.Operands = "rel8"; r.Note = "Conditional short jump."; }
				else if (op == 0x0F && pos + 1 < bytes.Length && bytes[pos + 1] >= 0x80 && bytes[pos + 1] <= 0x8F)
				{
					r.Length = pos + 6;
					r.Mnemonic = "Jcc";
					r.Operands = "rel32";
					r.Note = "Conditional near jump.";
				}
				// 0xFF is a group opcode. The ModRM.reg field selects the
				// actual instruction (INC, DEC, CALL, JMP, PUSH...).
				else if (op == 0xFF && pos + 1 < bytes.Length)
				{
					byte modrm = bytes[pos + 1];
					byte reg = (byte)((modrm >> 3) & 0x07);
					string subOp;
					string extraNote = null;
					switch (reg)
					{
						case 0: subOp = "INC"; break;
						case 1: subOp = "DEC"; break;
						case 2:
							subOp = "CALL";
							extraNote = "Indirect call through a register or memory operand. A crash on this instruction usually means the pointer being called was invalid (null or corrupted).";
							break;
						case 3: subOp = "CALL FAR"; break;
						case 4:
							subOp = "JMP";
							extraNote = "Indirect jump through a register or memory operand.";
							break;
						case 5: subOp = "JMP FAR"; break;
						case 6: subOp = "PUSH"; break;
						default: subOp = "?"; break;
					}
					r.Length = pos + 2;
					r.Mnemonic = subOp;
					r.Operands = DecodeModRM(bytes, pos + 1, is64Bit);
					r.Note = extraNote;
				}
				// MOV family (0x88-0x8B). These touch memory, so they are
				// the most common source of access-violation crashes.
				else if (op >= 0x88 && op <= 0x8B)
				{
					r.Length = pos + 2;
					r.Mnemonic = "MOV";
					r.Operands = DecodeModRM(bytes, pos + 1, is64Bit);
					if (op == 0x8B) r.Note = "Load a value from memory or register into a register. A crash here usually means the source address was invalid.";
					else r.Note = "Store a value to memory or register. A crash here usually means the destination address was invalid or read-only.";
				}
				// LEA never dereferences memory, so it does not crash on
				// bad addresses - worth mentioning in the note.
				else if (op == 0x8D)
				{
					r.Length = pos + 2;
					r.Mnemonic = "LEA";
					r.Operands = DecodeModRM(bytes, pos + 1, is64Bit);
					r.Note = "Load Effective Address - computes a memory address without accessing memory. Does not crash on bad addresses.";
				}
				// MOV EAX/RAX, imm (short form). REX.W changes the immediate size.
				else if (op == 0xB8)
				{
					r.Length = pos + (is64Bit && rexW ? 10 : 5);
					r.Mnemonic = "MOV";
					r.Operands = "EAX, imm";
					r.Note = "Load immediate value into register.";
				}
				// XOR group. Commonly used by the compiler to zero a register.
				else if (op >= 0x31 && op <= 0x33)
				{
					r.Length = pos + 2;
					r.Mnemonic = "XOR";
					r.Operands = DecodeModRM(bytes, pos + 1, is64Bit);
					r.Note = "Bitwise XOR. A common compiler idiom for zeroing a register.";
				}
				// CMP group. Reads both operands, so bad addresses show up here.
				else if (op >= 0x39 && op <= 0x3B)
				{
					r.Length = pos + 2;
					r.Mnemonic = "CMP";
					r.Operands = DecodeModRM(bytes, pos + 1, is64Bit);
					r.Note = "Compare - reads from memory or register. A crash here suggests a bad read address.";
				}
				// Anything else is outside this minimal decoder's table.
				else
				{
					r.Length = 1;
					r.Mnemonic = "(unknown)";
					r.Operands = "";
					r.Note = "Opcode 0x" + op.ToString("X2") + " is not in the minimal decoder's table. Full disassembly requires a dedicated tool like WinDbg or IDA.";
				}

				r.Success = true;
				r.FullText = r.Mnemonic + (string.IsNullOrEmpty(r.Operands) ? "" : " " + r.Operands);
				return r;
			}
			catch
			{
				r.Success = false;
				return r;
			}
		}

		// Legacy prefixes that can precede the real opcode.
		private static bool IsPrefix(byte b)
		{
			switch (b)
			{
				case 0x26: case 0x2E: case 0x36: case 0x3E:
				case 0x64: case 0x65:
				case 0x66: case 0x67:
				case 0xF0: case 0xF2: case 0xF3:
					return true;
			}
			return false;
		}

		// Maps a 0-7 register index to the correct name for the bitness.
		private static string RegName(int index, bool is64Bit)
		{
			if (is64Bit)
			{
				string[] regs = { "RAX", "RCX", "RDX", "RBX", "RSP", "RBP", "RSI", "RDI" };
				return regs[index];
			}
			string[] regs32 = { "EAX", "ECX", "EDX", "EBX", "ESP", "EBP", "ESI", "EDI" };
			return regs32[index];
		}

		// Decodes a ModRM byte and, if needed, the SIB byte and displacement
		// that follow it. Produces a human-readable operand string in the
		// form of "<reg>, [<memexpr>]" or "<reg>, <reg>".
		private static string DecodeModRM(byte[] bytes, int offset, bool is64Bit)
		{
			if (offset >= bytes.Length) return "?";
			byte modrm = bytes[offset];
			int mod = (modrm >> 6) & 0x03;
			int reg = (modrm >> 3) & 0x07;
			int rm = modrm & 0x07;

			string regName = RegName(reg, is64Bit);

			// Register-direct form (no memory access)
			if (mod == 3)
			{
				return regName + ", " + RegName(rm, is64Bit);
			}

			// SIB byte present when rm == 4 (in both x86 and x64, low 3 bits of
			// ModRM == 100 means "SIB follows").
			if (rm == 4)
			{
				return regName + ", " + DecodeSIBOperand(bytes, offset, mod, is64Bit);
			}

			// Regular ModRM memory operand with no SIB
			string baseName = RegName(rm, is64Bit);
			if (mod == 0 && rm == 5) return regName + ", [disp32]";
			if (mod == 1) return regName + ", [" + baseName + "+disp8]";
			if (mod == 2) return regName + ", [" + baseName + "+disp32]";
			return regName + ", [" + baseName + "]";
		}

		// Decodes the SIB byte (and any displacement) that accompanies a
		// memory operand. Handles the special cases for "no base" and "no
		// index" that the SIB encoding reserves.
		private static string DecodeSIBOperand(byte[] bytes, int modrmOffset, int mod, bool is64Bit)
		{
			int sibOffset = modrmOffset + 1;
			if (sibOffset >= bytes.Length) return "[?]";

			byte sib = bytes[sibOffset];
			int scale = (sib >> 6) & 0x03;
			int index = (sib >> 3) & 0x07;
			int baseReg = sib & 0x07;

			int scaleValue = 1 << scale; // 1, 2, 4, or 8

			// In SIB encoding, index == 4 means "no index" (ESP/RSP cannot
			// be used as an index register, so this slot is repurposed).
			string indexPart = (index == 4) ? null : RegName(index, is64Bit) + "*" + scaleValue;

			// base == 5 combined with mod == 0 means "no base, disp32 follows".
			string basePart = null;
			bool hasBase = !(mod == 0 && baseReg == 5);
			if (hasBase) basePart = RegName(baseReg, is64Bit);

			// Determine displacement
			string dispPart = null;
			if (mod == 0)
			{
				if (!hasBase) dispPart = "disp32";
			}
			else if (mod == 1)
			{
				dispPart = "disp8";
			}
			else if (mod == 2)
			{
				dispPart = "disp32";
			}

			// Build the memory operand string
			StringBuilder sb = new StringBuilder();
			sb.Append("[");
			bool wroteSomething = false;
			if (basePart != null)
			{
				sb.Append(basePart);
				wroteSomething = true;
			}
			if (indexPart != null)
			{
				if (wroteSomething) sb.Append("+");
				sb.Append(indexPart);
				wroteSomething = true;
			}
			if (dispPart != null)
			{
				if (wroteSomething) sb.Append("+");
				sb.Append(dispPart);
			}
			sb.Append("]");
			return sb.ToString();
		}
	}
}