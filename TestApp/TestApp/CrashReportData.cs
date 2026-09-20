/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
* Developed by Limen
*/
using System;
using System.Collections.Generic;

namespace TestApp
{
	// One entry from the Windows Event Log, captured around the crash time.
	internal class WindowsEventEntry
	{
		public DateTime TimeGenerated;
		public string Source;
		public string Level;
		public string Message;
	}

	// The central data model of the tool. Every analyzer fills part of this
	// object, and every renderer reads from it. Fields are plain public
	// members on purpose: the class is a transport container, not a service.
	internal class CrashReportData
	{
		// --- Target identity ---
		public string TargetPath;
		public DateTime StartedAt;
		public DateTime FinishedAt;
		public string Architecture;
		public string SubsystemVersion;
		public string TargetProcessor;
		public string Sha256Hash;

		// --- PE header and structure ---
		public uint EntryPointRva;
		public bool IsDotNetAssembly;
		public bool HasTlsCallbacks;
		public bool HasResources;
		public bool HasRelocations;
		public bool HasDebugInfo;
		public bool HasDelayImports;
		public bool IsLikelyPacked;
		public string PackedHint;
		public string DllCharacteristicsFlags;
		public int NumberOfSections;
		public List<string> SectionInfo = new List<string>();
		public List<string> DelayLoadedDlls = new List<string>();
		public List<string> ExportedFunctions = new List<string>();

		// --- Crash outcome ---
		public string ResultLabel;
		public string StatusCodeHex;
		public string Meaning;
		public string AddressHex;
		public string ModuleName;
		public string ModuleOffsetHex;
		public long SecondsUntilCrash = -1;

		// --- Access violation details ---
		public bool HaveAccessInfo;
		public string AccessKind;
		public string AccessedAddressHex;
		public bool LikelyNullPointer;
		public bool LikelyWriteToReadOnly;
		public bool LikelyDepViolation;

		// --- Resource counters at the moment of the crash ---
		public long CrashWorkingSetMb = -1;
		public int CrashHandleCount = -1;
		public int CrashGdiObjects = -1;
		public int CrashUserObjects = -1;

		// --- Call stack and symbol resolution ---
		public List<string> CallStack = new List<string>();
		public string SymbolDiagnosticNote;
		public string SymbolPathUsed;
		public bool SymbolsInitialized;
		public bool SymbolsWereResolved;

		// --- Dependency analysis ---
		public List<string> ConfirmedMissingDlls = new List<string>();
		public List<string> ProbablyFineDlls = new List<string>();
		public List<string> IndirectMissingDlls = new List<string>();
		public List<string> RedistributableSuggestions = new List<string>();

		// --- Environment snapshot ---
		public string PeChecksumWarning;
		public long AvailableSystemMemoryMb = -1;
		public long FreeDiskSpaceMb = -1;

		// --- Security / tampering signals ---
		public List<string> SuspiciousModules = new List<string>();
		public List<string> AntiCheatModules = new List<string>();

		// --- Shims, signature, artifacts ---
		public bool HasCompatibilityShim;
		public string CompatibilityFlags;
		public string SignatureInfo;
		public string ScreenshotFileName;
		public string MiniDumpFileName;
		public bool InDownloadsFolder;

		// --- Hang tracking ---
		public bool HangDetected;
		public DateTime HangStartTime;
		public int HangCount;

		// --- Related system events ---
		public List<WindowsEventEntry> RelatedSystemEvents = new List<WindowsEventEntry>();

		// --- Recommendations and known issues ---
		public List<string> Recommendations = new List<string>();
		public List<string> KnownIssueMatches = new List<string>();

		// --- Steam integration state ---
		public bool SteamApiLoaded;
		public bool SteamClientRunning;

		// --- Crash region and memory dump ---
		public string CrashRegionState;
		public string CrashRegionProtect;
		public string CrashRegionType;
		public string CrashRegionDescription;
		public long CrashRegionBase = 0;
		public long CrashRegionSize = 0;
		public string CrashSiteHexDump;
		public string CrashSitePattern;
		public string CrashSiteMemoryFilePath;

		// --- Faulting instruction (from the minimal disassembler) ---
		public string FaultingInstructionText;
		public string FaultingInstructionNote;
		public int FaultingInstructionLength = -1;

		// --- Extra evidence collected during the run ---
		public List<string> SectionEntropyLines = new List<string>();
		public List<string> CompanionLogContents = new List<string>();
		public List<string> ThreadSnapshotLines = new List<string>();
		public List<string> InterestingStrings = new List<string>();

		// --- Registers, stack dump and process context ---
		public List<string> RegisterLines = new List<string>();
		public string StackHexDump;
		public long StackRegionBase;
		public long StackRegionSize;
		public string DetectedEngine;
		public List<string> ApiHooksDetected = new List<string>();
		public List<string> ProcessEnvironment = new List<string>();
		public List<string> WerReportLines = new List<string>();
		public string ManifestContent;

		// --- Full raw log, used by analyzers that scan the whole session ---
		public List<string> AllLogLines = new List<string>();
	}
}