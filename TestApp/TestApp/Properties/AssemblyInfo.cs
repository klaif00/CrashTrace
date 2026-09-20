/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */
#region Using directives

using System;
using System.Reflection;
using System.Runtime.InteropServices;

#endregion

// ---------------------------------------------------------------------------
// Assembly metadata.
//
// These attributes are baked into the PE header of the executable. Windows
// (Explorer, Task Manager, UAC, Add/Remove Programs) and various tools
// (antivirus, installers, PE viewers) read them when they inspect the
// binary. None of them affect runtime behaviour.
// ---------------------------------------------------------------------------

// Short name Windows shows in Explorer and in the UAC elevation prompt.
[assembly: AssemblyTitle("CrashTrace")]

// Long free-form description. Left empty; nobody reads this field in practice.
[assembly: AssemblyDescription("")]

// Build configuration label (Debug / Release). Left empty because the
// Configuration dropdown in the project already tracks this.
[assembly: AssemblyConfiguration("")]

// Publisher. Not a registered company, so left blank.
[assembly: AssemblyCompany("")]

// Product name. Same as AssemblyTitle for now, but the two are kept
// separate because installers sometimes want a longer product name than
// what fits in a taskbar tooltip.
[assembly: AssemblyProduct("CrashTrace")]

// Copyright notice, following the (c) YEAR HOLDER convention.
[assembly: AssemblyCopyright("Copyright 2026")]

// Trademark. Not applicable.
[assembly: AssemblyTrademark("")]

// Culture identifier. Invariant - the tool ships as a single English
// binary with no satellite resource assemblies.
[assembly: AssemblyCulture("")]

// ---------------------------------------------------------------------------
// COM visibility.
//
// Hidden by default, which is the correct default for a standalone desktop
// app. If a specific type ever needs to be callable from COM, mark that
// type [ComVisible(true)] individually instead of flipping this.
// ---------------------------------------------------------------------------
[assembly: ComVisible(false)]

// ---------------------------------------------------------------------------
// Version number: Major.Minor.Build.Revision
//
// Major and Minor are pinned at 1.0 so the public version stays stable.
// The "*" wildcard tells the compiler to auto-generate Build and Revision
// on every compilation. Every build therefore has a unique version, which
// is useful for tying crash reports to a specific binary - and it removes
// the temptation to hand-edit version numbers, which almost always leads
// to drift.
// ---------------------------------------------------------------------------
[assembly: AssemblyVersion("1.0.*")]