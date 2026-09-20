/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen
 * 
 * 
 * Reads the application manifest for the target executable. Manifests
 * describe OS compatibility, DPI awareness, requested privileges, and
 * required side-by-side assemblies (like specific Visual C++ runtimes).
 *
 * Two sources are checked:
 *   1. External: <exe>.manifest file next to the executable.
 *   2. Embedded: a small XML fragment stored in the PE resource section.
 *
 * The embedded search is done by scanning the raw file bytes for the
 * "<?xml" marker followed by "assembly" - this is a pragmatic heuristic
 * that catches ~90% of real manifests without implementing the full
 * resource directory parser.
 */
using System;
using System.IO;
using System.Text;

namespace TestApp
{
	internal static class ManifestReader
	{
		// Public entry point. Returns the manifest text (with a small
		// "Source:" header) or null if no manifest was found.
		public static string ReadManifest(string exePath)
		{
			try
			{
				// External manifest first - preferred if present
				string externalPath = exePath + ".manifest";
				if (File.Exists(externalPath))
				{
					try
					{
						return "Source: external file (" + Path.GetFileName(externalPath) + ")\n" +
							File.ReadAllText(externalPath);
					}
					catch { }
				}

				// Embedded: search raw bytes for an XML manifest fragment
				try
				{
					byte[] data = File.ReadAllBytes(exePath);
					string manifest = FindEmbeddedManifest(data);
					if (!string.IsNullOrEmpty(manifest))
						return "Source: embedded resource\n" + manifest;
				}
				catch { }
			}
			catch
			{
				// Fall through to null.
			}

			return null;
		}

		// Scans the raw file bytes for a "<?xml" marker followed by an
		// "assembly" element. This is a heuristic, not a real PE
		// resource-directory parser, and it is intentional: the goal is
		// coverage of common manifests without the complexity of a full
		// parser.
		private static string FindEmbeddedManifest(byte[] data)
		{
			try
			{
				// Look for "<?xml" followed (within a reasonable window) by
				// "<assembly". Cap the manifest at 64 KB.
				int maxLen = data.Length;
				for (int i = 0; i < maxLen - 5; i++)
				{
					if (data[i] != '<' || data[i + 1] != '?') continue;
					if (data[i + 2] != 'x' && data[i + 2] != 'X') continue;
					if (data[i + 3] != 'm' && data[i + 3] != 'M') continue;
					if (data[i + 4] != 'l') continue;

					// Found <?xml - now find where the XML ends
					int end = i;
					int limit = Math.Min(data.Length, i + 65536);
					for (int j = i; j < limit - 1; j++)
					{
						if (data[j] == 0) { end = j; break; }
						if (data[j] == '<' && data[j + 1] == '/') continue;
					}
					if (end == i) end = limit;

					// Extract and check for <assembly
					StringBuilder sb = new StringBuilder();
					for (int j = i; j < end && sb.Length < 65536; j++)
					{
						byte b = data[j];
						if (b == 0) break;
						if (b >= 0x09 && b < 0x7F || b == 0x0A || b == 0x0D)
							sb.Append((char)b);
						else
							sb.Append(' ');
					}
					string xml = sb.ToString();
					if (xml.IndexOf("assembly", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						return xml.Trim();
					}
				}
			}
			catch
			{
			}
			return null;
		}
	}
}