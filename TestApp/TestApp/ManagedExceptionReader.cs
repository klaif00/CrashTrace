/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen
 * 
 * Tries to read the current managed exception from a thread.
 *
 * Three paths, in order:
 *   1. Direct: attach ClrMD from this process. Works when this
 *      process and the target have the same architecture.
 *   2. Helper (same arch): launch TestAppHelper32.exe or
 *      TestAppHelper64.exe next to us, whichever matches the
 *      target's bitness, and let it read the exception.
 *   3. Give up and return whatever error we collected.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime;

namespace TestApp
{
	internal static class ManagedExceptionReader
	{
		// Public entry point. Tries the direct ClrMD path first, then
		// the out-of-process helper. All out parameters are always
		// initialized, even on failure.
		public static bool TryRead(int pid, uint crashingTid,
			out string exceptionType, out string exceptionMessage,
			out List<string> managedStackTrace, out string error)
		{
			// Initialize ALL out parameters immediately, so every
			// early return path is valid.
			exceptionType = null;
			exceptionMessage = null;
			managedStackTrace = new List<string>();
			error = null;

			// Try the direct path first.
			string directError;
			if (TryReadDirect(pid, crashingTid,
				out exceptionType, out exceptionMessage,
				out managedStackTrace, out directError))
			{
				return true;
			}

			// Direct failed. Try the helper path, choosing the correct
			// helper based on the target's bitness.
			string helperError;
			if (TryReadViaHelper(pid, crashingTid,
				out exceptionType, out exceptionMessage,
				out managedStackTrace, out helperError))
			{
				return true;
			}

			// Both failed. Combine the errors so the report shows why.
			error = "Direct read failed (" + (directError ?? "unknown") +
				"). Helper also failed (" + (helperError ?? "unknown") + ").";
			return false;
		}

		// ------------------------------------------------------------------
		// Direct path
		// ------------------------------------------------------------------
		// Attaches to the target with ClrMD in passive mode. Works only
		// when this process and the target share the same architecture.
		private static bool TryReadDirect(int pid, uint crashingTid,
			out string exceptionType, out string exceptionMessage,
			out List<string> managedStackTrace, out string error)
		{
			exceptionType = null;
			exceptionMessage = null;
			managedStackTrace = new List<string>();
			error = null;

			try
			{
				using (DataTarget dt = DataTarget.AttachToProcess(pid, 5000, AttachFlag.Passive))
				{
					// A managed process must expose at least one CLR
					// version, otherwise this is not a .NET process.
					if (dt.ClrVersions.Count == 0)
					{
						error = "No CLR runtime found in the target process.";
						return false;
					}

					ClrInfo clrInfo = dt.ClrVersions[0];
					ClrRuntime runtime = clrInfo.CreateRuntime();

					// Find the ClrThread that matches the crashing OS TID.
					ClrThread crashThread = null;
					foreach (ClrThread t in runtime.Threads)
					{
						if (t.OSThreadId == crashingTid)
						{
							crashThread = t;
							break;
						}
					}

					if (crashThread == null)
					{
						error = "Could not find the managed thread (TID " + crashingTid + ").";
						return false;
					}

					ClrException ex = crashThread.CurrentException;
					if (ex == null)
					{
						error = "No managed exception object was found on this thread.";
						return false;
					}

					exceptionType = (ex.Type != null) ? ex.Type.Name : "(unknown type)";
					exceptionMessage = ex.Message;

					// Read the managed stack trace frame by frame.
					foreach (ClrStackFrame frame in ex.StackTrace)
					{
						string name = (frame.Method != null)
							? frame.Method.Name
							: frame.ToString();
						managedStackTrace.Add(name);
					}

					return true;
				}
			}
			catch (Exception readEx)
			{
				error = readEx.Message;
				return false;
			}
		}

		// ------------------------------------------------------------------
		// Helper path
		// ------------------------------------------------------------------
		// Launches a same-architecture helper executable, hands it the
		// PID and TID, and parses its small key=value output file.
		private static bool TryReadViaHelper(int pid, uint crashingTid,
			out string exceptionType, out string exceptionMessage,
			out List<string> managedStackTrace, out string error)
		{
			exceptionType = null;
			exceptionMessage = null;
			managedStackTrace = new List<string>();
			error = null;

			// Decide which helper to use based on the target's bitness.
			string helperName;
			try
			{
				bool isTarget64 = IsTargetProcess64Bit(pid);
				helperName = isTarget64 ? "TestAppHelper64.exe" : "TestAppHelper32.exe";
			}
			catch
			{
				helperName = "TestAppHelper32.exe";
			}

			string outputPath = null;
			try
			{
				string helperPath = GetHelperPath(helperName);
				if (helperPath == null)
				{
					error = helperName + " not found next to the main executable.";
					return false;
				}

				// The helper writes its findings to a temp file. Using a
				// GUID avoids collisions between simultaneous runs.
				outputPath = Path.Combine(Path.GetTempPath(),
					"TestAppHelper_" + Guid.NewGuid().ToString("N") + ".txt");

				ProcessStartInfo psi = new ProcessStartInfo();
				psi.FileName = helperPath;
				psi.Arguments = pid.ToString() + " " + crashingTid.ToString()
					+ " \"" + outputPath + "\"";
				psi.UseShellExecute = false;
				psi.CreateNoWindow = true;
				psi.WindowStyle = ProcessWindowStyle.Hidden;

				Process p = Process.Start(psi);
				if (p == null)
				{
					error = "Could not start the helper process.";
					return false;
				}

				// Hard timeout: if the helper hangs, kill it and report.
				if (!p.WaitForExit(10000))
				{
					try { p.Kill(); } catch { }
					error = "The helper process did not finish within 10 seconds.";
					return false;
				}

				if (!File.Exists(outputPath))
				{
					error = "The helper produced no output file.";
					return false;
				}

				string[] lines = File.ReadAllLines(outputPath);

				string status = null;
				List<string> frames = new List<string>();

				// Simple key=value parser. Unknown keys are ignored.
				foreach (string line in lines)
				{
					int eq = line.IndexOf('=');
					if (eq < 0) continue;

					string key = line.Substring(0, eq);
					string val = line.Substring(eq + 1);

					if (key == "STATUS") status = val;
					else if (key == "TYPE") exceptionType = val;
					else if (key == "MESSAGE") exceptionMessage = val;
					else if (key == "ERROR") error = val;
					else if (key == "FRAME") frames.Add(val);
				}

				managedStackTrace = frames;

				return status == "OK";
			}
			catch (Exception ex)
			{
				error = ex.Message;
				return false;
			}
			finally
			{
				// Always try to remove the temp file, even on failure.
				if (outputPath != null)
				{
					try { File.Delete(outputPath); } catch { }
				}
			}
		}

		// ------------------------------------------------------------------
		// Detection: is the target 64-bit?
		// ------------------------------------------------------------------
		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool IsWow64Process(IntPtr hProcess, out bool Wow64Process);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool CloseHandle(IntPtr hObject);

		private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

		// Returns true only when this process is 64-bit AND the target
		// is not running under WOW64 (i.e. the target is native 64-bit).
		private static bool IsTargetProcess64Bit(int pid)
		{
			try
			{
				if (!Environment.Is64BitOperatingSystem) return false;
				if (!Environment.Is64BitProcess) return false;

				IntPtr hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
				if (hProc == IntPtr.Zero) return false;

				try
				{
					bool isWow64;
					if (!IsWow64Process(hProc, out isWow64))
						return false;

					return !isWow64;
				}
				finally
				{
					try { CloseHandle(hProc); } catch { }
				}
			}
			catch
			{
				return false;
			}
		}

		// ------------------------------------------------------------------
		// Helper path resolution
		// ------------------------------------------------------------------
		// Looks for the helper next to the main executable.
		private static string GetHelperPath(string helperName)
		{
			try
			{
				string exeDir = Path.GetDirectoryName(
					System.Reflection.Assembly.GetExecutingAssembly().Location);
				if (string.IsNullOrEmpty(exeDir)) return null;

				string p = Path.Combine(exeDir, helperName);
				if (File.Exists(p)) return p;
			}
			catch { }

			return null;
		}
	}
}