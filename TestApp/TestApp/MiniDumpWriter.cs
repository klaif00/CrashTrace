/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen
 * 
 * 
 * Writes a Windows minidump file for the crashed process. The dump can
 * later be opened in WinDbg or Visual Studio for deeper post-mortem
 * analysis even though we don't have symbols loaded here.
 */
using System;
using System.IO;

namespace TestApp
{
	internal static class MiniDumpWriter
	{
		// Writes a minidump for the given process to outputPath.
		// Returns true on success, false on any failure.
		public static bool WriteDump(IntPtr hProcess, uint pid, string outputPath)
		{
			try
			{
				// Use a FileStream so we can pass its native handle to
				// MiniDumpWriteDump. The stream is closed automatically
				// at the end of the using block.
				using (FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
				{
					// A moderate flag set: captures data segments,
					// handle info, unloaded modules, per-process and
					// per-thread data, thread info, and memory that is
					// only referenced indirectly. This is a good balance
					// between dump size and usefulness.
					uint flags =
						NativeMethods.MiniDumpWithDataSegs |
						NativeMethods.MiniDumpWithHandleData |
						NativeMethods.MiniDumpWithUnloadedModules |
						NativeMethods.MiniDumpWithProcessThreadData |
						NativeMethods.MiniDumpWithThreadInfo |
						NativeMethods.MiniDumpWithIndirectlyReferencedMemory;

					// The last three parameters (exception info, user
					// stream, callback) are not used here.
					bool ok = NativeMethods.MiniDumpWriteDump(
						hProcess,
						pid,
						fs.SafeFileHandle.DangerousGetHandle(),
						flags,
						IntPtr.Zero,
						IntPtr.Zero,
						IntPtr.Zero);
					return ok;
				}
			}
			catch
			{
				return false;
			}
		}
	}
}