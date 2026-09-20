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
 * Program entry point: installs global exception handlers, checks for
 * Administrator privileges, and prevents more than one instance from
 * running at the same time.
 *
 * The application manifest requests Administrator level, so Windows will
 * normally show a UAC prompt and launch us as admin. If we somehow end up
 * without admin rights (e.g. group policy blocks elevation), we try to
 * relaunch ourselves elevated once, and if that fails, we show a clear
 * message and exit.
 */
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TestApp
{
	internal sealed class Program
	{
		// Single-instance guard. Held for the lifetime of the process.
		private static Mutex singleInstanceMutex;

		[STAThread]
		private static void Main(string[] args)
		{
			// Wire up global exception handlers before anything else,
			// so any startup failure produces an emergency log.
			Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
			Application.ThreadException += OnThreadException;
			AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

			try
			{
				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);

				// The manifest asks for admin. If we do not have it, try
				// to relaunch elevated; if that fails, tell the user.
				if (!IsRunningAsAdministrator())
				{
					if (!TryRelaunchElevated(args))
					{
						MessageBox.Show(
							"This tool requires Administrator privileges to work properly.\n\n" +
							"The application manifest requested elevated privileges, but Windows\n" +
							"did not grant them. This usually means a group policy blocks the\n" +
							"elevation prompt. Please right-click the tool's icon and choose\n" +
							"\"Run as administrator\" manually.",
							"Administrator Privileges Required",
							MessageBoxButtons.OK,
							MessageBoxIcon.Warning);
					}
					return;
				}

				// Named mutex enforces "one instance at a time". The name
				// is global to the session so two different users can
				// still run their own copy if needed.
				bool createdNew;
				singleInstanceMutex = new Mutex(true, "TestApp_CrashDiagnosticTool_SingleInstance", out createdNew);

				if (!createdNew)
				{
					MessageBox.Show(
						"Another copy of this tool is already running.\n\n" +
						"Only one instance can monitor a process at a time.",
						"Already Running",
						MessageBoxButtons.OK,
						MessageBoxIcon.Information);
					return;
				}

				try
				{
					MainForm form = new MainForm();

					// Command-line support: if a file path was passed, use it.
					try
					{
						if (args != null && args.Length > 0)
						{
							string initial = args[0];
							if (!string.IsNullOrEmpty(initial) && File.Exists(initial))
								form.LoadInitialTarget(initial);
						}
					}
					catch { }

					Application.Run(form);
				}
				finally
				{
					try { singleInstanceMutex.ReleaseMutex(); }
					catch { }
				}
			}
			catch (Exception ex)
			{
				WriteEmergencyLog("Startup", ex);
				try
				{
					MessageBox.Show(
						"The tool failed to start:\n\n" + ex.Message +
						"\n\nAn emergency log was written to your Desktop.",
						"Startup Error",
						MessageBoxButtons.OK,
						MessageBoxIcon.Error);
				}
				catch { }
			}
		}

		// Relaunches this executable with the "runas" verb, which
		// triggers the UAC prompt. Arguments are quoted and forwarded.
		private static bool TryRelaunchElevated(string[] args)
		{
			try
			{
				ProcessStartInfo psi = new ProcessStartInfo();
				psi.FileName = Assembly.GetExecutingAssembly().Location;
				psi.UseShellExecute = true;
				psi.Verb = "runas";

				if (args != null && args.Length > 0)
				{
					StringBuilder sb = new StringBuilder();
					for (int i = 0; i < args.Length; i++)
					{
						if (i > 0) sb.Append(' ');
						sb.Append('"').Append(args[i]).Append('"');
					}
					psi.Arguments = sb.ToString();
				}

				Process.Start(psi);
				return true;
			}
			catch
			{
				return false;
			}
		}

		// UI-thread exceptions are logged and swallowed so the user can
		// keep working with the rest of the tool.
		private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
		{
			WriteEmergencyLog("UI thread exception", e != null ? e.Exception : null);
		}

		// Unhandled exceptions on background threads are logged. When
		// IsTerminating is true the process is about to die, but we still
		// try to leave a log file behind.
		private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Exception ex = e != null ? e.ExceptionObject as Exception : null;
			WriteEmergencyLog("Unhandled exception (terminating=" +
				(e != null ? e.IsTerminating.ToString() : "?") + ")", ex);
		}

		// Writes a small diagnostic text file to the Desktop. Used only
		// when the normal logging is not available yet (startup, global
		// exception handlers).
		private static void WriteEmergencyLog(string context, Exception ex)
		{
			try
			{
				string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
				if (string.IsNullOrEmpty(desktop)) desktop = Path.GetTempPath();

				string path = Path.Combine(desktop,
					"TestApp_EmergencyLog_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt");

				StringBuilder sb = new StringBuilder();
				sb.AppendLine("Emergency log - " + context);
				sb.AppendLine("Time         : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
				sb.AppendLine("OS           : " + Environment.OSVersion);
				sb.AppendLine("CLR          : " + Environment.Version);
				sb.AppendLine("64-bit OS    : " + Environment.Is64BitOperatingSystem);
				sb.AppendLine("64-bit proc  : " + Environment.Is64BitProcess);
				sb.AppendLine("Working dir  : " + Environment.CurrentDirectory);
				sb.AppendLine();

				if (ex != null)
				{
					sb.AppendLine("Exception type    : " + ex.GetType().FullName);
					sb.AppendLine("Exception message : " + ex.Message);
					sb.AppendLine();
					sb.AppendLine("Stack trace:");
					sb.AppendLine(ex.ToString());
				}
				else
				{
					sb.AppendLine("(no exception object provided)");
				}

				File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
			}
			catch
			{
			}
		}

		// Uses the Windows identity to check whether the current process
		// is running with Administrator rights.
		private static bool IsRunningAsAdministrator()
		{
			try
			{
				WindowsIdentity identity = WindowsIdentity.GetCurrent();
				WindowsPrincipal principal = new WindowsPrincipal(identity);
				return principal.IsInRole(WindowsBuiltInRole.Administrator);
			}
			catch
			{
				return false;
			}
		}
	}
}