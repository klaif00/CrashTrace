/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen
 * 
 * 
 * Detects the engine or framework a program is built with, based on
 * well-known DLL names and file presence. This is purely informative:
 * it does not modify any behavior, it just reads the module list and
 * the target's directory. No false positives are produced because the
 * signals we look for are specific, unmistakable file names.
 */
using System;
using System.Collections.Generic;
using System.IO;

namespace TestApp
{
	internal static class EngineDetector
	{
		// Returns the engine/framework name, or null if nothing matched.
		// Loaded module paths come from the running target; file existence
		// checks are done against the target's own directory.
		public static string Detect(string exePath, IEnumerable<string> loadedModulePaths)
		{
			try
			{
				// Normalize module names once; only the file name matters.
				HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				foreach (string p in loadedModulePaths)
				{
					if (string.IsNullOrEmpty(p)) continue;
					try { names.Add(Path.GetFileName(p)); } catch { }
				}

				// Unity
				if (names.Contains("UnityPlayer.dll") || names.Contains("mono-2.0-bdwgc.dll"))
					return "Unity Engine";

				// Unreal Engine: the shipped DLLs are prefixed UE4- / UE5-.
				foreach (string n in names)
				{
					if (n.StartsWith("UE4-", StringComparison.OrdinalIgnoreCase)) return "Unreal Engine 4";
					if (n.StartsWith("UE5-", StringComparison.OrdinalIgnoreCase)) return "Unreal Engine 5";
				}
				if (names.Contains("UnrealEngine.dll")) return "Unreal Engine";

				// Source engine: tier0 + vstdlib are the signature pair.
				if (names.Contains("tier0.dll") && names.Contains("vstdlib.dll"))
					return "Source Engine (Valve)";

				// CryEngine
				if (names.Contains("CrySystem.dll") || names.Contains("CryEngine.dll"))
					return "CryEngine";

				// GameMaker: identified by its data files, not by DLLs.
				try
				{
					string dir = Path.GetDirectoryName(exePath);
					if (!string.IsNullOrEmpty(dir))
					{
						if (File.Exists(Path.Combine(dir, "data.win"))) return "GameMaker Studio";
						if (File.Exists(Path.Combine(dir, "game.unx"))) return "GameMaker Studio";
					}
				}
				catch { }

				// Godot
				if (names.Contains("godot.dll") || names.Contains("GodotSharp.dll"))
					return "Godot Engine";

				// MonoGame / XNA
				if (names.Contains("MonoGame.Framework.dll")) return "MonoGame";
				if (names.Contains("Microsoft.Xna.Framework.dll")) return "XNA Framework";

				// RPG Maker: RGSS runtime DLLs sit next to the exe.
				try
				{
					string dir = Path.GetDirectoryName(exePath);
					if (!string.IsNullOrEmpty(dir))
					{
						if (File.Exists(Path.Combine(dir, "RGSS102E.dll")) ||
							File.Exists(Path.Combine(dir, "RGSS200E.dll")) ||
							File.Exists(Path.Combine(dir, "RGSS300.dll")))
							return "RPG Maker (RGSS)";
					}
				}
				catch { }

				// Electron: this specific trio is the standard runtime shape.
				if (names.Contains("libEGL.dll") && names.Contains("libGLESv2.dll") && names.Contains("node.dll"))
					return "Electron (web-based)";

				// Adobe AIR / Flash
				if (names.Contains("Adobe AIR.dll") || names.Contains("FlashPlayer.dll"))
					return "Adobe AIR / Flash";

				// Nothing matched: caller treats null as "unknown engine".
				return null;
			}
			catch
			{
				return null;
			}
		}
	}
}