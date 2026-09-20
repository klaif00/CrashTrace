/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen 
 * 
 * 
 * Extra system-level checks that go beyond "is this DLL missing":
 * cross-referencing missing runtime DLLs against known Visual C++ /
 * DirectX redistributables, verifying the target file's own PE
 * checksum isn't corrupted, snapshotting overall system resources
 * at the moment of a crash, reading Windows Event Log entries that
 * relate to the crashed process, and detecting whether the Steam
 * client is running.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace TestApp
{
	internal static class RedistributableChecker
	{
		// Maps a VC runtime DLL base name to the redistributable that
		// installs it. Keyed by the name without extension, lowercased.
		private static readonly Dictionary<string, string> VcRuntimeMap = new Dictionary<string, string>
		{
			{ "msvcr80", "Microsoft Visual C++ 2005 Redistributable" },
			{ "msvcp80", "Microsoft Visual C++ 2005 Redistributable" },
			{ "msvcr90", "Microsoft Visual C++ 2008 Redistributable" },
			{ "msvcp90", "Microsoft Visual C++ 2008 Redistributable" },
			{ "msvcr100", "Microsoft Visual C++ 2010 Redistributable" },
			{ "msvcp100", "Microsoft Visual C++ 2010 Redistributable" },
			{ "msvcr110", "Microsoft Visual C++ 2012 Redistributable" },
			{ "msvcp110", "Microsoft Visual C++ 2012 Redistributable" },
			{ "msvcr120", "Microsoft Visual C++ 2013 Redistributable" },
			{ "msvcp120", "Microsoft Visual C++ 2013 Redistributable" },
			{ "vcruntime140", "Microsoft Visual C++ 2015-2022 Redistributable" },
			{ "vcruntime140_1", "Microsoft Visual C++ 2015-2022 Redistributable" },
			{ "msvcp140", "Microsoft Visual C++ 2015-2022 Redistributable" },
			{ "concrt140", "Microsoft Visual C++ 2015-2022 Redistributable" },
		};

		// Prefixes that belong to the DirectX legacy redistributables
		// (D3DX, XInput, XAudio, X3DAudio, XAPOFX).
		private static readonly string[] DirectXLegacyPrefixes = new string[]
		{
			"d3dx9_", "d3dx10_", "d3dx11_", "d3dcompiler_42", "d3dcompiler_43",
			"d3dcompiler_46", "d3dcompiler_47",
			"xinput1_1", "xinput1_2", "xinput1_3", "xinput1_4", "xinput9_1_0",
			"xaudio2_0", "xaudio2_1", "xaudio2_2", "xaudio2_3", "xaudio2_4",
			"xaudio2_5", "xaudio2_6", "xaudio2_7", "xaudio2_8", "xaudio2_9",
			"x3daudio1_0", "x3daudio1_1", "x3daudio1_2", "x3daudio1_3",
			"x3daudio1_4", "x3daudio1_5", "x3daudio1_6", "x3daudio1_7",
			"xapofx1_0", "xapofx1_1", "xapofx1_2", "xapofx1_3", "xapofx1_4", "xapofx1_5"
		};

		// Given a missing DLL name, returns a human-readable suggestion
		// about which redistributable to install, or null when we do not
		// recognize the DLL.
		public static string IdentifyRequiredRedistributable(string dllName)
		{
			string baseName = Path.GetFileNameWithoutExtension(dllName).ToLowerInvariant();

			// Strip trailing digits/underscores for VC runtime names
			// (msvcr100.dll -> msvcr100, but also handles msvcr100_clr0400 etc.)
			string vcPackage;
			if (VcRuntimeMap.TryGetValue(baseName, out vcPackage))
			{
				bool installed = IsVcRedistributableInstalled(vcPackage);
				return vcPackage + (installed
					? " (already appears installed - the game may need the specific x86/x64 build, or a repair install)"
					: " - not detected as installed. Install it from Microsoft's website.");
			}

			foreach (string prefix in DirectXLegacyPrefixes)
			{
				if (baseName.StartsWith(prefix))
				{
					return "DirectX End-User Runtime (legacy, June 2010) - install it from Microsoft's website. " +
						"Many older games also ship their own local copy of this DLL next to their EXE.";
				}
			}

			if (baseName == "d3d9" || baseName == "d3d11" || baseName == "d3d12" ||
				baseName == "dxgi" || baseName == "d2d1" || baseName == "dwrite")
			{
				return "DirectX runtime component (should ship with Windows - if missing, run System File Checker with 'sfc /scannow').";
			}

			if (baseName == "msvcp_win" || baseName == "ucrtbase" || baseName == "api-ms-win-crt-runtime-l1-1-0")
			{
				return "Universal C Runtime (UCRT) - normally part of Windows. If missing, run Windows Update or install the Visual C++ 2015-2022 Redistributable.";
			}

			if (baseName == "steam_api" || baseName == "steam_api64")
			{
				return "Steamworks API - if the game is a Steam title, launch it from inside the Steam client. If it's a standalone copy, copy steam_api.dll from a legitimate Steam installation next to the game EXE.";
			}

			if (baseName == "steamclient" || baseName == "steamclient64" || baseName == "tier0_s" || baseName == "vstdlib_s")
			{
				return "Steam client components - make sure the Steam client is running and the game is a legitimate copy.";
			}

			if (baseName.StartsWith("physx") || baseName.StartsWith("nxcooking") || baseName.StartsWith("nvtt"))
			{
				return "NVIDIA PhysX runtime - install the NVIDIA PhysX System Software from NVIDIA's website.";
			}

			if (baseName.StartsWith("openal"))
			{
				return "OpenAL audio runtime - install OpenAL Soft or the Creative OpenAL runtime.";
			}

			if (baseName.StartsWith("fmodex") || baseName.StartsWith("fmod"))
			{
				return "FMOD audio library - usually ships with the game. If missing, reinstall the game.";
			}

			if (baseName.StartsWith("mss") && (baseName == "mss32" || baseName == "mss64" || baseName == "mssmp3"))
			{
				return "Miles Sound System - usually ships with the game. If missing, reinstall the game or copy the DLL from the game's installation media.";
			}

			if (baseName.StartsWith("binkw"))
			{
				return "Bink Video runtime - usually ships with the game. If missing, reinstall the game.";
			}

			if (baseName == "libeay32" || baseName == "ssleay32" || baseName.StartsWith("libssl") || baseName.StartsWith("libcrypto"))
			{
				return "OpenSSL runtime library - usually ships with the game. If missing, reinstall the game.";
			}

			return null;
		}

		// Checks whether the given VC++ redistributable appears in the
		// Uninstall registry keys (both 64-bit and WOW6432Node views).
		private static bool IsVcRedistributableInstalled(string packageNamePart)
		{
			string keyword = packageNamePart.Replace("Microsoft ", "").Replace(" Redistributable", "");
			try
			{
				if (ScanUninstallKey(RegistryHive.LocalMachine, "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall", keyword)) return true;
				if (ScanUninstallKey(RegistryHive.LocalMachine, "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall", keyword)) return true;
			}
			catch
			{
			}
			return false;
		}

		// Scans one Uninstall subkey and returns true when a DisplayName
		// contains both the keyword and "Visual C++".
		private static bool ScanUninstallKey(RegistryHive hive, string subKeyPath, string keyword)
		{
			try
			{
				using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
				using (RegistryKey uninstallKey = baseKey.OpenSubKey(subKeyPath))
				{
					if (uninstallKey == null) return false;
					foreach (string subName in uninstallKey.GetSubKeyNames())
					{
						using (RegistryKey entry = uninstallKey.OpenSubKey(subName))
						{
							if (entry == null) continue;
							object displayName = entry.GetValue("DisplayName");
							if (displayName != null &&
								displayName.ToString().IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 &&
								displayName.ToString().IndexOf("Visual C++", StringComparison.OrdinalIgnoreCase) >= 0)
							{
								return true;
							}
						}
					}
				}
			}
			catch
			{
			}
			return false;
		}
	}

	internal static class PeChecksumValidator
	{
		// MapFileAndCheckSum reads the stored checksum from the PE header
		// and recomputes it. When the two differ, the file has been
		// modified after it was signed.
		[DllImport("imagehlp.dll", SetLastError = true, CharSet = CharSet.Auto)]
		private static extern uint MapFileAndCheckSum(string filename, out uint headerSum, out uint checkSum);

		// Returns a warning string when the checksum mismatches, or null
		// when the file is intact or the check could not run.
		public static string Validate(string filePath)
		{
			try
			{
				uint headerSum, checkSum;
				uint result = MapFileAndCheckSum(filePath, out headerSum, out checkSum);
				if (result != 0) return null;
				if (headerSum != 0 && headerSum != checkSum)
				{
					return "This file's stored checksum does not match its actual content " +
						"(header: 0x" + headerSum.ToString("X8") + ", computed: 0x" + checkSum.ToString("X8") +
						") - it may be corrupted or an incomplete download.";
				}
			}
			catch
			{
			}
			return null;
		}
	}

	internal static class SystemResourceInfo
	{
		// MEMORYSTATUSEX is used by GlobalMemoryStatusEx.
		[StructLayout(LayoutKind.Sequential)]
		private struct MEMORYSTATUSEX
		{
			public uint dwLength;
			public uint dwMemoryLoad;
			public ulong ullTotalPhys;
			public ulong ullAvailPhys;
			public ulong ullTotalPageFile;
			public ulong ullAvailPageFile;
			public ulong ullTotalVirtual;
			public ulong ullAvailVirtual;
			public ulong ullAvailExtendedVirtual;
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

		// Returns free physical memory in MB, or -1 on failure.
		public static long GetAvailableSystemMemoryMb()
		{
			try
			{
				MEMORYSTATUSEX status = new MEMORYSTATUSEX();
				status.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
				if (GlobalMemoryStatusEx(ref status))
				{
					return (long)(status.ullAvailPhys / 1024 / 1024);
				}
			}
			catch
			{
			}
			return -1;
		}

		// Returns free space on the drive that contains anyPathOnDrive,
		// in MB. Returns -1 on failure.
		public static long GetFreeDiskSpaceMb(string anyPathOnDrive)
		{
			try
			{
				string root = Path.GetPathRoot(anyPathOnDrive);
				DriveInfo drive = new DriveInfo(root);
				return drive.AvailableFreeSpace / 1024 / 1024;
			}
			catch
			{
				return -1;
			}
		}
	}

	internal static class WindowsEventLogReader
	{
		// Reads the Application event log and returns any entries whose
		// message mentions the target executable name, within the given
		// time window.
		public static List<WindowsEventEntry> GetRecentEntriesForProcess(
			string exeName, DateTime since)
		{
			List<WindowsEventEntry> entries = new List<WindowsEventEntry>();
			try
			{
				EventLog log = new EventLog("Application");
				foreach (EventLogEntry entry in log.Entries)
				{
					// Entries are oldest-first; once we go past the
					// "since" cutoff, everything after is too old.
					if (entry.TimeGenerated < since) break;
					string msg;
					try { msg = entry.Message; }
					catch { continue; }
					if (string.IsNullOrEmpty(msg)) continue;
					if (msg.IndexOf(exeName, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						WindowsEventEntry w = new WindowsEventEntry();
						w.TimeGenerated = entry.TimeGenerated;
						w.Source = entry.Source;
						w.Level = entry.EntryType.ToString();
						// Truncate long messages so the report stays readable.
						if (msg.Length > 400) msg = msg.Substring(0, 400) + "...";
						w.Message = msg;
						entries.Add(w);
						if (entries.Count >= 10) break;
					}
				}
				log.Close();
			}
			catch
			{
			}
			return entries;
		}
	}

	internal static class SteamClientChecker
	{
		// Detects whether the Steam client (or its web helper) is
		// currently running. Used to explain "steam_api.dll missing"
		// cases that are actually just "Steam is not started".
		public static bool IsSteamClientRunning()
		{
			try
			{
				Process[] procs = Process.GetProcessesByName("steam");
				if (procs != null && procs.Length > 0)
				{
					return true;
				}
				Process[] procs2 = Process.GetProcessesByName("steamwebhelper");
				if (procs2 != null && procs2.Length > 0)
				{
					return true;
				}
			}
			catch
			{
			}
			return false;
		}
	}
}