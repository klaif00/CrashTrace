/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen
 * 
 * 
 * Enumerates all threads in the target process. Even when a crash
 * happens on a single thread, the actual cause is often another
 * thread that corrupted shared state earlier. Seeing the total thread
 * count, and the thread IDs that were active at the crash, gives
 * useful context.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace TestApp
{
	internal static class ThreadAnalyzer
	{
		// A single thread's state captured at a point in time. Every
		// field is a plain value, no handles are kept.
		public struct ThreadSnapshot
		{
			public int ThreadId;
			public string State;
			public string WaitReason;
			public long TotalProcessorTimeMs;
			public string PriorityName;
		}

		// Enumerates all threads of the given process and returns one
		// snapshot per thread. Fields that cannot be read (because the
		// thread already exited, or access was denied) fall back to a
		// safe default so the report still has a row for each thread.
		public static List<ThreadSnapshot> Snapshot(uint pid)
		{
			List<ThreadSnapshot> list = new List<ThreadSnapshot>();
			try
			{
				Process proc = Process.GetProcessById((int)pid);
				ProcessThreadCollection threads = proc.Threads;
				foreach (ProcessThread t in threads)
				{
					ThreadSnapshot s = new ThreadSnapshot();
					s.ThreadId = t.Id;

					// Each accessor is wrapped individually: some of them
					// throw when the underlying thread has already ended.
					try { s.State = t.ThreadState.ToString(); }
					catch { s.State = "Unknown"; }

					try { s.WaitReason = t.WaitReason.ToString(); }
					catch { s.WaitReason = "Unknown"; }

					try { s.TotalProcessorTimeMs = (long)t.TotalProcessorTime.TotalMilliseconds; }
					catch { s.TotalProcessorTimeMs = -1; }

					try { s.PriorityName = t.PriorityLevel.ToString(); }
					catch { s.PriorityName = "Unknown"; }

					list.Add(s);
				}
			}
			catch
			{
			}
			return list;
		}
	}
}