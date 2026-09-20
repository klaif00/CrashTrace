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
 * Reads the current managed exception from a specific thread in a
 * 64-bit .NET process. Runs inside the x64 helper, so the ClrMD DAC
 * architecture matches and the read succeeds.
 */
using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Runtime;

namespace TestAppHelper64
{
	internal static class HelperExceptionReader
	{
		// Same algorithm as the 32-bit helper, but compiled as x64 so
		// the DAC that ClrMD loads matches a 64-bit target. All out
		// parameters are initialized before any early return.
		public static bool TryRead(int pid, uint crashingTid,
			out string exceptionType, out string exceptionMessage,
			out List<string> managedStackTrace, out string error)
		{
			exceptionType = null;
			exceptionMessage = null;
			managedStackTrace = new List<string>();
			error = null;

			try
			{
				// Passive attach: we do not pause or modify the target,
				// we only inspect it.
				using (DataTarget dt = DataTarget.AttachToProcess(pid, 5000, AttachFlag.Passive))
				{
					// A managed process must expose at least one CLR
					// version. If not, this is not a .NET process.
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
	}
}