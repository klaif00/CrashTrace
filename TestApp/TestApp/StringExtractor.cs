/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * 
 * Developed by Limen 
 * 
 * Extracts printable strings from a binary file. Useful for finding
 * embedded error messages, configuration paths, internal function
 * names that survived stripping, and anything else human-readable
 * that the developer accidentally left in.
 *
 * The filter is deliberately strict: it rejects runs of repeated
 * characters (which usually come from uninitialized memory or padding),
 * demands a high ratio of letters, and requires at least one recognizable
 * "word" (three or more consecutive letters) so we don't flood the
 * report with binary noise masquerading as text.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TestApp
{
	internal static class StringExtractor
	{
		// Convenience overload: reads the whole file and delegates to
		// ExtractFromBytes. Returns an empty list on any I/O failure.
		public static List<string> ExtractFromFile(string filePath, int maxStrings, int minLength)
		{
			List<string> result = new List<string>();
			try
			{
				byte[] data = File.ReadAllBytes(filePath);
				return ExtractFromBytes(data, maxStrings, minLength);
			}
			catch
			{
			}
			return result;
		}

		// Core routine. Walks the byte array and collects printable
		// ASCII runs that pass the IsInteresting filter. Stops as soon
		// as maxStrings entries have been found.
		public static List<string> ExtractFromBytes(byte[] data, int maxStrings, int minLength)
		{
			List<string> result = new List<string>();
			try
			{
				StringBuilder current = new StringBuilder();
				for (int i = 0; i < data.Length; i++)
				{
					byte b = data[i];
					if (b >= 0x20 && b < 0x7F)
					{
						current.Append((char)b);
					}
					else
					{
						// A non-printable byte terminates the current run.
						if (current.Length >= minLength)
						{
							string s = current.ToString();
							if (IsInteresting(s) && !result.Contains(s))
							{
								result.Add(s);
								if (result.Count >= maxStrings) return result;
							}
						}
						current.Length = 0;
					}
				}

				// Flush the final run
				if (current.Length >= minLength)
				{
					string s = current.ToString();
					if (IsInteresting(s) && !result.Contains(s) && result.Count < maxStrings)
					{
						result.Add(s);
					}
				}
			}
			catch
			{
			}
			return result;
		}

		// Heuristic filter. Rejects runs of repeated bytes, binary
		// noise, and single-word garbage. Accepts strings that look like
		// real messages, paths, URLs, or contain common error keywords.
		private static bool IsInteresting(string s)
		{
			if (string.IsNullOrEmpty(s)) return false;
			if (s.Length < 10 || s.Length > 512) return false;

			int letters = 0;
			int digits = 0;
			int spaces = 0;
			int punctuation = 0;
			int other = 0;

			int maxRepeat = 1;
			int currentRepeat = 1;

			for (int i = 0; i < s.Length; i++)
			{
				char c = s[i];
				if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) letters++;
				else if (c >= '0' && c <= '9') digits++;
				else if (c == ' ') spaces++;
				else if (IsPunctuation(c)) punctuation++;
				else other++;

				if (i > 0 && s[i] == s[i - 1])
				{
					currentRepeat++;
					if (currentRepeat > maxRepeat) maxRepeat = currentRepeat;
				}
				else
				{
					currentRepeat = 1;
				}
			}

			// Binary noise often has long runs of identical bytes.
			if (maxRepeat >= 4) return false;

			// Non-printable-ish garbage inside the string is a red flag.
			if (other > s.Length / 10) return false;

			// Needs a healthy ratio of letters to be real text.
			double letterRatio = (double)letters / s.Length;
			if (letterRatio < 0.45) return false;

			// Must contain at least one recognizable word (3+ consecutive letters).
			if (!HasWord(s)) return false;

			// Strong signal: file paths, dll/exe references, common error words.
			if (ContainsAny(s, ".dll", ".exe", ".log", ".ini", ".xml", ".dat",
							   "error", "fail", "cannot", "missing", "unable",
							   "invalid", "denied", "not found", "http://",
							   "https://", "C:\\", "D:\\", "\\\\", "\\%s", "%s"))
			{
				return true;
			}

			// Multi-word phrases are usually real human-readable messages.
			if (spaces >= 2) return true;

			// At least one letter-word plus something else useful.
			if (letters >= 5 && (digits > 0 || punctuation > 0)) return true;

			// Otherwise, be conservative and reject single-word noise.
			return false;
		}

		// Returns true when the string contains at least three
		// consecutive letters, which is the minimum for "looks like a
		// real word".
		private static bool HasWord(string s)
		{
			int run = 0;
			for (int i = 0; i < s.Length; i++)
			{
				char c = s[i];
				if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
				{
					run++;
					if (run >= 3) return true;
				}
				else
				{
					run = 0;
				}
			}
			return false;
		}

		// Classification helper for the "other characters" ratio check.
		private static bool IsPunctuation(char c)
		{
			switch (c)
			{
				case '.': case ',': case '!': case '?': case ':': case ';':
				case '-': case '_': case '/': case '\\': case '(': case ')':
				case '[': case ']': case '\'': case '"': case '@': case '#':
				case '$': case '%': case '&': case '*': case '+': case '=':
				case '<': case '>': case '|': case '~': case '`': case '^':
				case '{': case '}':
					return true;
			}
			return false;
		}

		// Case-insensitive "contains any of" helper used by the strong-
		// signal check above.
		private static bool ContainsAny(string s, params string[] needles)
		{
			foreach (string n in needles)
			{
				if (s.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0) return true;
			}
			return false;
		}
	}
}