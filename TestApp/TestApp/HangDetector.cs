/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * 
 * Developed by Limen
 * 
 * Watches the target process's top-level visible window for
 * responsiveness. If a window stops answering the standard ping
 * message for longer than a threshold, it is considered hung. This
 * runs on its own thread and reports state changes through simple
 * callbacks so the MainForm can log them.
 */
using System;
using System.Threading;

namespace TestApp
{
	internal sealed class HangDetector
	{
		// Callbacks and dependencies are provided by the caller so this
		// class stays decoupled from MainForm.
		private readonly Func<uint> getPid;
		private readonly Action<string> onHangStarted;
		private readonly Action<string> onHangEnded;
		private Thread worker;
		private volatile bool stopRequested;
		private bool lastHungState;
		private DateTime lastHungStart;

		private const int PingIntervalMs = 1000;
		private const uint PingTimeoutMs = 700;

		public HangDetector(Func<uint> pidProvider, Action<string> hangStarted, Action<string> hangEnded)
		{
			getPid = pidProvider;
			onHangStarted = hangStarted;
			onHangEnded = hangEnded;
		}

		// Starts a background thread that pings the target window once per
		// second. Marked IsBackground so it never blocks process exit.
		public void Start()
		{
			stopRequested = false;
			lastHungState = false;
			worker = new Thread(Loop);
			worker.IsBackground = true;
			worker.Start();
		}

		// Requests the worker thread to stop; the thread checks the flag
		// between iterations.
		public void Stop()
		{
			stopRequested = true;
		}

		public bool WasEverHung
		{
			get { return lastHungStart != default(DateTime); }
		}

		public DateTime FirstHangTime
		{
			get { return lastHungStart; }
		}

		// Main loop: find the target's top window, ping it, and fire
		// callbacks on state transitions. The 100 ms sleep loop allows the
		// stop request to be honored quickly.
		private void Loop()
		{
			while (!stopRequested)
			{
				try
				{
					uint pid = getPid();
					if (pid != 0)
					{
						IntPtr hwnd = FindTopWindowForPid(pid);
						if (hwnd != IntPtr.Zero)
						{
							bool hung = IsWindowHung(hwnd);
							if (hung && !lastHungState)
							{
								lastHungState = true;
								if (lastHungStart == default(DateTime))
								{
									lastHungStart = DateTime.Now;
								}
								if (onHangStarted != null)
								{
									try { onHangStarted("Target window became unresponsive at " + DateTime.Now.ToString("HH:mm:ss")); }
									catch { }
								}
							}
							else if (!hung && lastHungState)
							{
								lastHungState = false;
								if (onHangEnded != null)
								{
									try { onHangEnded("Target window recovered at " + DateTime.Now.ToString("HH:mm:ss")); }
									catch { }
								}
							}
						}
					}
				}
				catch
				{
				}

				// Sleep in small slices so Stop() is honored within 100 ms.
				int slept = 0;
				while (slept < PingIntervalMs && !stopRequested)
				{
					Thread.Sleep(100);
					slept += 100;
				}
			}
		}

		// Finds the first visible top-level window owned by the given PID.
		// EnumWindows invokes the callback for every top-level window; the
		// first match wins and we return false to stop enumeration.
		private IntPtr FindTopWindowForPid(uint pid)
		{
			IntPtr found = IntPtr.Zero;
			try
			{
				NativeMethods.EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
				{
					uint winPid;
					NativeMethods.GetWindowThreadProcessId(hWnd, out winPid);
					if (winPid == pid && NativeMethods.IsWindowVisible(hWnd))
					{
						found = hWnd;
						return false;
					}
					return true;
				}, IntPtr.Zero);
			}
			catch
			{
			}
			return found;
		}

		// Two-step hang check:
		// 1. IsHungAppWindow is cheap and covers most cases.
		// 2. WM_NULL with SMTO_ABORTIFHUNG is the historical fallback.
		private bool IsWindowHung(IntPtr hWnd)
		{
			try
			{
				if (NativeMethods.IsHungAppWindow(hWnd))
				{
					return true;
				}

				// Fallback: send WM_NULL with a short timeout.
				IntPtr result;
				IntPtr res = NativeMethods.SendMessageTimeout(
					hWnd, 0x0000, IntPtr.Zero, IntPtr.Zero,
					NativeMethods.SMTO_ABORTIFHUNG, PingTimeoutMs, out result);
				return res == IntPtr.Zero;
			}
			catch
			{
				return false;
			}
		}
	}
}