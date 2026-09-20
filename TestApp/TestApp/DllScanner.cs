/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen
 * Static dependency scanner. Given an EXE or DLL file, this class
 * reads its dependencies (both native PE imports and .NET assembly
 * references) and checks which of them actually exist on disk.
 *
 * Nothing is executed. No debugger is attached. This is pure
 * static analysis, similar to what DependenciesGui or PE-bear do.
 */
using System;
using System.Collections.Generic;
using System.IO;

namespace TestApp
{
	// Represents a single dependency: its name, whether it was found,
	// where it was found, and which search source matched.
	internal class ScannedDll
	{
		public string Name;
		public string FoundPath;
		public string Source;
		public bool Found;
	}

	// Aggregated result of scanning one target file.
	internal class DllScanResult
	{
		public string TargetPath;
		public string Architecture = "?";
		public bool IsDotNet;
		public List<ScannedDll> DirectImports = new List<ScannedDll>();
		public List<ScannedDll> DotNetReferences = new List<ScannedDll>();
		public string Note;
	}

	internal static class DllScanner
	{
		// Main entry point. Reads PE header for basic info, then resolves
		// native imports and (if managed) .NET assembly references.
		public static DllScanResult Scan(string filePath)
		{
			DllScanResult result = new DllScanResult();
			result.TargetPath = filePath;

			try
			{
				// Read the PE header to get architecture and .NET flag.
				PeHeaderReader.PeInfo pe = PeHeaderReader.GetBasicInfo(filePath);
				if (pe.Success)
				{
					result.Architecture = pe.MachineName;
					result.IsDotNet = pe.IsDotNet;
				}

				string gameDir = Path.GetDirectoryName(filePath);

				// ---- Native PE imports ----
				try
				{
					List<string> imports = PeImportReader.GetImportedDllNames(filePath);
					foreach (string imp in imports)
					{
						ScannedDll sd = ResolveNative(imp, gameDir);
						result.DirectImports.Add(sd);
					}
				}
				catch { }

				// ---- .NET assembly references ----
				if (result.IsDotNet)
				{
					try
					{
						List<string> refs = ClrAssemblyReader.GetReferences(filePath);
						foreach (string r in refs)
						{
							ScannedDll sd = ResolveDotNet(r, gameDir);
							result.DotNetReferences.Add(sd);
						}
					}
					catch { }
				}
			}
			catch (Exception ex)
			{
				result.Note = "Scan error: " + ex.Message;
			}

			return result;
		}

		// Resolves a native DLL name by checking, in order:
		// game folder, System32, SysWOW64, Sysnative.
		private static ScannedDll ResolveNative(string name, string gameDir)
		{
			ScannedDll sd = new ScannedDll();
			sd.Name = name;

			try
			{
				// 1. Next to the target EXE.
				if (!string.IsNullOrEmpty(gameDir))
				{
					string p = Path.Combine(gameDir, name);
					if (File.Exists(p)) { SetFound(sd, p, "Game folder"); return sd; }
				}

				// 2. System32.
				string sys32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
				string p2 = Path.Combine(sys32, name);
				if (File.Exists(p2)) { SetFound(sd, p2, "System32"); return sd; }

				// 3. SysWOW64 (32-bit DLLs on 64-bit Windows).
				string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
				string wow = Path.Combine(win, "SysWOW64");
				string p3 = Path.Combine(wow, name);
				if (File.Exists(p3)) { SetFound(sd, p3, "SysWOW64"); return sd; }

				// 4. Sysnative (for 32-bit processes querying 64-bit system dir).
				string sysNative = Path.Combine(win, "Sysnative");
				string p4 = Path.Combine(sysNative, name);
				if (File.Exists(p4)) { SetFound(sd, p4, "Sysnative"); return sd; }
			}
			catch { }

			// Not found anywhere.
			return sd;
		}

		// Resolves a .NET assembly reference by checking, in order:
		// game folder, GAC (all flavors), .NET Framework folders, Windows dir.
		private static ScannedDll ResolveDotNet(string name, string gameDir)
		{
			ScannedDll sd = new ScannedDll();
			sd.Name = name;

			try
			{
				// 1. Same folder as the target EXE.
				if (!string.IsNullOrEmpty(gameDir))
				{
					string p = Path.Combine(gameDir, name);
					if (File.Exists(p)) { SetFound(sd, p, "Game folder"); return sd; }
				}

				string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
				string nameNoExt = Path.GetFileNameWithoutExtension(name);

				// 2. Global Assembly Cache (GAC). Try the three common
				//    subfolders: GAC_MSIL, GAC_32, GAC_64.
				string[] gacFlavors = new string[] { "GAC_MSIL", "GAC_32", "GAC_64" };
				foreach (string flavor in gacFlavors)
				{
					string gacPath = Path.Combine(win, "Microsoft.NET", "assembly", flavor, nameNoExt);
					if (Directory.Exists(gacPath))
					{
						try
						{
							// GAC folders contain version subfolders; pick the first.
							string[] versionDirs = Directory.GetDirectories(gacPath);
							if (versionDirs.Length > 0)
							{
								SetFound(sd, versionDirs[0], flavor);
								return sd;
							}
						}
						catch { }
						// Fallback to the parent GAC folder if version enumeration fails.
						SetFound(sd, gacPath, flavor);
						return sd;
					}
				}

				// 3. .NET Framework folders (v4.0.30319 is the common runtime).
				string fw32 = Path.Combine(win, "Microsoft.NET", "Framework", "v4.0.30319");
				string direct32 = Path.Combine(fw32, name);
				if (File.Exists(direct32)) { SetFound(sd, direct32, ".NET Framework v4"); return sd; }

				string fw64 = Path.Combine(win, "Microsoft.NET", "Framework64", "v4.0.30319");
				string direct64 = Path.Combine(fw64, name);
				if (File.Exists(direct64)) { SetFound(sd, direct64, ".NET Framework64 v4"); return sd; }

				// 4. Windows folder directly (some assemblies live here).
				string winDirect = Path.Combine(win, name);
				if (File.Exists(winDirect)) { SetFound(sd, winDirect, "Windows folder"); return sd; }
			}
			catch { }

			// Not found anywhere.
			return sd;
		}

		// Marks a ScannedDll as found and records the resolved path and source.
		private static void SetFound(ScannedDll sd, string path, string source)
		{
			sd.Found = true;
			sd.FoundPath = path;
			sd.Source = source;
		}
	}
}