/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen
 * 
 * 
 * Small static lookup lists used to flag loaded modules that are worth
 * calling out in a report: third-party overlay/hook tools that commonly
 * conflict with games, and anti-cheat systems (informational, not a
 * problem by themselves).
 */
namespace TestApp
{
	internal static class ModuleClassifier
	{
		// Known third-party modules that commonly interfere with games (overlays/hooks)
		public static readonly string[] SuspiciousModules = new string[]
		{
			"rtsshooks", "gameoverlayrenderer", "discord_hook", "nvspcap",
			"gamebarpresencewriter", "xsplit", "obs-hook", "asusoverlay",
			"eosoverlayrenderer", "nahimicosd", "razerironclaw"
		};

		// Known anti-cheat modules - informational only, not inherently a
		// problem, but useful context (e.g. some anti-cheat systems are
		// known to conflict with certain overlays/drivers).
		public static readonly string[] AntiCheatModules = new string[]
		{
			"easyanticheat", "eac", "beservice", "bedaisy", "battleye",
			"vanguard", "vgc", "punkbuster", "pbcl", "xigncode"
		};

		// Case-sensitive substring match. Callers are expected to pass
		// the file name already lowercased.
		public static bool MatchesAny(string fileNameLower, string[] list)
		{
			foreach (string candidate in list)
			{
				if (fileNameLower.Contains(candidate))
				{
					return true;
				}
			}
			return false;
		}
	}
}