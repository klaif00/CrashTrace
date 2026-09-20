/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen
 * 
 * 
 * Reads companion log files next to the target executable. Many games
 * and applications write their own diagnostic logs which often contain
 * the specific error message that explains why they failed - info that
 * never reaches the debugger. This makes those logs part of the report.
 *
 * Important: this must NOT read our own report files (which sit in the
 * same folder as the target). Those are filtered out by pattern.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TestApp
{
	internal static class CompanionLogReader
	{
		// Extensions we consider as "log files".
		private static readonly string[] LogExtensions = new string[]
		{
			".log", ".err", ".error", ".crash"
		};

		// Filenames containing any of these words are ignored: they are
		// documentation, not runtime diagnostics.
		private static readonly string[] SkipNamePatterns = new string[]
		{
			"readme", "license", "eula", "changelog", "credits", "history"
		};

		// Finds log files near the exe and returns them as a flat list of
		// lines (with a small header per file). Reads at most maxLinesPerFile
		// from the tail of each file, and at most maxFiles files total.
		public static List<string> FindAndRead(string exePath, int maxLinesPerFile, int maxFiles)
		{
			List<string> result = new List<string>();
			try
			{
				string dir = Path.GetDirectoryName(exePath);
				string exeName = Path.GetFileNameWithoutExtension(exePath);
				if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return result;

				List<string> candidates = new List<string>();

				// Priority 1: log files named after the exe (exact match + .log or .err)
				foreach (string ext in LogExtensions)
				{
					try
					{
						string p = Path.Combine(dir, exeName + ext);
						if (File.Exists(p) && !IsOurOwnOutput(p, exeName)) candidates.Add(p);
					}
					catch { }
				}

				// Priority 2: any .log / .err / .error file in the same folder,
				// but only if we haven't found enough yet.
				if (candidates.Count < maxFiles)
				{
					try
					{
						foreach (string ext in LogExtensions)
						{
							foreach (string file in Directory.GetFiles(dir, "*" + ext))
							{
								if (candidates.Count >= maxFiles) break;
								if (candidates.Contains(file)) continue;
								if (IsOurOwnOutput(file, exeName)) continue;
								if (IsNoise(file)) continue;
								candidates.Add(file);
							}
						}
					}
					catch { }
				}

				// Read the tail of each candidate. A per-file header is added
				// so the reader knows where each log starts and how much was
				// actually shown.
				foreach (string file in candidates)
				{
					try
					{
						FileInfo fi = new FileInfo(file);
						// Skip huge files - they're usually data, not logs.
						if (fi.Length > 5 * 1024 * 1024) continue;

						string[] lines = File.ReadAllLines(file);
						int start = Math.Max(0, lines.Length - maxLinesPerFile);

						result.Add("=== " + Path.GetFileName(file) + " (last " +
							Math.Min(maxLinesPerFile, lines.Length) + " of " + lines.Length + " lines) ===");
						for (int i = start; i < lines.Length; i++)
						{
							result.Add(lines[i]);
						}
						result.Add("");
					}
					catch
					{
					}
				}
			}
			catch
			{
			}
			return result;
		}

		// True when the filename contains a known documentation keyword.
		private static bool IsNoise(string filePath)
		{
			try
			{
				string name = Path.GetFileName(filePath).ToLowerInvariant();
				foreach (string skip in SkipNamePatterns)
				{
					if (name.Contains(skip)) return true;
				}
			}
			catch
			{
			}
			return false;
		}

		// Detects whether a given file was produced by this diagnostic tool
		// itself, so we don't treat our own reports as game logs.
		private static bool IsOurOwnOutput(string filePath, string exeName)
		{
			try
			{
				string name = Path.GetFileName(filePath);
				if (string.IsNullOrEmpty(name)) return false;
				string lower = name.ToLowerInvariant();

				// Our own satellite files
				if (lower.EndsWith(".dmp")) return true;
				if (lower.EndsWith("_screenshot.png")) return true;
				if (lower.EndsWith("_crashsite.bin")) return true;

				// Our report pattern: <exeName>_YYYY-MM-DD_HH-MM-SS.(txt|html|json)
				if (name.StartsWith(exeName + "_", StringComparison.OrdinalIgnoreCase))
				{
					string rest = name.Substring(exeName.Length + 1);
					// Expected format: 2026-09-19_07-16-20.txt
					if (rest.Length >= 19
						&& IsDigit(rest, 0) && IsDigit(rest, 1) && IsDigit(rest, 2) && IsDigit(rest, 3)
						&& rest[4] == '-'
						&& IsDigit(rest, 5) && IsDigit(rest, 6)
						&& rest[7] == '-'
						&& IsDigit(rest, 8) && IsDigit(rest, 9)
						&& rest[10] == '_'
						&& IsDigit(rest, 11) && IsDigit(rest, 12)
						&& rest[13] == '-'
						&& IsDigit(rest, 14) && IsDigit(rest, 15)
						&& rest[16] == '-'
						&& IsDigit(rest, 17) && IsDigit(rest, 18))
					{
						return true;
					}
				}
			}
			catch
			{
			}
			return false;
		}

		// Small guard helper used by the timestamp pattern check above.
		private static bool IsDigit(string s, int i)
		{
			if (i < 0 || i >= s.Length) return false;
			char c = s[i];
			return c >= '0' && c <= '9';
		}
	}
}