/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen
 * 
 * 
 * Computes Shannon entropy for each section of a PE file. High entropy
 * (near 8.0) indicates compressed or encrypted data - a strong signal
 * that the section was packed with UPX, Themida, or similar, which has
 * direct consequences for debugging (breakpoints may not hit, stack
 * walks may be unreliable, and anti-cheat/anti-tamper may be involved).
 *
 * Also sanitizes section names so that non-ASCII bytes are shown as '?'
 * instead of garbled characters, and falls back to VirtualSize when the
 * raw size on disk is zero.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TestApp
{
	internal static class EntropyAnalyzer
	{
		// One section's entropy and its plain-language interpretation.
		public struct SectionEntropy
		{
			public string Name;
			public double Entropy;
			public long Size;
			public string Interpretation;
		}

		// Walks the PE section table and returns entropy data for each
		// section. Any failure returns an empty list, never throws.
		public static List<SectionEntropy> AnalyzeFile(string filePath)
		{
			List<SectionEntropy> results = new List<SectionEntropy>();
			try
			{
				using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				using (BinaryReader br = new BinaryReader(fs))
				{
					// PE offset is stored at 0x3C of the DOS header.
					fs.Seek(0x3C, SeekOrigin.Begin);
					int peOffset = br.ReadInt32();

					// Verify the "PE\0\0" signature.
					fs.Seek(peOffset, SeekOrigin.Begin);
					uint signature = br.ReadUInt32();
					if (signature != 0x00004550) return results;

					// NumberOfSections (2 bytes) at PE+6, SizeOfOptionalHeader
					// (2 bytes) at PE+20.
					fs.Seek(peOffset + 4 + 2, SeekOrigin.Begin);
					short numberOfSections = br.ReadInt16();
					fs.Seek(peOffset + 4 + 16, SeekOrigin.Begin);
					short sizeOfOptionalHeader = br.ReadInt16();

					// Section table starts right after the optional header.
					long sectionTableOffset = peOffset + 4 + 20 + sizeOfOptionalHeader;
					fs.Seek(sectionTableOffset, SeekOrigin.Begin);

					for (int i = 0; i < numberOfSections; i++)
					{
						// Each section header is 40 bytes. We read the fields
						// we need and skip the rest with ReadBytes/Read* calls.
						byte[] nameBytes = br.ReadBytes(8);
						string cleanName = SanitizeName(nameBytes);
						uint virtualSize = br.ReadUInt32();
						br.ReadUInt32(); // VirtualAddress
						uint rawSize = br.ReadUInt32();
						uint rawPtr = br.ReadUInt32();
						br.ReadUInt32(); br.ReadUInt32(); // relocs, linenumbers
						br.ReadUInt16(); br.ReadUInt16(); // counts

						// When the section occupies no space on disk (e.g.
						// purely-initialized data that is filled in at load
						// time), fall back to the virtual size so the user
						// still sees a meaningful number.
						uint effectiveSize = rawSize > 0 ? rawSize : virtualSize;

						SectionEntropy se = new SectionEntropy();
						se.Name = cleanName;
						se.Size = effectiveSize;
						se.Entropy = effectiveSize > 0 ? ComputeEntropy(fs, rawPtr, effectiveSize) : 0;
						se.Interpretation = Interpret(se.Entropy, effectiveSize, rawSize);
						results.Add(se);
					}
				}
			}
			catch
			{
			}
			return results;
		}

		// Converts an 8-byte section name into printable ASCII. Non-printable
		// bytes become '?'. Empty names become "(unnamed)".
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

		// Shannon entropy over the section's raw bytes. Returns 0 on any
		// failure, or for very large sections (the 50 MB cap prevents
		// spending seconds on a single call).
		private static double ComputeEntropy(FileStream fs, uint offset, uint size)
		{
			try
			{
				if (size == 0 || size > 50 * 1024 * 1024) return 0;
				if (offset == 0) return 0;
				fs.Seek(offset, SeekOrigin.Begin);

				long[] counts = new long[256];
				int totalRead = 0;
				byte[] buffer = new byte[8192];
				int remaining = (int)size;

				// Stream the section in 8 KB chunks so we never allocate
				// the whole section in memory.
				while (remaining > 0)
				{
					int toRead = Math.Min(buffer.Length, remaining);
					int read = fs.Read(buffer, 0, toRead);
					if (read <= 0) break;
					for (int i = 0; i < read; i++)
					{
						counts[buffer[i]]++;
					}
					totalRead += read;
					remaining -= read;
				}

				if (totalRead == 0) return 0;

				// Standard Shannon entropy formula: -sum(p * log2(p)).
				double entropy = 0;
				for (int i = 0; i < 256; i++)
				{
					if (counts[i] == 0) continue;
					double p = (double)counts[i] / totalRead;
					entropy -= p * Math.Log(p, 2);
				}
				return entropy;
			}
			catch
			{
				return 0;
			}
		}

		// Turns the entropy value into a short human-readable meaning.
		private static string Interpret(double entropy, long effectiveSize, uint rawSize)
		{
			if (effectiveSize == 0) return "Empty section";
			if (rawSize == 0) return "Purely virtual (no data on disk - filled in at load time)";
			if (entropy >= 7.5) return "VERY HIGH - almost certainly encrypted or compressed (packed/protected executable)";
			if (entropy >= 7.0) return "HIGH - likely compressed or contains encrypted data";
			if (entropy >= 6.0) return "Elevated - could contain compressed resources";
			if (entropy >= 3.0) return "Normal - typical code or data";
			return "Low - typically padding, zero-filled, or highly repetitive data";
		}
	}
}