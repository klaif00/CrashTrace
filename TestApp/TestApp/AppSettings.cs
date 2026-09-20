/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen
 * Tiny local settings file (not the Windows Registry, not app.config's
 * user settings mechanism) so the tool stays fully portable: a single
 * text file living next to the .exe.
 */
using System;
using System.IO;
using System.Reflection;

namespace TestApp
{
	internal static class AppSettings
	{
		// Resolves <exe folder>\settings.txt. Computed on demand so the
		// path is always correct even if the exe is moved.
		private static string SettingsFilePath
		{
			get
			{
				string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
				return Path.Combine(exeDir, "settings.txt");
			}
		}

		// Reads settings.txt and returns true only if it contains
		// "DarkMode=True". Anything else (missing file, unreadable,
		// different value) falls back to false.
		public static bool LoadDarkMode()
		{
			try
			{
				if (File.Exists(SettingsFilePath))
				{
					string content = File.ReadAllText(SettingsFilePath);
					return content.IndexOf("DarkMode=True", StringComparison.OrdinalIgnoreCase) >= 0;
				}
			}
			catch
			{
			}
			return false;
		}

		// Writes the current dark-mode flag to settings.txt, replacing
		// the file content. Failures are ignored on purpose so the app
		// never crashes because of a read-only folder.
		public static void SaveDarkMode(bool enabled)
		{
			try
			{
				File.WriteAllText(SettingsFilePath, "DarkMode=" + enabled);
			}
			catch
			{
			}
		}
	}
}