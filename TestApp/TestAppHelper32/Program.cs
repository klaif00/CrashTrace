/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen
 * 
 * 
 * TestAppHelper32 - small command-line helper used by TestApp.
 *
 * Because a 64-bit process cannot load the 32-bit DAC that ClrMD
 * needs to read a 32-bit .NET target, TestApp launches this helper
 * whenever it needs to inspect a 32-bit managed crash. This helper
 * is compiled as x86, so it can load the correct DAC.
 *
 * Usage: TestAppHelper32.exe <pid> <tid> <outputFilePath>
 * Exit codes:
 *   0 = success (output file written with STATUS=OK)
 *   1 = failure (output file written with STATUS=FAIL, or nothing)
 *   2 = wrong arguments
 *   3 = invalid arguments
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TestAppHelper32
{
	internal sealed class Program
	{
		[STAThread]
		private static int Main(string[] args)
		{
			// Exit code 2: not enough arguments to do anything.
			if (args == null || args.Length < 3)
				return 2;

			// Exit code 3: arguments are present but not valid numbers.
			int pid;
			uint tid;
			if (!int.TryParse(args[0], out pid)) return 3;
			if (!uint.TryParse(args[1], out tid)) return 3;

			string outputPath = args[2];

			try
			{
				string type, message, error;
				List<string> stack;

				// Delegate the actual ClrMD work to HelperExceptionReader.
				bool ok = HelperExceptionReader.TryRead(
					pid, tid, out type, out message, out stack, out error);

				WriteResult(outputPath, ok, type, message, stack, error);
				return ok ? 0 : 1;
			}
			catch (Exception ex)
			{
				// Even on unexpected failure, try to leave a result file
				// behind so the parent process knows what happened.
				try
				{
					WriteResult(outputPath, false, null, null,
						new List<string>(), ex.Message);
				}
				catch { }
				return 1;
			}
		}

		// Writes the small key=value output file. STATUS is always the
		// first line. On success we emit TYPE, MESSAGE and one FRAME
		// line per stack frame. On failure we emit ERROR.
		private static void WriteResult(string path, bool ok,
			string type, string message, List<string> stack, string error)
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine(ok ? "STATUS=OK" : "STATUS=FAIL");

			if (ok)
			{
				sb.AppendLine("TYPE=" + OneLine(type));
				sb.AppendLine("MESSAGE=" + OneLine(message));
				if (stack != null)
				{
					foreach (string frame in stack)
						sb.AppendLine("FRAME=" + OneLine(frame));
				}
			}
			else
			{
				sb.AppendLine("ERROR=" + OneLine(error));
			}

			File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
		}

		// Collapse newlines and carriage returns into spaces so each
		// field fits on a single line of the output file.
		private static string OneLine(string s)
		{
			if (s == null) return "";
			return s.Replace("\r", " ").Replace("\n", " ").Trim();
		}
	}
}