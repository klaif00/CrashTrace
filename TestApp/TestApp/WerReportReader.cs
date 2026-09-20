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
 * Reads Windows Error Reporting (WER) reports that mention the target
 * executable. Windows writes these under %LOCALAPPDATA% and
 * %PROGRAMDATA% whenever a process crashes. The reports often contain
 * information that never reaches the debugger, such as faulting module
 * version numbers and driver information.
 */
using System;
using System.Collections.Generic;
using System.IO;

namespace TestApp
{
	internal static class WerReportReader
	{
		// Scans the local WER report folders for entries whose name
		// contains the target executable, then returns a filtered,
		// human-readable dump of the interesting lines from each
		// Report.wer file. Bounded by maxReports to keep the report
		// from growing without limit.
		public static List<string> FindReportsForProcess(string exeName, int maxReports)
		{
			List<string> results = new List<string>();
			try
			{
				List<string> basePaths = new List<string>();

				// Per-user WER folders. ReportArchive holds finalized
				// reports; ReportQueue holds reports still being written.
				try
				{
					string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
					if (!string.IsNullOrEmpty(local))
					{
						basePaths.Add(Path.Combine(local, "Microsoft", "Windows", "WER", "ReportArchive"));
						basePaths.Add(Path.Combine(local, "Microsoft", "Windows", "WER", "ReportQueue"));
					}
				}
				catch { }

				// Machine-wide WER folders. Used when the crash was
				// reported on behalf of the system rather than the user.
				try
				{
					string common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
					if (!string.IsNullOrEmpty(common))
					{
						basePaths.Add(Path.Combine(common, "Microsoft", "Windows", "WER", "ReportArchive"));
						basePaths.Add(Path.Combine(common, "Microsoft", "Windows", "WER", "ReportQueue"));
					}
				}
				catch { }

				string targetLower = exeName.ToLowerInvariant();

				foreach (string basePath in basePaths)
				{
					try
					{
						if (!Directory.Exists(basePath)) continue;
						foreach (string dir in Directory.GetDirectories(basePath))
						{
							try
							{
								// Report folder names contain the crashed
								// executable name; use that as the filter.
								string dirName = Path.GetFileName(dir);
								if (dirName.IndexOf(targetLower, StringComparison.OrdinalIgnoreCase) < 0)
									continue;

								results.Add("=== " + dirName + " ===");

								string reportFile = Path.Combine(dir, "Report.wer");
								if (File.Exists(reportFile))
								{
									string[] lines = File.ReadAllLines(reportFile);
									int emitted = 0;
									foreach (string line in lines)
									{
										// Only emit lines that carry useful info
										if (line.StartsWith("Sig[") || line.StartsWith("DynamicSig[") ||
											line.StartsWith("AppName=") || line.StartsWith("AppPath=") ||
											line.StartsWith("ModName=") || line.StartsWith("ModVer=") ||
											line.StartsWith("ExceptionCode=") || line.StartsWith("EventType=") ||
											line.StartsWith("OsInfo[") || line.StartsWith("AppCompatFlags="))
										{
											results.Add("  " + line);
											emitted++;
											if (emitted > 40) break;
										}
									}
								}
								results.Add("");

								// Cheap upper bound on total output size.
								if (results.Count > maxReports * 20) return results;
							}
							catch { }
						}
					}
					catch { }
				}
			}
			catch
			{
			}
			return results;
		}
	}
}