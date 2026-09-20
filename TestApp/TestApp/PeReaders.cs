/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen 
 * 
 * 
 * Static PE (.exe/.dll) file header readers - used to inspect a target
 * program before/without running it: its declared imports, delay-loaded
 * imports, exports, sections, and basic architecture info. Kept separate
 * from the live debugging engine.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TestApp
{
	internal static class PeImportReader
	{
		// Reads the standard import directory and returns the list of
		// referenced DLL names. Returns an empty list on any failure.
		public static List<string> GetImportedDllNames(string filePath)
		{
			List<string> result = new List<string>();
			try
			{
				using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				using (BinaryReader br = new BinaryReader(fs))
				{
					// Locate the import directory (data directory index 1).
					long importFileOffset = FindImportDirectory(fs, br, false);
					if (importFileOffset < 0) return result;

					// We need the section table to convert RVAs into file
					// offsets for the module-name strings.
					List<uint> sectVirtAddr, sectVirtSize, sectRawPtr;
					ReadSectionTable(fs, br, out sectVirtAddr, out sectVirtSize, out sectRawPtr);

					// Walk the import descriptor array. It is terminated by
					// an all-zero entry.
					fs.Seek(importFileOffset, SeekOrigin.Begin);
					while (true)
					{
						uint originalFirstThunk = br.ReadUInt32();
						br.ReadUInt32();
						br.ReadUInt32();
						uint nameRva = br.ReadUInt32();
						uint firstThunk = br.ReadUInt32();

						if (originalFirstThunk == 0 && nameRva == 0 && firstThunk == 0) break;

						// Convert the DLL-name RVA into a file offset and
						// read the ASCII string there.
						long nameOffset = RvaToFileOffset(nameRva, sectVirtAddr, sectVirtSize, sectRawPtr);
						if (nameOffset >= 0)
						{
							long savedPos = fs.Position;
							fs.Seek(nameOffset, SeekOrigin.Begin);
							string name = ReadAsciiString(br);
							if (!string.IsNullOrEmpty(name) && !result.Contains(name))
							{
								result.Add(name);
							}
							fs.Seek(savedPos, SeekOrigin.Begin);
						}
					}
				}
			}
			catch
			{
			}
			return result;
		}

		// Reads the delay-load import directory and returns the list of
		// delay-loaded DLL names. Returns an empty list on any failure.
		public static List<string> GetDelayImportedDllNames(string filePath)
		{
			List<string> result = new List<string>();
			try
			{
				using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				using (BinaryReader br = new BinaryReader(fs))
				{
					// Delay imports live in data directory index 13.
					long delayOffset = FindImportDirectory(fs, br, true);
					if (delayOffset < 0) return result;

					List<uint> sectVirtAddr, sectVirtSize, sectRawPtr;
					ReadSectionTable(fs, br, out sectVirtAddr, out sectVirtSize, out sectRawPtr);

					// Walk the delay-load descriptor array. Each entry is
					// 32 bytes; the array is terminated by an all-zero entry.
					fs.Seek(delayOffset, SeekOrigin.Begin);
					while (true)
					{
						uint attributes = br.ReadUInt32();
						uint nameRva = br.ReadUInt32();
						uint moduleHandleRva = br.ReadUInt32();
						uint importAddressTableRva = br.ReadUInt32();
						uint importNameTableRva = br.ReadUInt32();
						uint boundImportAddressTableRva = br.ReadUInt32();
						uint unloadInformationTableRva = br.ReadUInt32();
						uint timeDateStamp = br.ReadUInt32();

						if (attributes == 0 && nameRva == 0 && moduleHandleRva == 0 &&
							importAddressTableRva == 0 && importNameTableRva == 0 &&
							boundImportAddressTableRva == 0 && unloadInformationTableRva == 0)
						{
							break;
						}

						// When the "RVA-based" attribute bit is not set, the
						// name field holds a virtual address instead of an
						// RVA, so we subtract the ImageBase.
						uint realNameRva = nameRva;
						if ((attributes & 1) != 0)
						{
							uint imageBase = ReadImageBase(fs, br);
							realNameRva = nameRva - imageBase;
						}

						long nameOffset = RvaToFileOffset(realNameRva, sectVirtAddr, sectVirtSize, sectRawPtr);
						if (nameOffset >= 0)
						{
							long savedPos = fs.Position;
							fs.Seek(nameOffset, SeekOrigin.Begin);
							string name = ReadAsciiString(br);
							if (!string.IsNullOrEmpty(name) && !result.Contains(name))
							{
								result.Add(name);
							}
							fs.Seek(savedPos, SeekOrigin.Begin);
						}
					}
				}
			}
			catch
			{
			}
			return result;
		}

		// Reads the PE ImageBase field from the optional header. Handles
		// both PE32 and PE32+ layouts. Returns the classic 0x400000 on
		// any failure, which is the common default.
		private static uint ReadImageBase(FileStream fs, BinaryReader br)
		{
			try
			{
				long savedPos = fs.Position;
				fs.Seek(0x3C, SeekOrigin.Begin);
				int peOffset = br.ReadInt32();
				fs.Seek(peOffset + 4 + 20, SeekOrigin.Begin);
				ushort magic = br.ReadUInt16();
				uint imageBase;
				if (magic == 0x20B)
				{
					// PE32+ : ImageBase is a 64-bit value at optional+24.
					fs.Seek(peOffset + 4 + 20 + 24, SeekOrigin.Begin);
					imageBase = (uint)br.ReadUInt64();
				}
				else
				{
					// PE32 : ImageBase is a 32-bit value at optional+28.
					fs.Seek(peOffset + 4 + 20 + 28, SeekOrigin.Begin);
					imageBase = br.ReadUInt32();
				}
				fs.Seek(savedPos, SeekOrigin.Begin);
				return imageBase;
			}
			catch
			{
				return 0x400000;
			}
		}

		// Reads the section table and fills three parallel lists: virtual
		// address, virtual size, and file pointer for each section.
		private static void ReadSectionTable(FileStream fs, BinaryReader br,
			out List<uint> sectVirtAddr, out List<uint> sectVirtSize, out List<uint> sectRawPtr)
		{
			sectVirtAddr = new List<uint>();
			sectVirtSize = new List<uint>();
			sectRawPtr = new List<uint>();

			long savedPos = fs.Position;
			fs.Seek(0x3C, SeekOrigin.Begin);
			int peOffset = br.ReadInt32();
			fs.Seek(peOffset + 4 + 16, SeekOrigin.Begin);
			short sizeOfOptionalHeader = br.ReadInt16();
			fs.Seek(peOffset + 4 + 2, SeekOrigin.Begin);
			short numberOfSections = br.ReadInt16();

			long sectionTableOffset = peOffset + 4 + 20 + sizeOfOptionalHeader;
			fs.Seek(sectionTableOffset, SeekOrigin.Begin);
			for (int i = 0; i < numberOfSections; i++)
			{
				// Skip the 8-byte section name.
				fs.Seek(8, SeekOrigin.Current);
				uint virtualSize = br.ReadUInt32();
				uint virtualAddress = br.ReadUInt32();
				// Skip SizeOfRawData.
				fs.Seek(4, SeekOrigin.Current);
				uint pointerToRawData = br.ReadUInt32();
				// Skip the remaining fields of the section header.
				fs.Seek(16, SeekOrigin.Current);

				sectVirtAddr.Add(virtualAddress);
				sectVirtSize.Add(virtualSize);
				sectRawPtr.Add(pointerToRawData);
			}
			fs.Seek(savedPos, SeekOrigin.Begin);
		}

		// Returns the file offset of the import directory (or delay-import
		// directory when delayImport is true), or -1 on failure.
		private static long FindImportDirectory(FileStream fs, BinaryReader br, bool delayImport)
		{
			try
			{
				long savedPos = fs.Position;
				fs.Seek(0x3C, SeekOrigin.Begin);
				int peOffset = br.ReadInt32();
				fs.Seek(peOffset + 4 + 16, SeekOrigin.Begin);
				short sizeOfOptionalHeader = br.ReadInt16();
				fs.Seek(2, SeekOrigin.Current);
				long optionalHeaderOffset = fs.Position;

				// Data directory base differs between PE32 and PE32+.
				ushort magic = br.ReadUInt16();
				int dirBase = (magic == 0x20B) ? 112 : 96;
				int dirIndex = delayImport ? 13 : 1;
				int dirOffsetInOptional = dirBase + (dirIndex * 8);

				fs.Seek(optionalHeaderOffset + dirOffsetInOptional, SeekOrigin.Begin);
				uint rva = br.ReadUInt32();

				fs.Seek(savedPos, SeekOrigin.Begin);
				if (rva == 0) return -1;

				List<uint> sectVirtAddr, sectVirtSize, sectRawPtr;
				ReadSectionTable(fs, br, out sectVirtAddr, out sectVirtSize, out sectRawPtr);
				return RvaToFileOffset(rva, sectVirtAddr, sectVirtSize, sectRawPtr);
			}
			catch
			{
				return -1;
			}
		}

		// Reads a NUL-terminated ASCII string, capping the length at 512
		// bytes so a malformed file cannot cause a runaway read.
		public static string ReadAsciiString(BinaryReader br)
		{
			StringBuilder sb = new StringBuilder();
			byte b = br.ReadByte();
			int safety = 0;
			while (b != 0 && safety < 512)
			{
				sb.Append((char)b);
				b = br.ReadByte();
				safety++;
			}
			return sb.ToString();
		}

		// Converts an RVA to a file offset using the section table.
		// Returns -1 when the RVA is not inside any section.
		public static long RvaToFileOffset(uint rva, List<uint> sectVirtAddr, List<uint> sectVirtSize, List<uint> sectRawPtr)
		{
			for (int i = 0; i < sectVirtAddr.Count; i++)
			{
				uint start = sectVirtAddr[i];
				uint size = sectVirtSize[i];
				if (rva >= start && rva < start + size)
				{
					return sectRawPtr[i] + (rva - start);
				}
			}
			return -1;
		}
	}

	internal static class PeHeaderReader
	{
		// Summary of the interesting fields from a PE file. All string
		// fields are pre-formatted for display.
		public struct PeInfo
		{
			public bool Success;
			public ushort Machine;
			public string MachineName;
			public ushort SubsystemMajor;
			public ushort SubsystemMinor;
			public ushort Subsystem;
			public uint EntryPointRva;
			public ushort DllCharacteristics;
			public ushort NumberOfSections;
			public bool IsDotNet;
			public bool HasTls;
			public bool HasResources;
			public bool HasRelocations;
			public bool HasDebugInfo;
			public bool HasDelayImports;
			public string PackedHint;
			public List<string> SectionInfo;
			public string DllCharacteristicsFlags;
			public uint ImageBase;
			public string SubsystemName;
		}

		// Parses the PE header and returns a filled PeInfo. Success is
		// false when the file is not a valid PE or cannot be read.
		public static PeInfo GetBasicInfo(string filePath)
		{
			PeInfo info = new PeInfo();
			info.Success = false;
			info.MachineName = "Unknown";
			info.SectionInfo = new List<string>();

			try
			{
				using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				using (BinaryReader br = new BinaryReader(fs))
				{
					// PE offset is at DOS header offset 0x3C.
					fs.Seek(0x3C, SeekOrigin.Begin);
					int peOffset = br.ReadInt32();

					// Verify the "PE\0\0" signature.
					fs.Seek(peOffset, SeekOrigin.Begin);
					uint signature = br.ReadUInt32();
					if (signature != 0x00004550) return info;

					info.Machine = br.ReadUInt16();
					info.MachineName = MachineToString(info.Machine);

					info.NumberOfSections = (ushort)br.ReadInt16();
					br.ReadUInt32();
					br.ReadUInt32();
					br.ReadUInt32();
					short sizeOfOptionalHeader = br.ReadInt16();
					ushort characteristics = br.ReadUInt16();

					long optionalHeaderOffset = fs.Position;
					ushort magic = br.ReadUInt16();

					if (magic == 0x20B)
					{
						// PE32+
						fs.Seek(optionalHeaderOffset + 16, SeekOrigin.Begin);
						info.EntryPointRva = br.ReadUInt32();
						fs.Seek(optionalHeaderOffset + 24, SeekOrigin.Begin);
						info.ImageBase = (uint)br.ReadUInt64();
						fs.Seek(optionalHeaderOffset + 48, SeekOrigin.Begin);
						info.SubsystemMajor = br.ReadUInt16();
						info.SubsystemMinor = br.ReadUInt16();
						// CORRECT: Subsystem at offset 68, DllCharacteristics at offset 70
						fs.Seek(optionalHeaderOffset + 68, SeekOrigin.Begin);
						info.Subsystem = br.ReadUInt16();
						fs.Seek(optionalHeaderOffset + 70, SeekOrigin.Begin);
						info.DllCharacteristics = br.ReadUInt16();
						// Data directory base for PE32+ is 112.
						info.HasResources = HasDirectory(fs, br, optionalHeaderOffset, 112, 2);
						info.HasRelocations = HasDirectory(fs, br, optionalHeaderOffset, 112, 5);
						info.HasDebugInfo = HasDirectory(fs, br, optionalHeaderOffset, 112, 6);
						info.HasTls = HasDirectory(fs, br, optionalHeaderOffset, 112, 9);
						info.HasDelayImports = HasDirectory(fs, br, optionalHeaderOffset, 112, 13);
						info.IsDotNet = HasDirectory(fs, br, optionalHeaderOffset, 112, 14);
					}
					else
					{
						// PE32
						fs.Seek(optionalHeaderOffset + 16, SeekOrigin.Begin);
						info.EntryPointRva = br.ReadUInt32();
						fs.Seek(optionalHeaderOffset + 28, SeekOrigin.Begin);
						info.ImageBase = br.ReadUInt32();
						fs.Seek(optionalHeaderOffset + 48, SeekOrigin.Begin);
						info.SubsystemMajor = br.ReadUInt16();
						info.SubsystemMinor = br.ReadUInt16();
						// CORRECT: Subsystem at offset 68, DllCharacteristics at offset 70
						fs.Seek(optionalHeaderOffset + 68, SeekOrigin.Begin);
						info.Subsystem = br.ReadUInt16();
						fs.Seek(optionalHeaderOffset + 70, SeekOrigin.Begin);
						info.DllCharacteristics = br.ReadUInt16();
						// Data directory base for PE32 is 96.
						info.HasResources = HasDirectory(fs, br, optionalHeaderOffset, 96, 2);
						info.HasRelocations = HasDirectory(fs, br, optionalHeaderOffset, 96, 5);
						info.HasDebugInfo = HasDirectory(fs, br, optionalHeaderOffset, 96, 6);
						info.HasTls = HasDirectory(fs, br, optionalHeaderOffset, 96, 9);
						info.HasDelayImports = HasDirectory(fs, br, optionalHeaderOffset, 96, 13);
						info.IsDotNet = HasDirectory(fs, br, optionalHeaderOffset, 96, 14);
					}

					info.SubsystemName = SubsystemToString(info.Subsystem);
					info.DllCharacteristicsFlags = DescribeDllCharacteristics(info.DllCharacteristics);

					ReadSectionInfo(fs, br, peOffset, sizeOfOptionalHeader, info);

					info.PackedHint = DetectPacked(info.EntryPointRva, info.SectionInfo, info.NumberOfSections);

					info.Success = true;
				}
			}
			catch
			{
				info.Success = false;
			}
			return info;
		}

		// Returns true when the given data directory entry has a non-zero
		// RVA and size (meaning the directory is present in the file).
		private static bool HasDirectory(FileStream fs, BinaryReader br, long optionalHeaderOffset, int dirBase, int index)
		{
			try
			{
				long savedPos = fs.Position;
				fs.Seek(optionalHeaderOffset + dirBase + (index * 8), SeekOrigin.Begin);
				uint rva = br.ReadUInt32();
				uint size = br.ReadUInt32();
				fs.Seek(savedPos, SeekOrigin.Begin);
				return rva != 0 && size != 0;
			}
			catch
			{
				return false;
			}
		}

		// Reads the section table and appends a short description of each
		// section to info.SectionInfo.
		private static void ReadSectionInfo(FileStream fs, BinaryReader br, int peOffset, short sizeOfOptionalHeader, PeInfo info)
		{
			try
			{
				long sectionTableOffset = peOffset + 4 + 20 + sizeOfOptionalHeader;
				fs.Seek(sectionTableOffset, SeekOrigin.Begin);
				for (int i = 0; i < info.NumberOfSections; i++)
				{
					byte[] nameBytes = br.ReadBytes(8);
					string cleanName = SanitizeName(nameBytes);
					uint virtualSize = br.ReadUInt32();
					uint virtualAddress = br.ReadUInt32();
					uint rawSize = br.ReadUInt32();
					uint rawPtr = br.ReadUInt32();
					br.ReadUInt32();
					br.ReadUInt32();
					ushort sectionChar = br.ReadUInt16();
					br.ReadUInt16();

					info.SectionInfo.Add(
						"  " + cleanName.PadRight(9) +
						" RVA=0x" + virtualAddress.ToString("X8") +
						" VSize=0x" + virtualSize.ToString("X8") +
						" RawSize=0x" + rawSize.ToString("X8") +
						" Flags=0x" + sectionChar.ToString("X4"));
				}
			}
			catch
			{
			}
		}

		// Converts an 8-byte section name into printable ASCII. Non-
		// printable bytes become '?'. Empty names become "(unnamed)".
		private static string SanitizeName(byte[] nameBytes)
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < 8; i++)
			{
				if (nameBytes[i] == 0) break;
				if (nameBytes[i] >= 0x20 && nameBytes[i] < 0x7F)
				{
					sb.Append((char)nameBytes[i]);
				}
				else
				{
					sb.Append('?');
				}
			}
			string result = sb.ToString();
			if (result.Length == 0) return "(unnamed)";
			return result;
		}

		// Looks for well-known packer section names and returns a short
		// hint when one is spotted, or null when nothing obvious matches.
		private static string DetectPacked(uint entryPointRva, List<string> sections, int sectionCount)
		{
			try
			{
				if (sections == null || sections.Count == 0) return null;
				foreach (string s in sections)
				{
					string lower = s.ToLowerInvariant();
					if (lower.Contains("upx0") || lower.Contains("upx1") || lower.Contains("upx2"))
						return "UPX packer signature detected";
					if (lower.Contains(".aspack") || lower.Contains(".adata"))
						return "ASPack packer signature detected";
					if (lower.Contains("themida") || lower.Contains(".themida"))
						return "Themida packer signature detected";
					if (lower.Contains(".vmp") || lower.Contains("vmp0") || lower.Contains("vmp1"))
						return "VMProtect packer signature detected";
					if (lower.Contains(".mpress") || lower.Contains("mpress1") || lower.Contains("mpress2"))
						return "MPRESS packer signature detected";
					if (lower.Contains(".petite"))
						return "Petite packer signature detected";
					if (lower.Contains(".enigma"))
						return "Enigma packer signature detected";
					if (lower.Contains(".packed") || lower.Contains("packed"))
						return "Generic packed section name detected";
				}
			}
			catch
			{
			}
			return null;
		}

		// Turns the DllCharacteristics bitfield into a comma-separated
		// list of flag names. Returns "(none)" when no flag is set.
		private static string DescribeDllCharacteristics(ushort flags)
		{
			List<string> parts = new List<string>();
			if ((flags & 0x0020) != 0) parts.Add("HIGH_ENTROPY_VA");
			if ((flags & 0x0040) != 0) parts.Add("DYNAMIC_BASE (ASLR)");
			if ((flags & 0x0080) != 0) parts.Add("FORCE_INTEGRITY");
			if ((flags & 0x0100) != 0) parts.Add("NX_COMPAT (DEP)");
			if ((flags & 0x0200) != 0) parts.Add("NO_ISOLATION");
			if ((flags & 0x0400) != 0) parts.Add("NO_SEH");
			if ((flags & 0x0800) != 0) parts.Add("NO_BIND");
			if ((flags & 0x1000) != 0) parts.Add("APPCONTAINER");
			if ((flags & 0x2000) != 0) parts.Add("WDM_DRIVER");
			if ((flags & 0x4000) != 0) parts.Add("GUARD_CF");
			if ((flags & 0x8000) != 0) parts.Add("TERMINAL_SERVER_AWARE");
			if (parts.Count == 0) return "(none)";
			return string.Join(", ", parts.ToArray());
		}

		// Machine type -> human name.
		private static string MachineToString(ushort machine)
		{
			switch (machine)
			{
				case 0x014c: return "x86 (32-bit)";
				case 0x8664: return "x64 (64-bit)";
				case 0x01c0: return "ARM";
				case 0x01c4: return "ARM (Thumb-2)";
				case 0xAA64: return "ARM64";
				case 0x0200: return "IA64 (Itanium)";
				default: return "Unknown (0x" + machine.ToString("X4") + ")";
			}
		}

		// Subsystem value -> human name.
		private static string SubsystemToString(ushort subsystem)
		{
			switch (subsystem)
			{
				case 0: return "Unknown";
				case 1: return "Native";
				case 2: return "Windows GUI";
				case 3: return "Windows Console";
				case 5: return "OS/2 Console";
				case 7: return "POSIX Console";
				case 8: return "Native Windows";
				case 9: return "Windows CE GUI";
				case 10: return "EFI Application";
				case 11: return "EFI Boot Service Driver";
				case 12: return "EFI Runtime Driver";
				case 13: return "EFI ROM";
				case 14: return "Xbox";
				case 16: return "Windows Boot Application";
				default: return "Unknown (" + subsystem.ToString() + ")";
			}
		}
	}
}