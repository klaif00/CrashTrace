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
 * Program Readiness & Protection Analyzer.
 *
 * Performs a complete static analysis of any Windows EXE or DLL (32-bit
 * or 64-bit) WITHOUT running it. Reports facts about:
 *   - System requirements (OS version, architecture, subsystem)
 *   - Required runtimes (VC++, .NET, DirectX) and whether they exist
 *   - Permissions (admin required, DEP, ASLR, CFG)
 *   - Protection (packers, anti-debug APIs, DRM indicators)
 *   - Language / compiler fingerprint
 *   - Program capabilities (graphics, audio, network, ...)
 *   - Risk indicators (unsigned, packed, suspicious location, ...)
 *
 * Nothing is inferred from the file's name or path. All findings are
 * derived from the file's own content and from the local system state.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Win32;

namespace TestApp
{
	// One finding row in the Readiness window: a status, a short item
	// name, what the program requires, and what this machine has.
	public class ReadinessItem
	{
		public string Status;       // OK / FAIL / WARN / INFO
		public string Item;
		public string Requirement;
		public string YourSystem;

		public ReadinessItem() { }

		public ReadinessItem(string status, string item, string requirement, string yourSystem)
		{
			Status = status;
			Item = item;
			Requirement = requirement;
			YourSystem = yourSystem;
		}
	}

	// Aggregated result of one readiness scan. Each list maps to one
	// group in the UI.
	public class ReadinessResult
	{
		public string TargetPath;
		public string Architecture;
		public string SubsystemVersion;
		public bool IsDotNet;
		public bool IsSigned;
		public DateTime ScannedAt;

		public List<ReadinessItem> System = new List<ReadinessItem>();
		public List<ReadinessItem> Runtimes = new List<ReadinessItem>();
		public List<ReadinessItem> Permissions = new List<ReadinessItem>();
		public List<ReadinessItem> Protection = new List<ReadinessItem>();
		public List<ReadinessItem> Fingerprint = new List<ReadinessItem>();
		public List<ReadinessItem> Capabilities = new List<ReadinessItem>();
		public List<ReadinessItem> Risks = new List<ReadinessItem>();
	}

	internal static class ReadinessAnalyzer
	{
		// CPU feature query. Used only for informational reporting.
		[DllImport("kernel32.dll")]
		private static extern bool IsProcessorFeaturePresent(uint feature);

		private const uint PF_XMMI_INSTRUCTIONS_AVAILABLE = 6;    // SSE
		private const uint PF_XMMI64_INSTRUCTIONS_AVAILABLE = 10; // SSE2

		// Main entry point. Reads the file, then runs every analysis
		// pass. Any individual pass failure is swallowed so a partial
		// result is still returned.
		public static ReadinessResult Analyze(string filePath)
		{
			ReadinessResult r = new ReadinessResult();
			r.TargetPath = filePath;
			r.ScannedAt = DateTime.Now;

			try
			{
				PeHeaderReader.PeInfo pe = PeHeaderReader.GetBasicInfo(filePath);
				r.Architecture = pe.Success ? pe.MachineName : "Unknown";
				r.SubsystemVersion = pe.Success ? (pe.SubsystemMajor + "." + pe.SubsystemMinor) : "n/a";
				r.IsDotNet = pe.Success && pe.IsDotNet;
				r.IsSigned = CheckSigned(filePath);

				// Gather the inputs that several analyzers share.
				List<string> imports = new List<string>();
				try { imports = PeImportReader.GetImportedDllNames(filePath); } catch { }

				List<string> strings = new List<string>();
				try { strings = StringExtractor.ExtractFromFile(filePath, 800, 6); } catch { }

				List<EntropyAnalyzer.SectionEntropy> sections = new List<EntropyAnalyzer.SectionEntropy>();
				try { sections = EntropyAnalyzer.AnalyzeFile(filePath); } catch { }

				AnalyzeSystem(r, pe);
				AnalyzeRuntimes(r, pe, imports, filePath);
				AnalyzePermissions(r, pe, filePath);
				AnalyzeProtection(r, strings, imports, sections);
				AnalyzeFingerprint(r, pe, strings, imports);
				AnalyzeCapabilities(r, imports);
				AnalyzeRisks(r, filePath, pe, sections, strings, imports);
			}
			catch { }

			return r;
		}

		// ------------------------------------------------------------------
		// System & Architecture
		// ------------------------------------------------------------------
		// Compares the OS version the PE declares against what this
		// machine actually reports.
		private static void AnalyzeSystem(ReadinessResult r, PeHeaderReader.PeInfo pe)
		{
			if (!pe.Success) return;

			// OS version required by the program (from its subsystem version)
			string requiredOs = MapSubsystemToOsName(pe.SubsystemMajor, pe.SubsystemMinor);
			string currentOs = GetCurrentOsName();

			bool osOk = IsCurrentOsAtLeast(pe.SubsystemMajor, pe.SubsystemMinor);
			r.System.Add(new ReadinessItem(
				osOk ? "OK" : "FAIL",
				"Operating System",
				requiredOs + " or newer",
				currentOs));

			// Architecture
			string peArch = pe.MachineName;
			string osArch = Environment.Is64BitOperatingSystem ? "x64 (64-bit)" : "x86 (32-bit)";
			bool archOk = true;
			string archNote = "";

			if (pe.Machine == 0x8664)
			{
				// x64 program
				if (!Environment.Is64BitOperatingSystem)
				{
					archOk = false;
					archNote = "This is a 64-bit program but Windows is 32-bit - it cannot run here.";
				}
			}
			else if (pe.Machine == 0x014c)
			{
				// x86 program - runs on both 32 and 64 bit Windows
				archNote = Environment.Is64BitOperatingSystem ? "(runs via WOW64)" : "";
			}
			else if (pe.Machine == 0xAA64 || pe.Machine == 0x01c4)
			{
				// ARM/ARM64
				archOk = false;
				archNote = "This is an ARM program. It requires an ARM-based Windows installation.";
			}

			r.System.Add(new ReadinessItem(
				archOk ? "OK" : "FAIL",
				"Architecture",
				peArch,
				osArch + (archNote.Length > 0 ? "  " + archNote : "")));

			// Subsystem (informational)
			string subsystemName = DescribeSubsystem(pe.Subsystem);
			r.System.Add(new ReadinessItem(
				"INFO",
				"Subsystem",
				subsystemName,
				subsystemName));

			// CPU features the OS can provide (informational)
			List<string> cpuFeatures = new List<string>();
			try
			{
				if (IsProcessorFeaturePresent(PF_XMMI_INSTRUCTIONS_AVAILABLE)) cpuFeatures.Add("SSE");
				if (IsProcessorFeaturePresent(PF_XMMI64_INSTRUCTIONS_AVAILABLE)) cpuFeatures.Add("SSE2");
			}
			catch { }

			if (cpuFeatures.Count > 0)
			{
				r.System.Add(new ReadinessItem(
					"INFO",
					"CPU features detected",
					"Various",
					string.Join(", ", cpuFeatures.ToArray())));
			}
		}

		// ------------------------------------------------------------------
		// Runtimes
		// ------------------------------------------------------------------
		// .NET, Visual C++ and DirectX checks: the three most common
		// reasons a program "just will not start" on a clean machine.
		private static void AnalyzeRuntimes(ReadinessResult r, PeHeaderReader.PeInfo pe, List<string> imports, string filePath)
		{
			bool is64BitPe = pe.Success && pe.Machine == 0x8664;

			// .NET Framework
			if (r.IsDotNet)
			{
				string runtimeVersion = GetDotNetRuntimeVersion(filePath);
				Version installed = GetInstalledDotNetVersion();

				if (!string.IsNullOrEmpty(runtimeVersion))
				{
					string reqText = runtimeVersion;
					string yourText = installed != null ? ("v" + installed.ToString()) : "Not detected";

					bool ok = installed != null;
					if (ok && installed.Major >= 4 && runtimeVersion.StartsWith("v4")) ok = true;
					else if (ok && installed.Major >= 2 && runtimeVersion.StartsWith("v2")) ok = true;

					r.Runtimes.Add(new ReadinessItem(
						ok ? "OK" : "FAIL",
						".NET Framework",
						reqText + " runtime",
						yourText));
				}
				else
				{
					r.Runtimes.Add(new ReadinessItem(
						installed != null ? "OK" : "WARN",
						".NET Framework",
						"Managed assembly",
						installed != null ? ("v" + installed.ToString()) : "Not detected"));
				}
			}

			// Visual C++ runtimes from imports
			HashSet<string> vcVersions = new HashSet<string>();
			foreach (string imp in imports)
			{
				string v = MapVcRuntimeFromDll(imp);
				if (v != null) vcVersions.Add(v);
			}

			foreach (string vcYear in vcVersions)
			{
				bool installed = IsVcRuntimeInstalled(vcYear, is64BitPe);
				string archText = is64BitPe ? "x64" : "x86";

				r.Runtimes.Add(new ReadinessItem(
					installed ? "OK" : "FAIL",
					"Visual C++ " + vcYear + " Runtime (" + archText + ")",
					"Installed",
					installed ? "Installed" : "Not detected"));
			}

			// DirectX family from imports
			HashSet<string> directXFamilies = new HashSet<string>();
			bool hasD3DX9 = false, hasD3DX10 = false, hasD3DX11 = false;
			bool hasD3D9 = false, hasD3D11 = false, hasD3D12 = false;
			bool hasDXGI = false, hasXInput = false;

			foreach (string imp in imports)
			{
				string lower = imp.ToLowerInvariant();
				if (lower.StartsWith("d3dx9_")) { hasD3DX9 = true; directXFamilies.Add("DirectX 9 End-User Runtime (June 2010)"); }
				else if (lower.StartsWith("d3dx10_")) { hasD3DX10 = true; directXFamilies.Add("DirectX SDK (d3dx10)"); }
				else if (lower.StartsWith("d3dx11_")) { hasD3DX11 = true; directXFamilies.Add("DirectX SDK (d3dx11)"); }
				else if (lower == "d3d9.dll") { hasD3D9 = true; }
				else if (lower == "d3d11.dll") { hasD3D11 = true; }
				else if (lower == "d3d12.dll") { hasD3D12 = true; }
				else if (lower == "dxgi.dll") { hasDXGI = true; }
				else if (lower.StartsWith("xinput")) { hasXInput = true; directXFamilies.Add("XInput (DirectX)"); }
			}

			foreach (string dx in directXFamilies)
			{
				bool present = true;
				string yourText = "Available on Windows";
				string status = "OK";

				// d3dx9_XX and d3dx10_XX / d3dx11_XX are external DLLs - check disk
				if (dx.IndexOf("d3dx9", StringComparison.OrdinalIgnoreCase) >= 0 ||
					dx.IndexOf("d3dx10", StringComparison.OrdinalIgnoreCase) >= 0 ||
					dx.IndexOf("d3dx11", StringComparison.OrdinalIgnoreCase) >= 0 ||
					dx.IndexOf("XInput", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					present = false;
					foreach (string imp in imports)
					{
						string lower = imp.ToLowerInvariant();
						if ((dx.IndexOf("d3dx9", StringComparison.OrdinalIgnoreCase) >= 0 && lower.StartsWith("d3dx9_")) ||
							(dx.IndexOf("d3dx10", StringComparison.OrdinalIgnoreCase) >= 0 && lower.StartsWith("d3dx10_")) ||
							(dx.IndexOf("d3dx11", StringComparison.OrdinalIgnoreCase) >= 0 && lower.StartsWith("d3dx11_")) ||
							(dx.IndexOf("XInput", StringComparison.OrdinalIgnoreCase) >= 0 && lower.StartsWith("xinput")))
						{
							if (FindFileInSystemFolders(imp, is64BitPe) != null)
							{
								present = true;
								break;
							}
						}
					}
					yourText = present ? "File present on this system" : "File not found on disk";
					status = present ? "OK" : "FAIL";
				}

				r.Runtimes.Add(new ReadinessItem(status, dx, "Available", yourText));
			}

			// Built-in DirectX version note (informational)
			if (hasD3D9 || hasD3D11 || hasD3D12 || hasDXGI)
			{
				List<string> list = new List<string>();
				if (hasD3D9) list.Add("D3D9");
				if (hasD3D11) list.Add("D3D11");
				if (hasD3D12) list.Add("D3D12");
				if (hasDXGI) list.Add("DXGI");
				r.Runtimes.Add(new ReadinessItem(
					"INFO",
					"DirectX graphics APIs",
					string.Join(", ", list.ToArray()),
					"Built into Windows"));
			}
		}

		// ------------------------------------------------------------------
		// Permissions
		// ------------------------------------------------------------------
		// Uses the manifest exec level and the DllCharacteristics flags
		// to describe what the program asks from Windows.
		private static void AnalyzePermissions(ReadinessResult r, PeHeaderReader.PeInfo pe, string filePath)
		{
			if (!pe.Success) return;

			// Admin level from the manifest
			string execLevel = GetManifestExecLevel(filePath);
			string execYourSystem = IsRunningAsAdmin() ? "Running as Administrator" : "Running as standard user";

			if (execLevel == "requireAdministrator")
			{
				r.Permissions.Add(new ReadinessItem(
					IsRunningAsAdmin() ? "OK" : "WARN",
					"Administrator privileges",
					"Required (requireAdministrator)",
					execYourSystem));
			}
			else if (execLevel == "highestAvailable")
			{
				r.Permissions.Add(new ReadinessItem(
					"INFO",
					"Administrator privileges",
					"Highest available (runs elevated if possible)",
					execYourSystem));
			}
			else if (!string.IsNullOrEmpty(execLevel))
			{
				r.Permissions.Add(new ReadinessItem(
					"INFO",
					"Administrator privileges",
					"Not required (" + execLevel + ")",
					execYourSystem));
			}
			else
			{
				r.Permissions.Add(new ReadinessItem(
					"INFO",
					"Administrator privileges",
					"No manifest declared",
					execYourSystem));
			}

			// DEP / ASLR / CFG from DLL characteristics
			ushort dllChars = pe.DllCharacteristics;
			r.Permissions.Add(new ReadinessItem(
				(dllChars & 0x0100) != 0 ? "OK" : "WARN",
				"Data Execution Prevention (DEP)",
				(dllChars & 0x0100) != 0 ? "Compatible (NX_COMPAT)" : "Not declared",
				"Supported by all modern Windows versions"));

			r.Permissions.Add(new ReadinessItem(
				(dllChars & 0x0040) != 0 ? "OK" : "INFO",
				"Address Space Layout Randomization (ASLR)",
				(dllChars & 0x0040) != 0 ? "Compatible (DYNAMIC_BASE)" : "Not declared",
				"Supported by all modern Windows versions"));

			if ((dllChars & 0x4000) != 0)
			{
				r.Permissions.Add(new ReadinessItem(
					"INFO",
					"Control Flow Guard (CFG)",
					"Enabled",
					"Supported on Windows 8.1+"));
			}

			if ((dllChars & 0x0020) != 0)
			{
				r.Permissions.Add(new ReadinessItem(
					"INFO",
					"High Entropy VA",
					"Enabled",
					"Supported on 64-bit Windows"));
			}
		}

		// ------------------------------------------------------------------
		// Protection
		// ------------------------------------------------------------------
		// Detects packers, anti-debug APIs, DRM strings and anti-VM
		// strings. Informational: none of these are errors by themselves.
		private static void AnalyzeProtection(ReadinessResult r, List<string> strings, List<string> imports, List<EntropyAnalyzer.SectionEntropy> sections)
		{
			// 1. Packer / protector from section names
			List<string> packerHints = new List<string>();
			foreach (var s in sections)
			{
				string n = s.Name.ToLowerInvariant();
				if (n.Contains("upx")) packerHints.Add("UPX (section " + s.Name + ")");
				else if (n.Contains(".aspack") || n.Contains(".adata")) packerHints.Add("ASPack (section " + s.Name + ")");
				else if (n.Contains("themida")) packerHints.Add("Themida (section " + s.Name + ")");
				else if (n.Contains(".vmp")) packerHints.Add("VMProtect (section " + s.Name + ")");
				else if (n.Contains(".mpress")) packerHints.Add("MPRESS (section " + s.Name + ")");
				else if (n.Contains(".enigma")) packerHints.Add("Enigma (section " + s.Name + ")");
				else if (n.Contains(".petite")) packerHints.Add("Petite (section " + s.Name + ")");
			}

			// High entropy sections also indicate packing
			foreach (var s in sections)
			{
				if (s.Entropy >= 7.5 && s.Size > 0x1000)
				{
					string hint = "High entropy section: " + s.Name + " (entropy " + s.Entropy.ToString("0.000") + ")";
					if (!packerHints.Contains(hint)) packerHints.Add(hint);
				}
			}

			if (packerHints.Count > 0)
			{
				foreach (string h in packerHints)
				{
					r.Protection.Add(new ReadinessItem("WARN", "Packer / protector", h, "Detected in file"));
				}
			}
			else
			{
				r.Protection.Add(new ReadinessItem("OK", "Packer / protector", "None detected", "Not detected"));
			}

			// 2. Anti-debug APIs from imports
			string[] antiDebugApis = new string[]
			{
				"IsDebuggerPresent",
				"CheckRemoteDebuggerPresent",
				"NtQueryInformationProcess",
				"OutputDebugString",
				"DebugActiveProcess",
				"NtSetInformationThread",
				"NtClose",
				"NtQueryObject"
			};
			List<string> foundAntiDebug = new List<string>();
			foreach (string imp in imports)
			{
				foreach (string api in antiDebugApis)
				{
					if (imp.IndexOf(api, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						if (!foundAntiDebug.Contains(imp)) foundAntiDebug.Add(imp);
					}
				}
			}

			if (foundAntiDebug.Count > 0)
			{
				r.Protection.Add(new ReadinessItem(
					"INFO",
					"Debugger-related APIs",
					string.Join(", ", foundAntiDebug.ToArray()),
					"Found in imports"));
			}

			// 3. DRM / protection indicators from strings
			string[] drmKeywords = new string[]
			{
				"denuvo", "arxan", "vmprotect", "themida", "enigma protector",
				"securom", "safedisc", "starforce", "steam_api", "steamclient",
				"steamstub", "uplay", "orbit", "origin sdk", "eac ", "easyanticheat",
				"battleye", "beservice", "vanguard", "xigncode", "nprotect",
				"gameguard", "punkbuster", "fairfight", "equ8", "faceit"
			};

			List<string> drmFound = new List<string>();
			foreach (string s in strings)
			{
				string lower = s.ToLowerInvariant();
				foreach (string kw in drmKeywords)
				{
					if (lower.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						if (!drmFound.Contains(s)) drmFound.Add(s);
						break;
					}
				}
			}

			if (drmFound.Count > 0)
			{
				int limit = Math.Min(drmFound.Count, 8);
				for (int i = 0; i < limit; i++)
				{
					r.Protection.Add(new ReadinessItem(
						"INFO",
						"DRM-related string",
						"\"" + drmFound[i] + "\"",
						"Found in file"));
				}
			}

			// 4. Anti-VM indicators from strings
			string[] vmKeywords = new string[]
			{
				"vmware", "virtualbox", "vbox", "qemu", "sandboxie",
				"xenserver", "parallels", "virtual machine"
			};
			List<string> vmFound = new List<string>();
			foreach (string s in strings)
			{
				string lower = s.ToLowerInvariant();
				foreach (string kw in vmKeywords)
				{
					if (lower.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						if (!vmFound.Contains(s)) vmFound.Add(s);
						break;
					}
				}
			}

			if (vmFound.Count > 0)
			{
				int limit = Math.Min(vmFound.Count, 5);
				for (int i = 0; i < limit; i++)
				{
					r.Protection.Add(new ReadinessItem(
						"INFO",
						"VM-related string",
						"\"" + vmFound[i] + "\"",
						"Found in file"));
				}
			}
		}

		// ------------------------------------------------------------------
		// Fingerprint
		// ------------------------------------------------------------------
		// Guesses the compiler / SDK family from strings in the file.
		private static void AnalyzeFingerprint(ReadinessResult r, PeHeaderReader.PeInfo pe, List<string> strings, List<string> imports)
		{
			r.Fingerprint.Add(new ReadinessItem(
				"INFO",
				"Runtime type",
				pe.Success && pe.IsDotNet ? "Managed (.NET)" : "Native",
				pe.Success && pe.IsDotNet ? "Managed (.NET)" : "Native"));

			// Compiler hints from strings
			List<string> hints = new List<string>();
			foreach (string s in strings)
			{
				string lower = s.ToLowerInvariant();
				if (lower.Contains("microsoft visual c++") && !hints.Contains("Microsoft Visual C++"))
					hints.Add("Microsoft Visual C++");
				else if (lower.Contains("delphi") && !hints.Contains("Delphi"))
					hints.Add("Delphi");
				else if (lower.Contains("embarcadero") && !hints.Contains("Embarcadero (Delphi/C++ Builder)"))
					hints.Add("Embarcadero (Delphi/C++ Builder)");
				else if (lower.Contains("rustc") || lower.Contains("rust_backtrace") || lower.Contains("cargo"))
				{
					if (!hints.Contains("Rust")) hints.Add("Rust");
				}
				else if (lower.Contains("go build id") || lower.Contains("golang"))
				{
					if (!hints.Contains("Go (Golang)")) hints.Add("Go (Golang)");
				}
				else if (lower.Contains("qt5core") || lower.Contains("qt6core"))
				{
					if (!hints.Contains("Qt")) hints.Add("Qt");
				}
				else if (lower.Contains(".net framework"))
				{
					if (!hints.Contains(".NET Framework")) hints.Add(".NET Framework");
				}
			}

			if (hints.Count > 0)
			{
				r.Fingerprint.Add(new ReadinessItem(
					"INFO",
					"Compiler / SDK hints",
					string.Join(", ", hints.ToArray()),
					"Found in file"));
			}

			// Managed target framework if .NET
			if (hints.Count > 0)
			{
				r.Fingerprint.Add(new ReadinessItem(
				"INFO",
				"Compiler / SDK hints",
				string.Join(", ", hints.ToArray()),
				"Found in file"));
			}
		}

		// ------------------------------------------------------------------
		// Capabilities
		// ------------------------------------------------------------------
		// Maps import DLL names to broad capability categories so the
		// user gets a one-line summary of what the program can do.
		private static void AnalyzeCapabilities(ReadinessResult r, List<string> imports)
		{
			HashSet<string> caps = new HashSet<string>();

			bool hasGraphics = false;
			bool hasAudio = false;
			bool hasNetwork = false;
			bool hasCrypto = false;
			bool hasRegistry = false;
			bool hasCom = false;
			bool hasDirectShow = false;

			foreach (string imp in imports)
			{
				string lower = imp.ToLowerInvariant();

				if (lower.StartsWith("d3d9") || lower.StartsWith("d3d11") || lower.StartsWith("d3d12") ||
					lower.StartsWith("d3dx9") || lower.StartsWith("d3dx10") || lower.StartsWith("d3dx11") ||
					lower.StartsWith("opengl32") || lower.StartsWith("vulkan") || lower.StartsWith("dxgi"))
					hasGraphics = true;

				if (lower.StartsWith("dsound") || lower.StartsWith("xaudio") || lower == "winmm.dll" ||
					lower.StartsWith("mss32") || lower.StartsWith("mss64") || lower.StartsWith("fmod"))
					hasAudio = true;

				if (lower.StartsWith("ws2_32") || lower.StartsWith("wininet") || lower.StartsWith("winhttp") ||
					lower.StartsWith("iphlpapi"))
					hasNetwork = true;

				if (lower.StartsWith("bcrypt") || lower.StartsWith("ncrypt") || lower.StartsWith("crypt32"))
					hasCrypto = true;

				if (lower.StartsWith("advapi32")) hasRegistry = true;
				if (lower.StartsWith("ole32") || lower.StartsWith("oleaut32") || lower.StartsWith("combase"))
					hasCom = true;
				if (lower.StartsWith("quartz") || lower.StartsWith("devenum") || lower.StartsWith("strmiids"))
					hasDirectShow = true;
			}

			if (hasGraphics) caps.Add("Graphics / 3D rendering");
			if (hasAudio) caps.Add("Audio playback / processing");
			if (hasNetwork) caps.Add("Network communication");
			if (hasCrypto) caps.Add("Encryption / cryptography");
			if (hasRegistry) caps.Add("Windows Registry access");
			if (hasCom) caps.Add("COM / ActiveX usage");
			if (hasDirectShow) caps.Add("DirectShow media");

			foreach (string cap in caps)
			{
				r.Capabilities.Add(new ReadinessItem("INFO", "Capability", cap, "Present in imports"));
			}

			if (caps.Count == 0)
			{
				r.Capabilities.Add(new ReadinessItem("INFO", "Capability", "No specific APIs detected", ""));
			}
		}

		// ------------------------------------------------------------------
		// Risks
		// ------------------------------------------------------------------
		// Signature, location, size, packing, low import count, and PE
		// checksum. All entries here are warnings, not errors.
		private static void AnalyzeRisks(ReadinessResult r, string filePath, PeHeaderReader.PeInfo pe, List<EntropyAnalyzer.SectionEntropy> sections, List<string> strings, List<string> imports)
		{
			// Signature
			if (!r.IsSigned)
			{
				r.Risks.Add(new ReadinessItem(
					"WARN",
					"Digital signature",
					"Signed by a trusted publisher",
					"Not digitally signed"));
			}
			else
			{
				r.Risks.Add(new ReadinessItem(
					"OK",
					"Digital signature",
					"Signed by a trusted publisher",
					"Signed"));
			}

			// Location
			try
			{
				string lower = filePath.ToLowerInvariant();
				if (lower.Contains("\\downloads\\"))
				{
					r.Risks.Add(new ReadinessItem("WARN", "Location", "Installed folder",
						"File is in the Downloads folder"));
				}
				if (lower.Contains("\\temp\\") || lower.Contains("\\tmp\\"))
				{
					r.Risks.Add(new ReadinessItem("WARN", "Location", "Installed folder",
						"File is in a temporary folder"));
				}
			}
			catch { }

			// File size
			try
			{
				FileInfo fi = new FileInfo(filePath);
				if (fi.Length < 4096)
				{
					r.Risks.Add(new ReadinessItem("WARN", "File size", "Normal executable",
						fi.Length + " bytes (very small)"));
				}
			}
			catch { }

			// Packed (already detected in Protection, but flag as a risk)
			bool packed = false;
			foreach (var s in sections)
			{
				string n = s.Name.ToLowerInvariant();
				if (n.Contains("upx") || n.Contains("aspack") || n.Contains("themida") ||
					n.Contains("vmp") || n.Contains("mpress") || n.Contains("petite") || n.Contains("enigma"))
				{
					packed = true;
					break;
				}
			}

			if (packed)
			{
				r.Risks.Add(new ReadinessItem(
					"WARN",
					"File contents",
					"Unpacked executable",
					"Executable appears packed / protected"));
			}

			// Very few imports (often a sign of packing)
			if (imports.Count > 0 && imports.Count < 5 && pe.Success && !pe.IsDotNet)
			{
				r.Risks.Add(new ReadinessItem(
					"WARN",
					"Import table",
					"Normal number of imports",
					imports.Count + " imports only (unusual for a normal program)"));
			}

			// Missing PE checksum
			try
			{
				string checksumWarn = PeChecksumValidator.Validate(filePath);
				if (!string.IsNullOrEmpty(checksumWarn))
				{
					r.Risks.Add(new ReadinessItem("WARN", "PE checksum", "Valid checksum",
						"Checksum mismatch - file may be modified or corrupted"));
				}
			}
			catch { }
		}

		// ------------------------------------------------------------------
		// Helpers
		// ------------------------------------------------------------------
		// Wraps the Win32 signature check. Returns false when the file
		// is unsigned or the signature cannot be verified.
		private static bool CheckSigned(string filePath)
		{
			try
			{
				X509Certificate cert = X509Certificate.CreateFromSignedFile(filePath);
				return cert != null;
			}
			catch
			{
				return false;
			}
		}

		// Reads the CLR image version string from a managed assembly.
		private static string GetDotNetRuntimeVersion(string filePath)
		{
			try
			{
				Assembly asm = Assembly.ReflectionOnlyLoadFrom(filePath);
				return asm.ImageRuntimeVersion;
			}
			catch
			{
				return null;
			}
		}

		// Reads the installed .NET Framework version from the registry.
		// Looks at the "Full" and "Client" v4 keys first, then older
		// versions, and returns the highest one found.
		private static Version GetInstalledDotNetVersion()
		{
			try
			{
				string[] paths = new string[]
				{
					@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full",
					@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Client",
					@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v3.5",
					@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v3.0",
					@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v2.0.50727"
				};

				Version best = null;
				foreach (string p in paths)
				{
					try
					{
						using (RegistryKey k = Registry.LocalMachine.OpenSubKey(p))
						{
							if (k == null) continue;
							object v = k.GetValue("Version");
							if (v == null) continue;
							Version ver;
							if (!Version.TryParse(v.ToString(), out ver)) continue;
							if (best == null || ver > best) best = ver;
						}
					}
					catch { }
				}
				return best;
			}
			catch { return null; }
		}

		// Maps a VC++ runtime DLL name (like "msvcr120.dll") to a year
		// label used in the UI ("2013").
		private static string MapVcRuntimeFromDll(string dllName)
		{
			if (string.IsNullOrEmpty(dllName)) return null;
			string lower = dllName.ToLowerInvariant();
			if (lower.StartsWith("msvcr80") || lower.StartsWith("msvcp80")) return "2005";
			if (lower.StartsWith("msvcr90") || lower.StartsWith("msvcp90")) return "2008";
			if (lower.StartsWith("msvcr100") || lower.StartsWith("msvcp100")) return "2010";
			if (lower.StartsWith("msvcr110") || lower.StartsWith("msvcp110")) return "2012";
			if (lower.StartsWith("msvcr120") || lower.StartsWith("msvcp120")) return "2013";
			if (lower.StartsWith("vcruntime140") || lower.StartsWith("msvcp140")) return "2015-2022";
			return null;
		}

		// Checks whether the VC++ runtime for the given year is
		// installed, by scanning Uninstall registry entries for the
		// matching DisplayName. The bitness of the PE decides whether
		// we look at the 64-bit or 32-bit registry view.
		private static bool IsVcRuntimeInstalled(string year, bool is64BitPe)
		{
			try
			{
				RegistryView view = is64BitPe ? RegistryView.Registry64 : RegistryView.Registry32;
				string[] searchTerms;
				if (year == "2015-2022")
				{
					searchTerms = new string[] { "Visual C++ 2015", "Visual C++ 2017", "Visual C++ 2019", "Visual C++ 2022" };
				}
				else
				{
					searchTerms = new string[] { "Visual C++ " + year };
				}

				using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
				using (RegistryKey uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
				{
					if (uninstall == null) return false;
					foreach (string sub in uninstall.GetSubKeyNames())
					{
						try
						{
							using (RegistryKey entry = uninstall.OpenSubKey(sub))
							{
								if (entry == null) continue;
								object dn = entry.GetValue("DisplayName");
								if (dn == null) continue;
								string name = dn.ToString();
								foreach (string st in searchTerms)
								{
									if (name.IndexOf(st, StringComparison.OrdinalIgnoreCase) >= 0)
										return true;
								}
							}
						}
						catch { }
					}
				}
			}
			catch { }
			return false;
		}

		// Looks for a DLL in System32 and SysWOW64, preferring the
		// folder that matches the PE's bitness.
		private static string FindFileInSystemFolders(string fileName, bool is64BitPe)
		{
			try
			{
				string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

				// Same-arch folder first
				string primary = is64BitPe ? Path.Combine(win, "System32") : Path.Combine(win, "SysWOW64");
				string alt = is64BitPe ? Path.Combine(win, "SysWOW64") : Path.Combine(win, "System32");

				string p = Path.Combine(primary, fileName);
				if (File.Exists(p)) return p;
				p = Path.Combine(alt, fileName);
				if (File.Exists(p)) return p;
			}
			catch { }
			return null;
		}

		// Maps a PE subsystem version number to a friendly Windows name.
		private static string MapSubsystemToOsName(ushort major, ushort minor)
		{
			if (major >= 10) return "Windows 10 / 11";
			if (major == 6 && minor >= 3) return "Windows 8.1";
			if (major == 6 && minor >= 2) return "Windows 8";
			if (major == 6 && minor >= 1) return "Windows 7";
			if (major == 6 && minor >= 0) return "Windows Vista";
			if (major == 5 && minor >= 2) return "Windows XP x64 / Server 2003";
			if (major == 5 && minor >= 1) return "Windows XP";
			if (major == 5 && minor >= 0) return "Windows 2000";
			return "Windows " + major + "." + minor;
		}

		// Maps the PE subsystem number to a short name.
		private static string DescribeSubsystem(ushort subsystem)
		{
			switch (subsystem)
			{
				case 1: return "Native";
				case 2: return "Windows GUI";
				case 3: return "Windows Console";
				case 7: return "POSIX Console";
				case 9: return "Windows CE GUI";
				case 10: return "EFI Application";
				case 14: return "Xbox";
				case 16: return "Windows Boot Application";
				default: return "Unknown (" + subsystem + ")";
			}
		}

		// Reads the friendly OS name from the registry, falling back to
		// Environment.OSVersion when the registry is not accessible.
		private static string GetCurrentOsName()
		{
			try
			{
				using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
				{
					if (key != null)
					{
						object product = key.GetValue("ProductName");
						object display = key.GetValue("DisplayVersion");
						object build = key.GetValue("CurrentBuild");

						StringBuilder sb = new StringBuilder();
						if (product != null) sb.Append(product.ToString());
						else sb.Append("Windows");
						if (display != null && display.ToString().Length > 0)
							sb.Append(" (").Append(display.ToString()).Append(")");
						else if (build != null)
							sb.Append(" (build ").Append(build.ToString()).Append(")");
						return sb.ToString();
					}
				}
			}
			catch { }
			return Environment.OSVersion.VersionString;
		}

		// Compares the running OS version against a required (major,
		// minor) pair. Uses registry values to work around the
		// compatibility shim that reports 6.2 on Windows 8.1+ unless a
		// manifest is present.
		private static bool IsCurrentOsAtLeast(ushort requiredMajor, ushort requiredMinor)
		{
			try
			{
				Version cur = Environment.OSVersion.Version;
				// On Windows 8.1+ without a manifest, Environment.OSVersion may
				// report 6.2. Use the registry to get the real build.
				try
				{
					using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
					{
						if (key != null)
						{
							object major = key.GetValue("CurrentMajorVersionNumber");
							object minor = key.GetValue("CurrentMinorVersionNumber");
							if (major != null && minor != null)
							{
								int mj = Convert.ToInt32(major);
								int mn = Convert.ToInt32(minor);
								if (mj > cur.Major || (mj == cur.Major && mn > cur.Minor))
									cur = new Version(mj, mn);
							}
						}
					}
				}
				catch { }

				if (cur.Major > requiredMajor) return true;
				if (cur.Major < requiredMajor) return false;
				return cur.Minor >= requiredMinor;
			}
			catch
			{
				return true;
			}
		}

		// Reads the manifest and extracts the requestedExecutionLevel
		// value. Returns null when no manifest or no level is found.
		private static string GetManifestExecLevel(string filePath)
		{
			try
			{
				string manifest = ManifestReader.ReadManifest(filePath);
				if (string.IsNullOrEmpty(manifest)) return null;

				int idx = manifest.IndexOf("requestedExecutionLevel", StringComparison.OrdinalIgnoreCase);
				if (idx < 0) return null;

				int levelIdx = manifest.IndexOf("level", idx, StringComparison.OrdinalIgnoreCase);
				if (levelIdx < 0) return null;

				int eq = manifest.IndexOf('=', levelIdx);
				if (eq < 0) return null;

				// Find the opening quote
				int q1 = manifest.IndexOfAny(new char[] { '"', '\'' }, eq);
				if (q1 < 0) return null;
				char quoteChar = manifest[q1];

				int q2 = manifest.IndexOf(quoteChar, q1 + 1);
				if (q2 < 0) return null;

				return manifest.Substring(q1 + 1, q2 - q1 - 1);
			}
			catch
			{
				return null;
			}
		}

		// Admin check used by the permissions analysis.
		private static bool IsRunningAsAdmin()
		{
			try
			{
				var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
				var principal = new System.Security.Principal.WindowsPrincipal(identity);
				return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
			}
			catch
			{
				return false;
			}
		}
	}
}