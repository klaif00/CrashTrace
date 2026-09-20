/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Created by SharpDevelop.
 * Developed by Limen
 *
 * Crash / Exit Diagnostic Tool - Advanced Edition
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TestApp
{
	public partial class MainForm : Form
	{
		#region State

		// Shared theme state. Other forms read IsDarkMode and subscribe to
		// ThemeChanged to update themselves live.
		public static bool IsDarkMode { get; private set; }
		public static event Action ThemeChanged;

		// References to the auxiliary windows so we can push theme updates
		// to them and close them with the main form.
		private DllScanForm activeDllScanForm;
		private Can_I_Run_It activeCanIRunItForm;
		private AboutForm activeAboutForm;

		// Target and debug session state.
		private string targetPath;
		private string reportFilePath;
		private Thread debugThread;
		private volatile bool stopRequested;
		private volatile bool userForcedStop;
		private IntPtr hProcess = IntPtr.Zero;
		private IntPtr hThread = IntPtr.Zero;
		private uint currentPid;

		// Module map captured from debug events (base address -> path).
		private Dictionary<IntPtr, string> loadedModuleNames = new Dictionary<IntPtr, string>();
		private List<string> suspiciousModulesFound = new List<string>();
		private List<string> antiCheatModulesFound = new List<string>();
		private List<string> recentEvents = new List<string>();
		private const int RECENT_EVENTS_LIMIT = 40;

		// Full log of the session. This is what ends up in the report.
		private List<string> allLogLines = new List<string>();

		// Crash outcome fields.
		private bool wasFatalCrash;
		private bool wasSilentExit;
		private uint finalStatusCode;
		private IntPtr crashAddress = IntPtr.Zero;
		private string crashModuleName = "Unknown";
		private IntPtr crashModuleBase = IntPtr.Zero;
		private ulong crashModuleOffset = 0;
		private bool haveAccessInfo;
		private long accessType = -1;
		private IntPtr accessedAddress = IntPtr.Zero;
		private bool isTarget32Bit;
		private bool isTarget64Bit;
		private bool symbolsInitialized;
		private bool symbolsWereResolved;
		private string symbolPathUsed;
		private List<string> capturedCallStack = new List<string>();
		private string symbolDiagnosticNote;
		private uint lastFirstChanceCode;
		private string managedExceptionType;
		private string managedExceptionMessage;
		private List<string> managedStackTraceLines = new List<string>();
		private IntPtr lastFirstChanceAddress = IntPtr.Zero;
		private bool haveLastFirstChance;
		private long crashWorkingSetBytes = -1;
		private int crashHandleCount = -1;
		private int crashGdiObjects = -1;
		private int crashUserObjects = -1;
		private string signatureInfo;
		private string compatibilityFlagsInfo;
		private string crashScreenshotPath;
		private string crashMiniDumpPath;
		private List<string> confirmedMissingDlls = new List<string>();
		private List<string> indirectMissingDlls = new List<string>();
		private List<string> probablyFineDlls = new List<string>();
		private List<string> redistributableSuggestions = new List<string>();
		private string peChecksumWarning;
		private string sha256Hash;
		private long crashAvailableSystemMemoryMb = -1;
		private long crashFreeDiskSpaceMb = -1;
		private DateTime sessionStartedAt;
		private DateTime sessionEndedAt;
		private PeHeaderReader.PeInfo currentPeInfo;

		// Last generated report text, kept for the Copy button.
		private string lastFinalAnalysisText;
		private bool darkModeEnabled;

		// Hang detection state.
		private HangDetector hangDetector;
		private volatile bool hangDetected;
		private DateTime hangStartTime;
		private int hangCount;

		// Windows Event Log entries captured around the crash.
		private List<WindowsEventEntry> relatedSystemEvents = new List<WindowsEventEntry>();

		private bool steamApiLoaded;
		private bool steamClientRunning;

		// Crash-site memory and disassembly capture.
		private string crashRegionState;
		private string crashRegionProtect;
		private string crashRegionType;
		private string crashRegionDescription;
		private long crashRegionBase;
		private long crashRegionSize;
		private string crashSiteHexDump;
		private string crashSitePattern;
		private string crashSiteMemoryFilePath;
		private string faultingInstructionText;
		private string faultingInstructionNote;
		private int faultingInstructionLength = -1;
		private List<string> sectionEntropyLines = new List<string>();
		private List<string> companionLogContents = new List<string>();
		private List<string> threadSnapshotLines = new List<string>();
		private List<string> interestingStrings = new List<string>();
		private string miniDisassembly;
		private string miniDisassemblyEngine;

		// Registers, stack, engine, hooks, environment, WER, manifest.
		private List<string> registerLines = new List<string>();
		private string stackHexDump;
		private long stackRegionBase;
		private long stackRegionSize;
		private string detectedEngine;
		private List<string> apiHooksDetected = new List<string>();
		private List<string> processEnvironment = new List<string>();
		private List<string> werReportLines = new List<string>();
		private string manifestContent;

		// Watchdog: forces the debug loop to end if it gets stuck.
		private volatile int lastActivityTick;
		private Thread watchdogThread;
		private volatile bool watchdogFired;
		private const int WatchdogTimeoutMs = 60000;
		private const int HangForceExitSeconds = 90;
		private const int WatchdogCheckIntervalMs = 1000;

		// Prevents duplicate "missing DLL" lines in the log.
		private HashSet<string> reportedMissingDlls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		#endregion

		public MainForm()
		{
			InitializeComponent();

			// Configure the log list view and load the saved theme.
			listView1.View = View.Details;
			listView1.FullRowSelect = true;
			listView1.HeaderStyle = ColumnHeaderStyle.None;
			listView1.Columns.Add("Log", listView1.Width - 25);

			darkModeEnabled = AppSettings.LoadDarkMode();
			ApplyTheme();

			UpdateStatus("Idle");
		}

		// Closes any auxiliary windows before the main form closes.
		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			try
			{
				if (activeDllScanForm != null && !activeDllScanForm.IsDisposed)
				{
					try { activeDllScanForm.Close(); } catch { }
				}
			}
			catch { }

			try
			{
				if (activeCanIRunItForm != null && !activeCanIRunItForm.IsDisposed)
				{
					try { activeCanIRunItForm.Close(); } catch { }
				}
			}
			catch { }

			try
			{
				if (activeAboutForm != null && !activeAboutForm.IsDisposed)
				{
					try { activeAboutForm.Close(); } catch { }
				}
			}
			catch { }

			base.OnFormClosing(e);
		}

		// Lets a caller (e.g. command line or another window) preload a target.
		public void LoadInitialTarget(string path)
		{
			try
			{
				if (!string.IsNullOrEmpty(path) && File.Exists(path))
					SetTargetFile(path);
			}
			catch { }
		}

		#region UI Event Handlers

		// Browse button: pick an EXE.
		void Button1Click(object sender, EventArgs e)
		{
			OpenFileDialog dlg = new OpenFileDialog();
			dlg.Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*";
			dlg.Title = "Select the program to test";
			if (dlg.ShowDialog() == DialogResult.OK)
			{
				SetTargetFile(dlg.FileName);
			}
		}

		// Drag-over: only accept EXE or LNK that resolves to an EXE.
		void MainFormDragEnter(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
				if (files.Length > 0)
				{
					string resolved = ResolveDropTarget(files[0]);
					if (resolved != null)
					{
						e.Effect = DragDropEffects.Copy;
						return;
					}
				}
			}
			e.Effect = DragDropEffects.None;
		}

		// Drop handler: resolve and load the target.
		void MainFormDragDrop(object sender, DragEventArgs e)
		{
			string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
			if (files.Length > 0)
			{
				string resolved = ResolveDropTarget(files[0]);
				if (resolved != null)
					SetTargetFile(resolved);
			}
		}

		// Accepts EXE directly; resolves LNK via WScript.Shell.
		private string ResolveDropTarget(string path)
		{
			if (string.IsNullOrEmpty(path)) return null;
			string lower = path.ToLowerInvariant();
			if (lower.EndsWith(".exe")) return path;
			if (lower.EndsWith(".lnk"))
			{
				string resolved = ResolveShortcut(path);
				if (!string.IsNullOrEmpty(resolved) && resolved.ToLowerInvariant().EndsWith(".exe"))
					return resolved;
			}
			return null;
		}

		// Resolves a .lnk to its target path using the Windows Script Host.
		// COM objects are released in finally, no leaks.
		private string ResolveShortcut(string shortcutPath)
		{
			object shell = null;
			object shortcut = null;
			try
			{
				Type shellType = Type.GetTypeFromProgID("WScript.Shell");
				if (shellType == null) return null;

				shell = Activator.CreateInstance(shellType);
				shortcut = shellType.InvokeMember("CreateShortcut",
					System.Reflection.BindingFlags.InvokeMethod, null, shell,
					new object[] { shortcutPath });

				if (shortcut == null) return null;

				object target = shortcut.GetType().InvokeMember("TargetPath",
					System.Reflection.BindingFlags.GetProperty, null, shortcut, null);

				return target != null ? target.ToString() : null;
			}
			catch
			{
				return null;
			}
			finally
			{
				if (shortcut != null) { try { Marshal.ReleaseComObject(shortcut); } catch { } }
				if (shell != null) { try { Marshal.ReleaseComObject(shell); } catch { } }
			}
		}

		// Opens the DLL scanner as a singleton window.
		void ButtonDllScanClick(object sender, EventArgs e)
		{
			if (activeDllScanForm != null && !activeDllScanForm.IsDisposed)
			{
				try
				{
					if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
						activeDllScanForm.SetInitialTarget(targetPath);
					activeDllScanForm.Activate();
				}
				catch { }
				return;
			}

			activeDllScanForm = new DllScanForm();
			if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
			{
				activeDllScanForm.SetInitialTarget(targetPath);
			}

			activeDllScanForm.Show();
		}

		// Opens the readiness analyzer as a singleton window.
		void ButtonCanIRunItClick(object sender, EventArgs e)
		{
			if (activeCanIRunItForm != null && !activeCanIRunItForm.IsDisposed)
			{
				try
				{
					if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
						activeCanIRunItForm.SetInitialTarget(targetPath);
					activeCanIRunItForm.Activate();
				}
				catch { }
				return;
			}

			activeCanIRunItForm = new Can_I_Run_It();
			if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
			{
				activeCanIRunItForm.SetInitialTarget(targetPath);
			}

			activeCanIRunItForm.Show();
		}

		// Menu handlers just forward to the corresponding button handlers.
		void MenuOpenClick(object sender, EventArgs e) { Button1Click(null, null); }
		void MenuExitClick(object sender, EventArgs e) { this.Close(); }
		void MenuDllScanClick(object sender, EventArgs e) { ButtonDllScanClick(null, null); }
		void MenuCanIRunItClick(object sender, EventArgs e) { ButtonCanIRunItClick(null, null); }
		void MenuCopyClick(object sender, EventArgs e) { ButtonCopyClick(null, null); }
		void MenuOpenFolderClick(object sender, EventArgs e) { ButtonOpenFolderClick(null, null); }

		// About window is a singleton too.
		void MenuAboutClick(object sender, EventArgs e)
		{
			if (activeAboutForm != null && !activeAboutForm.IsDisposed)
			{
				try { activeAboutForm.Activate(); } catch { }
				return;
			}

			activeAboutForm = new AboutForm();
			activeAboutForm.Show();
		}

		// Resets all state, resolves file metadata and logs a quick summary.
		private void SetTargetFile(string path)
		{
			targetPath = path;
			listView1.Items.Clear();
			allLogLines.Clear();
			UpdateFileInfoPanel(path);
			signatureInfo = GetSignatureInfo(path);
			compatibilityFlagsInfo = GetCompatibilityFlags(path);
			peChecksumWarning = PeChecksumValidator.Validate(path);
			sha256Hash = null;
			AppendToUI("Selected file: " + targetPath);

			PeHeaderReader.PeInfo pe = PeHeaderReader.GetBasicInfo(path);
			if (pe.Success)
			{
				AppendToUI("[Info] Processor: " + pe.MachineName + ", Subsystem: " + pe.SubsystemName +
					" " + pe.SubsystemMajor + "." + pe.SubsystemMinor);
				AppendToUI("[Info] Entry point RVA: 0x" + pe.EntryPointRva.ToString("X8") +
					", Sections: " + pe.NumberOfSections +
					", Managed: " + (pe.IsDotNet ? "yes" : "no"));
				if (!string.IsNullOrEmpty(pe.PackedHint))
				{
					AppendToUI("[Warning] Possible packer: " + pe.PackedHint);
				}
			}

			if (peChecksumWarning != null)
			{
				AppendToUI("[Warning] " + peChecksumWarning);
			}
		}

		// Reads the Authenticode signature, if any.
		private string GetSignatureInfo(string path)
		{
			try
			{
				X509Certificate cert = X509Certificate.CreateFromSignedFile(path);
				return "Digitally signed by: " + cert.Subject;
			}
			catch
			{
				return "Not digitally signed (or signature could not be verified)";
			}
		}

		// Reads the per-user compatibility flags from the registry.
		private string GetCompatibilityFlags(string path)
		{
			try
			{
				object value = Registry.GetValue(
					"HKEY_CURRENT_USER\\Software\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers",
					path, null);
				if (value != null) return value.ToString();
			}
			catch
			{
			}
			return null;
		}

		// Updates the icon and small info text panel.
		private void UpdateFileInfoPanel(string path)
		{
			try
			{
				if (pictureBoxIcon.Image != null)
				{
					pictureBoxIcon.Image.Dispose();
					pictureBoxIcon.Image = null;
				}
				Icon ico = Icon.ExtractAssociatedIcon(path);
				if (ico != null) pictureBoxIcon.Image = ico.ToBitmap();
			}
			catch
			{
			}

			try
			{
				FileInfo fi = new FileInfo(path);
				FileVersionInfo vi = FileVersionInfo.GetVersionInfo(path);
				string company = string.IsNullOrEmpty(vi.CompanyName) ? "Unknown publisher" : vi.CompanyName;
				string version = string.IsNullOrEmpty(vi.FileVersion) ? "n/a" : vi.FileVersion;
				double sizeMb = fi.Length / 1024.0 / 1024.0;
				labelFileInfo.Text = fi.Name + "  (" + sizeMb.ToString("0.0") + " MB)\n" +
					company + " - v" + version + " - modified " + fi.LastWriteTime.ToString("yyyy-MM-dd");
			}
			catch
			{
				labelFileInfo.Text = Path.GetFileName(path);
			}
		}

		// Starts a monitored run of the target.
		void Button2Click(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(targetPath))
			{
				MessageBox.Show("Please select a file first.", "Notice");
				return;
			}

			if (debugThread != null && debugThread.IsAlive)
			{
				MessageBox.Show("Monitoring is already running.", "Notice");
				return;
			}

			ResetState();
			UpdateStatus("Monitoring");
			progressBar1.MarqueeAnimationSpeed = 30;
			PlaySound(SystemSounds.Asterisk);

			// Report goes next to the target, with a timestamped name.
			string dir = Path.GetDirectoryName(targetPath);
			string baseName = Path.GetFileNameWithoutExtension(targetPath);
			string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
			reportFilePath = Path.Combine(dir, baseName + "_" + timestamp + ".txt");

			debugThread = new Thread(DebugLoop);
			debugThread.IsBackground = true;
			debugThread.Name = "DebugLoop";
			debugThread.Start();
		}

		// Stop button: try a graceful close, then force kill after a delay.
		void Button3Click(object sender, EventArgs e)
		{
			if (debugThread == null || !debugThread.IsAlive) return;

			userForcedStop = true;

			bool askedNicely = TryRequestGracefulClose();

			if (askedNicely)
			{
				Thread gracePeriod = new Thread(GraceKillAfterDelay);
				gracePeriod.IsBackground = true;
				gracePeriod.Start();
			}
			else
			{
				stopRequested = true;
				if (hProcess != IntPtr.Zero)
				{
					try { NativeMethods.TerminateProcess(hProcess, 1); } catch { }
				}
			}
		}

		// Toggles the theme and persists it.
		void ButtonDarkModeClick(object sender, EventArgs e)
		{
			darkModeEnabled = !darkModeEnabled;
			AppSettings.SaveDarkMode(darkModeEnabled);
			ApplyTheme();
		}

		// Copies the last report text to the clipboard.
		void ButtonCopyClick(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(lastFinalAnalysisText))
			{
				MessageBox.Show("No analysis available yet. Run a test first.", "Notice");
				return;
			}
			try
			{
				Clipboard.SetText(lastFinalAnalysisText);
				MessageBox.Show("Analysis copied to clipboard.", "Copied");
			}
			catch
			{
			}
		}

		// Opens Explorer with the report file selected.
		void ButtonOpenFolderClick(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(reportFilePath) || !File.Exists(reportFilePath))
			{
				MessageBox.Show("No report file yet. Run a test first.", "Notice");
				return;
			}
			try
			{
				Process.Start("explorer.exe", "/select,\"" + reportFilePath + "\"");
			}
			catch
			{
			}
		}

		void TextBoxSearchTextChanged(object sender, EventArgs e)
		{
			ApplySearchFilter();
		}

		// Rebuilds the log list view using the search filter.
		private void ApplySearchFilter()
		{
			string filter = textBoxSearch.Text;
			listView1.BeginUpdate();
			listView1.Items.Clear();
			for (int i = 0; i < allLogLines.Count; i++)
			{
				string line = allLogLines[i];
				if (string.IsNullOrEmpty(filter) || line.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					AddColoredItem(line);
				}
			}
			listView1.EndUpdate();
			if (listView1.Items.Count > 0)
			{
				listView1.EnsureVisible(listView1.Items.Count - 1);
			}
		}

		// Waits 3 seconds after a graceful close request, then force-kills.
		private void GraceKillAfterDelay()
		{
			Thread.Sleep(3000);
			stopRequested = true;
			if (hProcess != IntPtr.Zero)
			{
				try { NativeMethods.TerminateProcess(hProcess, 1); } catch { }
			}
		}

		// Sends WM_CLOSE to every top-level window of the target.
		private bool TryRequestGracefulClose()
		{
			bool found = false;
			uint targetPid = currentPid;

			try
			{
				NativeMethods.EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
				{
					uint pid;
					NativeMethods.GetWindowThreadProcessId(hWnd, out pid);
					if (pid == targetPid)
					{
						NativeMethods.PostMessage(hWnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
						found = true;
					}
					return true;
				}, IntPtr.Zero);
			}
			catch
			{
			}

			return found;
		}

		void ListView1SelectedIndexChanged(object sender, EventArgs e)
		{
		}

		// Clears every per-session field before a new run.
		private void ResetState()
		{
			stopRequested = false;
			userForcedStop = false;
			wasFatalCrash = false;
			wasSilentExit = false;
			finalStatusCode = 0;
			crashAddress = IntPtr.Zero;
			crashModuleName = "Unknown";
			crashModuleBase = IntPtr.Zero;
			crashModuleOffset = 0;
			haveAccessInfo = false;
			accessType = -1;
			accessedAddress = IntPtr.Zero;
			loadedModuleNames.Clear();
			suspiciousModulesFound.Clear();
			antiCheatModulesFound.Clear();
			recentEvents.Clear();
			capturedCallStack.Clear();
			symbolDiagnosticNote = null;
			symbolsWereResolved = false;
			isTarget32Bit = false;
			isTarget64Bit = false;
			symbolsInitialized = false;
			haveLastFirstChance = false;
			lastFirstChanceCode = 0;
			lastFirstChanceAddress = IntPtr.Zero;
			managedExceptionType = null;
			managedExceptionMessage = null;
			managedStackTraceLines.Clear();
			crashWorkingSetBytes = -1;
			crashHandleCount = -1;
			crashGdiObjects = -1;
			crashUserObjects = -1;
			crashAvailableSystemMemoryMb = -1;
			crashFreeDiskSpaceMb = -1;
			crashScreenshotPath = null;
			crashMiniDumpPath = null;
			confirmedMissingDlls.Clear();
			indirectMissingDlls.Clear();
			probablyFineDlls.Clear();
			redistributableSuggestions.Clear();
			lastFinalAnalysisText = null;
			hangDetected = false;
			hangStartTime = default(DateTime);
			hangCount = 0;
			relatedSystemEvents.Clear();
			steamApiLoaded = false;
			steamClientRunning = false;
			sessionEndedAt = default(DateTime);

			crashRegionState = null;
			crashRegionProtect = null;
			crashRegionType = null;
			crashRegionDescription = null;
			crashRegionBase = 0;
			crashRegionSize = 0;
			crashSiteHexDump = null;
			crashSitePattern = null;
			crashSiteMemoryFilePath = null;
			faultingInstructionText = null;
			faultingInstructionNote = null;
			faultingInstructionLength = -1;
			sectionEntropyLines.Clear();
			companionLogContents.Clear();
			threadSnapshotLines.Clear();
			interestingStrings.Clear();
			miniDisassembly = null;
			miniDisassemblyEngine = null;

			registerLines.Clear();
			stackHexDump = null;
			stackRegionBase = 0;
			stackRegionSize = 0;
			detectedEngine = null;
			apiHooksDetected.Clear();
			processEnvironment.Clear();
			werReportLines.Clear();
			manifestContent = null;

			lastActivityTick = Environment.TickCount;
			watchdogFired = false;
			watchdogThread = null;

			reportedMissingDlls.Clear();
		}

		#endregion

		#region Theming / Status / Sound

		// Applies the current theme to every control on the form and pushes
		// the change to any open auxiliary windows.
		private void ApplyTheme()
		{
			Color formBack, panelBack, logBack, text, border;

			if (darkModeEnabled)
			{
				formBack = Color.FromArgb(18, 18, 18);
				panelBack = Color.FromArgb(30, 30, 30);
				logBack = Color.FromArgb(24, 24, 24);
				text = Color.FromArgb(224, 224, 224);
				border = Color.FromArgb(60, 60, 60);
			}
			else
			{
				formBack = Color.White;
				panelBack = Color.FromArgb(240, 240, 240);
				logBack = Color.White;
				text = Color.FromArgb(26, 26, 26);
				border = Color.FromArgb(200, 200, 200);
			}

			this.BackColor = formBack;
			this.ForeColor = text;

			listView1.BackColor = logBack;
			listView1.ForeColor = text;

			textBoxSearch.BackColor = panelBack;
			textBoxSearch.ForeColor = text;

			labelFileInfo.ForeColor = text;
			labelStatus.ForeColor = text;
			pictureBoxIcon.BackColor = formBack;

			// Menu strip uses a custom renderer so colors apply on every
			// Windows version.
			try
			{
				Color menuBack = panelBack;
				Color menuText = text;
				Color menuHover = darkModeEnabled
					? Color.FromArgb(60, 60, 60)
					: Color.FromArgb(200, 200, 200);
				Color menuBorder = border;

				menuStripMain.Renderer = new ThemedMenuRenderer(menuBack, menuText, menuHover, menuBorder);
				menuStripMain.BackColor = menuBack;
				menuStripMain.ForeColor = menuText;

				foreach (ToolStripItem item in menuStripMain.Items)
				{
					item.ForeColor = menuText;
					item.BackColor = menuBack;
					if (item is ToolStripMenuItem)
					{
						ToolStripMenuItem mi = (ToolStripMenuItem)item;
						foreach (ToolStripItem sub in mi.DropDownItems)
						{
							sub.ForeColor = menuText;
							sub.BackColor = menuBack;
						}
					}
				}
			}
			catch { }

			// Uniform flat styling for every button.
			Button[] allButtons = new Button[] { button1, button2, button3, buttonDarkMode, buttonCopy, buttonOpenFolder, buttonDllScan, buttonCanIRunIt };
			foreach (Button b in allButtons)
			{
				b.UseVisualStyleBackColor = false;
				b.FlatStyle = FlatStyle.Flat;
				b.FlatAppearance.BorderColor = border;
				b.BackColor = panelBack;
				b.ForeColor = text;
			}

			buttonDarkMode.Text = darkModeEnabled ? "Light Mode" : "Dark Mode";

			// Recolor existing log lines according to their content.
			foreach (ListViewItem item in listView1.Items)
			{
				item.ForeColor = GetLineColor(item.Text);
			}

			IsDarkMode = darkModeEnabled;
			try { if (ThemeChanged != null) ThemeChanged(); } catch { }

			try
			{
				if (activeDllScanForm != null && !activeDllScanForm.IsDisposed)
					activeDllScanForm.RefreshTheme();
			}
			catch { }

			try
			{
				if (activeCanIRunItForm != null && !activeCanIRunItForm.IsDisposed)
					activeCanIRunItForm.RefreshTheme();
			}
			catch { }

			try
			{
				if (activeAboutForm != null && !activeAboutForm.IsDisposed)
					activeAboutForm.RefreshTheme();
			}
			catch { }
		}

		// Thread-safe status label update.
		private void UpdateStatus(string status)
		{
			if (this.InvokeRequired)
			{
				try { this.BeginInvoke(new MethodInvoker(delegate { UpdateStatus(status); })); }
				catch { }
				return;
			}

			try
			{
				labelStatus.Text = "Status: " + status;

				if (status == "Monitoring") progressBar1.MarqueeAnimationSpeed = 30;
				else progressBar1.MarqueeAnimationSpeed = 0;
			}
			catch
			{
			}
		}

		private void PlaySound(SystemSound sound)
		{
			try { sound.Play(); } catch { }
		}

		#endregion

		#region Watchdog

		// Starts the watchdog thread. It uses two independent triggers:
		// a hang timer and a no-debug-activity timer.
		private void StartWatchdog()
		{
			try
			{
				watchdogFired = false;
				lastActivityTick = Environment.TickCount;
				watchdogThread = new Thread(WatchdogLoop);
				watchdogThread.IsBackground = true;
				watchdogThread.Name = "CrashWatchdog";
				watchdogThread.Start();
			}
			catch
			{
				watchdogThread = null;
			}
		}

		// Signals the watchdog to stop and joins it briefly.
		private void StopWatchdog()
		{
			try
			{
				if (watchdogThread != null && watchdogThread.IsAlive)
				{
					watchdogThread.Join(2000);
				}
			}
			catch
			{
			}
			watchdogThread = null;
		}

		// Main watchdog loop.
		private void WatchdogLoop()
		{
			while (!stopRequested && !watchdogFired)
			{
				try { Thread.Sleep(WatchdogCheckIntervalMs); }
				catch { break; }

				if (userForcedStop) break;
				if (stopRequested) break;

				try { PopulateDependencyAnalysis(); }
				catch { }

				try
				{
					// Trigger 1: the target has been hung for too long.
					if (hangDetected && !stopRequested && !userForcedStop)
					{
						double hangSeconds = (DateTime.Now - hangStartTime).TotalSeconds;
						if (hangSeconds > HangForceExitSeconds)
						{
							AppendLogSafe("[Watchdog] Target has been unresponsive for " +
								hangSeconds.ToString("0") + " seconds. " +
								"Forcing exit so the report can be generated.");

							try { CaptureHangSnapshot(); } catch { }

							watchdogFired = true;
							stopRequested = true;
							try { NativeMethods.TerminateProcess(hProcess, 0); } catch { }
							break;
						}
					}

					// If the target is already gone, wait a bit then force
					// the debug loop out. This covers cases where the debug
					// event never arrives.
					bool targetAlive = IsTargetProcessAlive();
					if (!targetAlive)
					{
						Thread.Sleep(1500);

						if (!stopRequested)
						{
							AppendLogSafe("[Watchdog] Target process exited but the debug loop did not stop. " +
								"Forcing exit so the report can be generated.");
							watchdogFired = true;
							stopRequested = true;
							try { NativeMethods.TerminateProcess(hProcess, 0); } catch { }
							break;
						}
						continue;
					}

					// Trigger 2: no debug activity for the timeout window.
					int elapsed = unchecked(Environment.TickCount - lastActivityTick);
					if (elapsed < 0) elapsed = 0;

					if (elapsed > WatchdogTimeoutMs && !hangDetected)
					{
						AppendLogSafe("[Watchdog] No debug activity for " +
							(WatchdogTimeoutMs / 1000) + " seconds and the target is not " +
							"detected as hung. Forcing exit so the report can be generated.");

						try { CaptureHangSnapshot(); } catch { }

						watchdogFired = true;
						stopRequested = true;
						try { NativeMethods.TerminateProcess(hProcess, 1); } catch { }
						break;
					}
				}
				catch
				{
				}
			}
		}

		// Collects thread states and short stacks for the hang report.
		private void CaptureHangSnapshot()
		{
			try
			{
				threadSnapshotLines.Clear();
				List<ThreadAnalyzer.ThreadSnapshot> threads = ThreadAnalyzer.Snapshot(currentPid);
				threadSnapshotLines.Add("  Total threads at hang: " + threads.Count);
				foreach (ThreadAnalyzer.ThreadSnapshot t in threads)
				{
					threadSnapshotLines.Add(
						"  TID " + t.ThreadId.ToString().PadRight(8) +
						" state=" + (t.State ?? "?").PadRight(12) +
						" wait=" + (t.WaitReason ?? "?").PadRight(18) +
						" prio=" + (t.PriorityName ?? "?").PadRight(12) +
						" cpu=" + t.TotalProcessorTimeMs + "ms");
				}

				// Also capture a short stack for each thread.
				foreach (ThreadAnalyzer.ThreadSnapshot t in threads)
				{
					try
					{
						List<string> frames = CaptureCallStack((uint)t.ThreadId);
						if (frames != null && frames.Count > 0)
						{
							threadSnapshotLines.Add("  --- Stack of TID " + t.ThreadId + " ---");
							for (int i = 0; i < frames.Count && i < 15; i++)
								threadSnapshotLines.Add("    #" + i + " " + frames[i]);
						}
					}
					catch { }
				}
			}
			catch { }
		}

		// Non-invasive check: the process is alive if GetExitCodeProcess
		// returns STILL_ACTIVE.
		private bool IsTargetProcessAlive()
		{
			try
			{
				if (hProcess == IntPtr.Zero) return false;
				uint exitCode;
				if (!NativeMethods.GetExitCodeProcess(hProcess, out exitCode)) return false;
				return exitCode == NativeMethods.STILL_ACTIVE;
			}
			catch
			{
				return false;
			}
		}

		// Thread-safe log append used from the watchdog and debug loop.
		private void AppendLogSafe(string text)
		{
			try
			{
				string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text;
				try { recentEvents.Add(line); } catch { }
				if (recentEvents.Count > RECENT_EVENTS_LIMIT) recentEvents.RemoveAt(0);
				try { allLogLines.Add(line); } catch { }

				try
				{
					if (this.InvokeRequired)
						this.BeginInvoke(new MethodInvoker(delegate { AppendToUI(line); }));
					else
						AppendToUI(line);
				}
				catch { }
			}
			catch
			{
			}
		}

		#endregion

		#region Debug Loop

		// Main debug loop. Starts the target under DEBUG_ONLY_THIS_PROCESS,
		// pumps debug events, and on exit gathers the remaining evidence
		// and writes the report.
		private void DebugLoop()
		{
			IntPtr debugEventBuffer = IntPtr.Zero;
			bool processCreated = false;

			try
			{
				NativeMethods.STARTUPINFO si = new NativeMethods.STARTUPINFO();
				si.cb = Marshal.SizeOf(si);
				NativeMethods.PROCESS_INFORMATION pi;

				string workingDir = Path.GetDirectoryName(targetPath);

				// Read PE info before launching so we can log architecture
				// and packed status up front.
				PeHeaderReader.PeInfo peInfo = PeHeaderReader.GetBasicInfo(targetPath);
				currentPeInfo = peInfo;

				if (peInfo.Success)
				{
					LogLine("[Info] Target architecture: " + peInfo.MachineName +
						", Subsystem: " + peInfo.SubsystemName + " " +
						peInfo.SubsystemMajor + "." + peInfo.SubsystemMinor);
					LogLine("[Info] Sections: " + peInfo.NumberOfSections +
						", Entry point: 0x" + peInfo.EntryPointRva.ToString("X8"));
					LogLine("[Info] DLL characteristics: " + peInfo.DllCharacteristicsFlags);
					LogLine("[Info] Managed (.NET): " + (peInfo.IsDotNet ? "Yes" : "No") +
						", TLS: " + (peInfo.HasTls ? "Yes" : "No") +
						", Relocations: " + (peInfo.HasRelocations ? "Yes" : "No"));

					if (!string.IsNullOrEmpty(peInfo.PackedHint))
					{
						LogLine("[Warning] Executable appears packed/protected: " + peInfo.PackedHint);
					}
					if (peInfo.IsDotNet)
					{
						LogLine("[Info] This is a managed .NET assembly - many native-crash diagnostics do not apply.");
					}

					if (peInfo.Machine == 0x8664 && !Environment.Is64BitOperatingSystem)
					{
						LogLine("[Warning] This is a 64-bit program, but this Windows installation is 32-bit. It cannot run here at all.");
					}
				}

				bool created = NativeMethods.CreateProcess(
					targetPath, null, IntPtr.Zero, IntPtr.Zero, false,
					NativeMethods.DEBUG_ONLY_THIS_PROCESS, IntPtr.Zero, workingDir,
					ref si, out pi);

				if (!created)
				{
					int err = Marshal.GetLastWin32Error();
					LogLine("[Error] Failed to start the process. Windows error code: " + err +
						" (" + ExplainWin32Error(err) + ")");
					return;
				}

				processCreated = true;
				hProcess = pi.hProcess;
				hThread = pi.hThread;
				currentPid = pi.dwProcessId;
				sessionStartedAt = DateTime.Now;

				isTarget32Bit = peInfo.Success && peInfo.Machine == 0x014c;
				isTarget64Bit = peInfo.Success && peInfo.Machine == 0x8664;

				// Kill the target if this debugger exits unexpectedly, so
				// we do not leave orphans.
				NativeMethods.DebugSetProcessKillOnExit(true);

				InitializeSymbols();
				StartHangDetector();
				StartWatchdog();

				try { PopulateDependencyAnalysis(); } catch { }

				LogLine("[Info] Monitoring started for: " + targetPath + " (PID " + currentPid + ")");

				debugEventBuffer = Marshal.AllocHGlobal(NativeMethods.DEBUG_EVENT_BUFFER_SIZE);

				while (!stopRequested && !watchdogFired)
				{
					bool got;
					try
					{
						// Short timeout so we can also notice hangs and
						// the stop flag.
						got = NativeMethods.WaitForDebugEvent(debugEventBuffer, 200);
					}
					catch
					{
						got = false;
					}

					if (got)
					{
						lastActivityTick = Environment.TickCount;
					}

					// Check the hang timer even when no debug event arrives.
					if (hangDetected && !stopRequested && !userForcedStop)
					{
						try
						{
							double hangSeconds = (DateTime.Now - hangStartTime).TotalSeconds;
							if (hangSeconds > HangForceExitSeconds)
							{
								AppendLogSafe("[DebugLoop] Target hung for " +
									hangSeconds.ToString("0") + " seconds. Forcing exit.");

								try { CaptureHangSnapshot(); } catch { }

								stopRequested = true;
								watchdogFired = true;
								try { NativeMethods.TerminateProcess(hProcess, 0); } catch { }
								break;
							}
						}
						catch { }
					}

					if (!got) continue;

					try
					{
						// Manually read the DEBUG_EVENT header (it is a
						// fixed-layout struct starting with these fields).
						uint eventCode = unchecked((uint)Marshal.ReadInt32(debugEventBuffer, 0));
						uint pid = unchecked((uint)Marshal.ReadInt32(debugEventBuffer, 4));
						uint tid = unchecked((uint)Marshal.ReadInt32(debugEventBuffer, 8));
						uint continueStatus = NativeMethods.DBG_CONTINUE;

						HandleDebugEvent(eventCode, tid, debugEventBuffer, ref continueStatus);

						NativeMethods.ContinueDebugEvent(pid, tid, continueStatus);
					}
					catch (Exception loopEx)
					{
						AppendLogSafe("[Error] Exception while handling a debug event: " + loopEx.Message);
					}
				}
			}
			catch (Exception ex)
			{
				LogLine("[Error] Unexpected internal error in the monitoring tool: " + ex.Message);
			}
			finally
			{
				// Orderly cleanup, in the same order resources were acquired.
				try { StopWatchdog(); } catch { }

				try
				{
					if (hangDetector != null)
					{
						hangDetector.Stop();
						hangDetector = null;
					}
				}
				catch { }

				try
				{
					if (debugEventBuffer != IntPtr.Zero)
					{
						Marshal.FreeHGlobal(debugEventBuffer);
					}
				}
				catch { }

				try
				{
					if (hThread != IntPtr.Zero)
					{
						NativeMethods.CloseHandle(hThread);
						hThread = IntPtr.Zero;
					}
				}
				catch { }

				try
				{
					if (symbolsInitialized && hProcess != IntPtr.Zero)
					{
						NativeMethods.SymCleanup(hProcess);
						symbolsInitialized = false;
					}
				}
				catch { }

				// MiniDump is written before we close the process handle.
				try
				{
					if (wasFatalCrash && hProcess != IntPtr.Zero)
					{
						TryWriteMiniDump();
					}
				}
				catch { }

				try
				{
					if (hProcess != IntPtr.Zero)
					{
						NativeMethods.CloseHandle(hProcess);
						hProcess = IntPtr.Zero;
					}
				}
				catch { }

				if (processCreated)
				{
					// All the late-stage analyzers. Each one is wrapped so a
					// failure in one does not stop the others.
					try { TryFetchEventLogEntries(); } catch { }
					try { AnalyzeSectionEntropy(); } catch { }
					try { AnalyzeCompanionLogs(); } catch { }
					try { AnalyzeThreads(); } catch { }
					try { AnalyzeInterestingStrings(); } catch { }
					try { AnalyzeEngine(); } catch { }
					try { AnalyzeApiHooks(); } catch { }
					try { AnalyzeProcessEnvironment(); } catch { }
					try { AnalyzeWerReports(); } catch { }
					try { AnalyzeManifest(); } catch { }

					try { PopulateDependencyAnalysis(); } catch { }

					if (watchdogFired)
					{
						AppendLogSafe("[Info] The watchdog forced an exit from the debug loop. " +
							"Generating the report with the data collected so far.");
					}

					LogLine("[Info] Monitoring stopped.");
					try { WriteFinalAnalysis(); } catch { }
				}

				// Final status/sound feedback to the user.
				if (wasFatalCrash)
				{
					UpdateStatus("Crashed");
					PlaySound(SystemSounds.Hand);
				}
				else if (wasSilentExit)
				{
					UpdateStatus("Silent Exit");
					PlaySound(SystemSounds.Exclamation);
				}
				else if (hangDetected)
				{
					UpdateStatus("Hang Detected");
					PlaySound(SystemSounds.Exclamation);
				}
				else if (userForcedStop)
				{
					UpdateStatus("Stopped");
					PlaySound(SystemSounds.Beep);
				}
				else
				{
					UpdateStatus("Finished");
					PlaySound(SystemSounds.Asterisk);
				}
			}
		}

		// Initializes DbgHelp with an offline-only symbol path. No network
		// access; only local folders and any user-specified local path.
		private void InitializeSymbols()
		{
			try
			{
				List<string> localPaths = new List<string>();

				try
				{
					string exeDir = Path.GetDirectoryName(
						System.Reflection.Assembly.GetExecutingAssembly().Location);
					if (!string.IsNullOrEmpty(exeDir))
					{
						localPaths.Add(Path.Combine(exeDir, "symbols"));
					}
				}
				catch { }

				try
				{
					string cacheDir = Path.Combine(
						Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
						"TestAppSymbols");
					try
					{
						if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);
					}
					catch { }
					localPaths.Add(cacheDir);
				}
				catch { }

				try
				{
					string programData = Environment.GetFolderPath(
						Environment.SpecialFolder.CommonApplicationData);
					if (!string.IsNullOrEmpty(programData))
					{
						localPaths.Add(Path.Combine(
							programData, "Microsoft", "Windows", "Debug", "Symbols"));
					}
				}
				catch { }

				try { localPaths.Add(@"C:\Symbols"); }
				catch { }

				// Reuse any user-provided paths that are NOT remote or a
				// Microsoft symbol-server alias.
				try
				{
					string userPath = Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH");
					if (!string.IsNullOrEmpty(userPath))
					{
						string[] parts = userPath.Split(';');
						foreach (string part in parts)
						{
							string trimmed = part.Trim();
							if (trimmed.Length == 0) continue;
							if (trimmed.IndexOf("srv*", StringComparison.OrdinalIgnoreCase) >= 0) continue;
							if (trimmed.IndexOf("http://", StringComparison.OrdinalIgnoreCase) >= 0) continue;
							if (trimmed.IndexOf("https://", StringComparison.OrdinalIgnoreCase) >= 0) continue;
							localPaths.Add(trimmed);
						}
					}
				}
				catch { }

				string offlinePath = string.Join(";", localPaths.ToArray());
				symbolPathUsed = offlinePath;

				uint options = NativeMethods.SYMOPT_UNDNAME |
							   NativeMethods.SYMOPT_DEFERRED_LOADS |
							   NativeMethods.SYMOPT_LOAD_LINES;
				NativeMethods.SymSetOptions(options);

				symbolsInitialized = NativeMethods.SymInitialize(hProcess, offlinePath, true);
				if (!symbolsInitialized)
				{
					int symInitError = Marshal.GetLastWin32Error();
					LogLine("[Warning] Symbol engine (dbghelp.dll) failed to start (error " +
						symInitError + "). Function names will be limited; Module+Offset will still work.");
				}
				else
				{
					LogLine("[Info] Symbol engine initialized (offline mode only - no internet access).");
					LogLine("[Info] Search path: " + offlinePath);
				}
			}
			catch (Exception symEx)
			{
				symbolsInitialized = false;
				LogLine("[Warning] Symbol engine threw an exception while starting: " + symEx.Message);
			}
		}

		// Hooks the HangDetector callbacks into the log and status UI.
		private void StartHangDetector()
		{
			try
			{
				hangDetector = new HangDetector(
					delegate { return currentPid; },
					delegate(string msg)
					{
						hangDetected = true;
						if (hangStartTime == default(DateTime))
							hangStartTime = DateTime.Now;
						hangCount++;
						LogLine("[Hang] " + msg);
						try { UpdateStatus("Target Hung"); } catch { }
					},
					delegate(string msg)
					{
						hangDetected = false;
						LogLine("[Hang] " + msg);
						try { UpdateStatus("Monitoring"); } catch { }
					});
				hangDetector.Start();
			}
			catch
			{
				hangDetector = null;
			}
		}

		// Dispatches one debug event to the matching handler.
		private void HandleDebugEvent(uint eventCode, uint tid, IntPtr buf, ref uint continueStatus)
		{
			switch (eventCode)
			{
				case NativeMethods.CREATE_PROCESS_DEBUG_EVENT: HandleCreateProcessEvent(buf); break;
				case NativeMethods.CREATE_THREAD_DEBUG_EVENT:
					LogLine("[Thread] New thread created (TID " + tid + ")");
					break;
				case NativeMethods.EXIT_THREAD_DEBUG_EVENT:
					int threadExitCode = 0;
					try { threadExitCode = Marshal.ReadInt32(buf, 16); } catch { }
					LogLine("[Thread] Thread " + tid + " exited (Exit code: " + threadExitCode + ")");
					break;
				case NativeMethods.EXIT_PROCESS_DEBUG_EVENT: HandleProcessExit(buf); break;
				case NativeMethods.LOAD_DLL_DEBUG_EVENT: HandleDllLoad(buf); break;
				case NativeMethods.UNLOAD_DLL_DEBUG_EVENT:
					// Remove from the module map and log.
					IntPtr unloadBase = IntPtr.Zero;
					try { unloadBase = Marshal.ReadIntPtr(buf, 16); } catch { }
					string unloadName;
					if (!loadedModuleNames.TryGetValue(unloadBase, out unloadName))
					{
						unloadName = "0x" + unloadBase.ToString("X");
					}
					LogLine("[DLL] Module unloaded: " + unloadName);
					loadedModuleNames.Remove(unloadBase);
					break;
				case NativeMethods.OUTPUT_DEBUG_STRING_EVENT:
					LogLine("[Debug] The program sent an internal debug string message.");
					break;
				case NativeMethods.EXCEPTION_DEBUG_EVENT: HandleException(buf, tid, ref continueStatus); break;
				default:
					LogLine("[Event] Debug event code " + eventCode);
					break;
			}
		}

		// Reads the image name from the process handle and adds it to the
		// module map.
		private void HandleCreateProcessEvent(IntPtr buf)
		{
			IntPtr fileHandle = IntPtr.Zero;
			IntPtr baseOfImage = IntPtr.Zero;
			try { fileHandle = Marshal.ReadIntPtr(buf, 16); } catch { }
			try { baseOfImage = Marshal.ReadIntPtr(buf, 40); } catch { }

			string resolvedName = targetPath;
			if (fileHandle != IntPtr.Zero)
			{
				try
				{
					StringBuilder pathBuffer = new StringBuilder(512);
					uint len = NativeMethods.GetFinalPathNameByHandle(fileHandle, pathBuffer, (uint)pathBuffer.Capacity, 0);
					if (len > 0 && len < pathBuffer.Capacity)
					{
						string full = pathBuffer.ToString();
						if (full.StartsWith("\\\\?\\")) full = full.Substring(4);
						resolvedName = full;
					}
					NativeMethods.CloseHandle(fileHandle);
				}
				catch { }
			}

			if (baseOfImage != IntPtr.Zero)
			{
				loadedModuleNames[baseOfImage] = resolvedName;
			}

			LogLine("[Process] Target process created successfully.");
		}

		// Adds a DLL to the module map, notes Steam/overlay/anti-cheat.
		private void HandleDllLoad(IntPtr buf)
		{
			IntPtr dllBase = IntPtr.Zero;
			IntPtr dllFileHandle = IntPtr.Zero;
			try { dllBase = Marshal.ReadIntPtr(buf, 24); } catch { }
			try { dllFileHandle = Marshal.ReadIntPtr(buf, 16); } catch { }

			string resolvedName = "0x" + dllBase.ToString("X");

			if (dllFileHandle != IntPtr.Zero)
			{
				try
				{
					StringBuilder pathBuffer = new StringBuilder(512);
					uint len = NativeMethods.GetFinalPathNameByHandle(dllFileHandle, pathBuffer, (uint)pathBuffer.Capacity, 0);
					if (len > 0 && len < pathBuffer.Capacity)
					{
						string full = pathBuffer.ToString();
						if (full.StartsWith("\\\\?\\")) full = full.Substring(4);
						resolvedName = full;
					}
					NativeMethods.CloseHandle(dllFileHandle);
				}
				catch { }
			}

			loadedModuleNames[dllBase] = resolvedName;

			string fileNameOnly = Path.GetFileName(resolvedName).ToLowerInvariant();

			if (fileNameOnly.IndexOf("steam_api", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				steamApiLoaded = true;
			}

			if (ModuleClassifier.MatchesAny(fileNameOnly, ModuleClassifier.SuspiciousModules))
			{
				if (!suspiciousModulesFound.Contains(fileNameOnly))
					suspiciousModulesFound.Add(fileNameOnly);
			}
			if (ModuleClassifier.MatchesAny(fileNameOnly, ModuleClassifier.AntiCheatModules))
			{
				if (!antiCheatModulesFound.Contains(fileNameOnly))
					antiCheatModulesFound.Add(fileNameOnly);
			}

			LogLine("[DLL] Loaded: " + resolvedName);
		}

		// Classifies the exit: user-stopped, abnormal exit code, silent
		// early exit, or normal close.
		private void HandleProcessExit(IntPtr buf)
		{
			int processExitCode = 0;
			try { processExitCode = Marshal.ReadInt32(buf, 16); } catch { }

			if (userForcedStop)
			{
				LogLine("[Process] Stopped manually by the user.");
			}
			else if (processExitCode != 0)
			{
				uint exitCodeUnsigned = unchecked((uint)processExitCode);
				finalStatusCode = exitCodeUnsigned;
				string exitMeaning = ExplainStatusCode(exitCodeUnsigned);
				LogLine("[Process - Abnormal Exit] Program terminated itself with code: 0x" +
					exitCodeUnsigned.ToString("X8") + " (" + exitMeaning + ")");
			}
			else
			{
				double seconds = 0;
				try { seconds = (DateTime.Now - sessionStartedAt).TotalSeconds; } catch { }

				// Exit code 0 but only a few seconds alive: almost always
				// a deliberate self-termination.
				if (seconds >= 0 && seconds < 10)
				{
					wasSilentExit = true;

					string note = "The program exited with code 0, but it only ran for " +
						seconds.ToString("0.0") + " second(s). A program that closes this quickly " +
						"with no error code has almost certainly terminated itself on purpose " +
						"(for example after detecting a missing asset, an invalid configuration, " +
						"or a failed self-check). This is not a normal shutdown.";

					LogLine("[Process - Silent Exit] " + note);

					if (haveLastFirstChance)
					{
						LogLine("[Process - Silent Exit] The last non-fatal exception before exit was 0x" +
							lastFirstChanceCode.ToString("X8") +
							" (" + ExplainStatusCode(lastFirstChanceCode) + ") at address 0x" +
							lastFirstChanceAddress.ToString("X"));
					}
				}
				else
				{
					LogLine("[Process] Program closed normally. Exit code: 0");
				}
			}

			stopRequested = true;
		}

		// Central exception handler. First-chance exceptions are logged
		// and passed through; the fatal one triggers all the evidence
		// capture.
		private void HandleException(IntPtr buf, uint tid, ref uint continueStatus)
		{
			uint exceptionCode = 0;
			IntPtr exceptionAddress = IntPtr.Zero;
			int numberParameters = 0;
			int firstChance = 0;

			try { exceptionCode = unchecked((uint)Marshal.ReadInt32(buf, 16)); } catch { return; }
			try { exceptionAddress = Marshal.ReadIntPtr(buf, 32); } catch { }
			try { numberParameters = Marshal.ReadInt32(buf, 40); } catch { }
			try { firstChance = Marshal.ReadInt32(buf, 168); } catch { }

			string meaning = ExplainStatusCode(exceptionCode);

			// First-chance path: usually the program will handle it.
			if (firstChance != 0)
			{
				// WOW64 bridge: a 32-bit process can emit exceptions at
				// 64-bit addresses during startup. Those are not part of
				// the target's own code.
				if (isTarget32Bit && exceptionAddress.ToInt64() > 0xFFFFFFFFL)
				{
					LogLine("[WOW64] Subsystem event at 0x" + exceptionAddress.ToString("X") +
						" (64-bit layer, not part of the 32-bit program)");
					if (exceptionCode != NativeMethods.STATUS_BREAKPOINT &&
						exceptionCode != NativeMethods.STATUS_SINGLE_STEP)
					{
						continueStatus = NativeMethods.DBG_EXCEPTION_NOT_HANDLED;
					}
					return;
				}

				LogLine("[Exception - Non-fatal] Code: 0x" + exceptionCode.ToString("X8") +
					" (" + meaning + ") at address 0x" + exceptionAddress.ToString("X") +
					" - the program will likely handle this itself.");

				if (exceptionCode != NativeMethods.STATUS_BREAKPOINT &&
					exceptionCode != NativeMethods.STATUS_SINGLE_STEP)
				{
					lastFirstChanceCode = exceptionCode;
					lastFirstChanceAddress = exceptionAddress;
					haveLastFirstChance = true;
					continueStatus = NativeMethods.DBG_EXCEPTION_NOT_HANDLED;
				}
				return;
			}

			// Fatal exception: record everything we can before the process
			// terminates.
			wasFatalCrash = true;
			finalStatusCode = exceptionCode;
			crashAddress = exceptionAddress;
			crashModuleName = ResolveModuleForAddress(exceptionAddress, out crashModuleBase);
			if (crashModuleBase != IntPtr.Zero)
			{
				crashModuleOffset = (ulong)(exceptionAddress.ToInt64() - crashModuleBase.ToInt64());
			}

			// Access violation carries two extra parameters.
			if (exceptionCode == NativeMethods.STATUS_ACCESS_VIOLATION && numberParameters >= 2)
			{
				try
				{
					accessType = Marshal.ReadInt64(buf, 48);
					accessedAddress = new IntPtr(Marshal.ReadInt64(buf, 56));
					haveAccessInfo = true;
				}
				catch { }
			}

			// Evidence capture, each wrapped so a failure in one does not
			// prevent the others from running.
			try { CapturePerformanceSnapshot(); } catch { }
			try { CaptureCrashScreenshot(); } catch { }
			try { CaptureCpuRegisters(tid); } catch { }
			try { AnalyzeCrashMemory(); } catch { }
			try { AnalyzeFaultingInstruction(); } catch { }
			try { BuildMiniDisassembly(); } catch { }
			try { capturedCallStack = CaptureCallStack(tid); } catch { capturedCallStack = new List<string>(); }

			// Managed (.NET) exception details, when available.
			if (currentPeInfo.IsDotNet)
			{
				try
				{
					string mType, mMsg, mErr;
					List<string> mStack;
					if (ManagedExceptionReader.TryRead((int)currentPid, tid, out mType, out mMsg, out mStack, out mErr))
					{
						managedExceptionType = mType;
						managedExceptionMessage = mMsg;
						managedStackTraceLines = mStack;
						LogLine("[Managed] Exception: " + mType + " - " + mMsg);
					}
					else
					{
						LogLine("[Managed] Could not read managed exception details: " + mErr);
					}
				}
				catch (Exception mex)
				{
					LogLine("[Managed] Managed-exception reader failed: " + mex.Message);
				}
			}

			LogLine("[!!! FATAL CRASH !!!] Code: 0x" + exceptionCode.ToString("X8") +
				" (" + meaning + ") at address 0x" + exceptionAddress.ToString("X") +
				" in module: " + crashModuleName + " - Thread " + tid);

			if (capturedCallStack.Count > 0)
			{
				LogLine("[Call Stack] " + capturedCallStack.Count + " frame(s) captured - see the final report for details.");
			}

			continueStatus = NativeMethods.DBG_EXCEPTION_NOT_HANDLED;
			stopRequested = true;
		}

		// Working set, handle count, GDI/USER objects, free RAM and disk.
		private void CapturePerformanceSnapshot()
		{
			try
			{
				Process proc = Process.GetProcessById((int)currentPid);
				crashWorkingSetBytes = proc.WorkingSet64;
				crashHandleCount = proc.HandleCount;
			}
			catch
			{
				crashWorkingSetBytes = -1;
				crashHandleCount = -1;
			}

			try { crashGdiObjects = NativeMethods.GetGuiResources(hProcess, 0); }
			catch { crashGdiObjects = -1; }

			try { crashUserObjects = NativeMethods.GetGuiResources(hProcess, 1); }
			catch { crashUserObjects = -1; }

			try { crashAvailableSystemMemoryMb = SystemResourceInfo.GetAvailableSystemMemoryMb(); } catch { }
			try { crashFreeDiskSpaceMb = SystemResourceInfo.GetFreeDiskSpaceMb(targetPath); } catch { }
		}

		// Grabs a PNG of the target's main window at crash time.
		private void CaptureCrashScreenshot()
		{
			try
			{
				IntPtr hWnd = IntPtr.Zero;
				uint targetPidLocal = currentPid;

				NativeMethods.EnumWindows(delegate(IntPtr hw, IntPtr lp)
				{
					uint winPid;
					NativeMethods.GetWindowThreadProcessId(hw, out winPid);
					if (winPid == targetPidLocal && NativeMethods.IsWindowVisible(hw))
					{
						hWnd = hw;
						return false;
					}
					return true;
				}, IntPtr.Zero);

				if (hWnd == IntPtr.Zero) return;

				NativeMethods.RECT rect;
				if (!NativeMethods.GetWindowRect(hWnd, out rect)) return;

				int width = rect.Right - rect.Left;
				int height = rect.Bottom - rect.Top;
				if (width <= 0 || height <= 0 || width > 8000 || height > 8000) return;

				using (Bitmap bmp = new Bitmap(width, height))
				{
					using (Graphics g = Graphics.FromImage(bmp))
					{
						g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
					}

					string dir = Path.GetDirectoryName(reportFilePath);
					string baseName = Path.GetFileNameWithoutExtension(reportFilePath);
					string screenshotPath = Path.Combine(dir, baseName + "_screenshot.png");
					bmp.Save(screenshotPath, ImageFormat.Png);
					crashScreenshotPath = screenshotPath;
				}
			}
			catch
			{
				crashScreenshotPath = null;
			}
		}

		// Reads the crash thread's CPU registers (x86 or x64).
		private void CaptureCpuRegisters(uint tid)
		{
			registerLines.Clear();
			IntPtr hThreadLocal = IntPtr.Zero;
			try
			{
				hThreadLocal = NativeMethods.OpenThread(
					NativeMethods.THREAD_GET_CONTEXT | NativeMethods.THREAD_QUERY_INFORMATION,
					false, tid);
				if (hThreadLocal == IntPtr.Zero)
				{
					registerLines.Add("(could not open thread to read registers)");
					return;
				}

				if (isTarget64Bit)
				{
					NativeMethods.CONTEXT_AMD64 ctx = new NativeMethods.CONTEXT_AMD64();
					ctx.ContextFlags = (uint)NativeMethods.CONTEXT_FULL_AMD64;
					if (!NativeMethods.GetThreadContext(hThreadLocal, ref ctx))
					{
						registerLines.Add("(failed to read 64-bit thread context)");
						return;
					}

					registerLines.Add("RAX = 0x" + ctx.Rax.ToString("X16"));
					registerLines.Add("RBX = 0x" + ctx.Rbx.ToString("X16"));
					registerLines.Add("RCX = 0x" + ctx.Rcx.ToString("X16"));
					registerLines.Add("RDX = 0x" + ctx.Rdx.ToString("X16"));
					registerLines.Add("RSI = 0x" + ctx.Rsi.ToString("X16"));
					registerLines.Add("RDI = 0x" + ctx.Rdi.ToString("X16"));
					registerLines.Add("RSP = 0x" + ctx.Rsp.ToString("X16"));
					registerLines.Add("RBP = 0x" + ctx.Rbp.ToString("X16"));
					registerLines.Add("RIP = 0x" + ctx.Rip.ToString("X16"));
					registerLines.Add("R8  = 0x" + ctx.R8.ToString("X16"));
					registerLines.Add("R9  = 0x" + ctx.R9.ToString("X16"));
					registerLines.Add("R10 = 0x" + ctx.R10.ToString("X16"));
					registerLines.Add("R11 = 0x" + ctx.R11.ToString("X16"));
					registerLines.Add("R12 = 0x" + ctx.R12.ToString("X16"));
					registerLines.Add("R13 = 0x" + ctx.R13.ToString("X16"));
					registerLines.Add("R14 = 0x" + ctx.R14.ToString("X16"));
					registerLines.Add("R15 = 0x" + ctx.R15.ToString("X16"));
					registerLines.Add("EFLAGS = 0x" + ctx.EFlags.ToString("X8"));
				}
				else
				{
					// WOW64 context. FloatSave.RegisterArea and
					// ExtendedRegisters must be sized before the call.
					NativeMethods.WOW64_CONTEXT ctx = new NativeMethods.WOW64_CONTEXT();
					ctx.ContextFlags = NativeMethods.CONTEXT_FULL_X86;
					ctx.FloatSave.RegisterArea = new byte[80];
					ctx.ExtendedRegisters = new byte[512];
					if (!NativeMethods.Wow64GetThreadContext(hThreadLocal, ref ctx))
					{
						registerLines.Add("(failed to read 32-bit thread context)");
						return;
					}

					registerLines.Add("EAX = 0x" + ctx.Eax.ToString("X8"));
					registerLines.Add("EBX = 0x" + ctx.Ebx.ToString("X8"));
					registerLines.Add("ECX = 0x" + ctx.Ecx.ToString("X8"));
					registerLines.Add("EDX = 0x" + ctx.Edx.ToString("X8"));
					registerLines.Add("ESI = 0x" + ctx.Esi.ToString("X8"));
					registerLines.Add("EDI = 0x" + ctx.Edi.ToString("X8"));
					registerLines.Add("ESP = 0x" + ctx.Esp.ToString("X8"));
					registerLines.Add("EBP = 0x" + ctx.Ebp.ToString("X8"));
					registerLines.Add("EIP = 0x" + ctx.Eip.ToString("X8"));
					registerLines.Add("EFLAGS = 0x" + ctx.EFlags.ToString("X8"));
				}
			}
			catch
			{
				registerLines.Add("(exception while reading registers)");
			}
			finally
			{
				if (hThreadLocal != IntPtr.Zero)
				{
					try { NativeMethods.CloseHandle(hThreadLocal); } catch { }
				}
			}
		}

		// Reads the crash-site memory region, a hex dump of the bytes
		// around the fault, a 256 KB dump of the region, and the stack.
		private void AnalyzeCrashMemory()
		{
			try
			{
				if (hProcess == IntPtr.Zero) return;

				MemoryInspector.RegionInfo region = MemoryInspector.DescribeRegion(hProcess, crashAddress);
				if (region.Success)
				{
					crashRegionState = region.State;
					crashRegionProtect = region.Protect;
					crashRegionType = region.Type;
					crashRegionDescription = region.Description;
					crashRegionBase = region.BaseAddress.ToInt64();
					crashRegionSize = region.RegionSize;
				}

				// 32 bytes before, 64 bytes after the fault.
				IntPtr readStart = new IntPtr(crashAddress.ToInt64() - 32);
				int bytesRead;
				byte[] bytes = MemoryInspector.ReadBytes(hProcess, readStart, 96, out bytesRead);
				if (bytes != null && bytesRead > 0)
				{
					crashSiteHexDump = MemoryInspector.FormatHexDump(bytes, readStart, 96);
					crashSitePattern = MemoryInspector.DetectPattern(bytes, bytesRead);
				}

				// Save a 256 KB binary blob of the crash region.
				try
				{
					int dumpSize = 256 * 1024;
					IntPtr dumpStart = new IntPtr(crashAddress.ToInt64() - (dumpSize / 2));
					int largeRead;
					byte[] largeBytes = MemoryInspector.ReadBytes(hProcess, dumpStart, dumpSize, out largeRead);
					if (largeBytes != null && largeRead > 0)
					{
						string dir = Path.GetDirectoryName(reportFilePath);
						string baseName = Path.GetFileNameWithoutExtension(reportFilePath);
						string memPath = Path.Combine(dir, baseName + "_crashsite.bin");
						File.WriteAllBytes(memPath, largeBytes);
						crashSiteMemoryFilePath = memPath;
					}
				}
				catch { }

				// Stack: use the SP we just captured.
				try
				{
					ulong spValue = 0;
					string spName = isTarget64Bit ? "RSP" : "ESP";
					foreach (string line in registerLines)
					{
						if (line.StartsWith(spName, StringComparison.OrdinalIgnoreCase))
						{
							spValue = ParseHexValue(line);
							break;
						}
					}

					if (spValue != 0)
					{
						IntPtr sp = new IntPtr((long)spValue);
						MemoryInspector.RegionInfo stackRegion = MemoryInspector.DescribeRegion(hProcess, sp);
						if (stackRegion.Success)
						{
							stackRegionBase = stackRegion.BaseAddress.ToInt64();
							stackRegionSize = stackRegion.RegionSize;
						}

						int stackSize = 4096;
						int stackRead;
						byte[] stackBytes = MemoryInspector.ReadBytes(hProcess, sp, stackSize, out stackRead);
						if (stackBytes != null && stackRead > 0)
						{
							stackHexDump = MemoryInspector.FormatHexDump(stackBytes, sp, Math.Min(stackRead, 512));
							try
							{
								string dir = Path.GetDirectoryName(reportFilePath);
								string baseName = Path.GetFileNameWithoutExtension(reportFilePath);
								string stackPath = Path.Combine(dir, baseName + "_stack.bin");
								File.WriteAllBytes(stackPath, stackBytes);
							}
							catch { }
						}
					}
				}
				catch { }
			}
			catch
			{
			}
		}

		// Parses "NAME = 0xVALUE" from a register line.
		private static ulong ParseHexValue(string line)
		{
			try
			{
				int idx = line.IndexOf('=');
				if (idx < 0) return 0;
				string val = line.Substring(idx + 1).Trim();
				val = val.Replace("0x", "").Trim();
				return Convert.ToUInt64(val, 16);
			}
			catch
			{
				return 0;
			}
		}

		// Decodes the faulting instruction via the minimal decoder.
		private void AnalyzeFaultingInstruction()
		{
			try
			{
				if (hProcess == IntPtr.Zero) return;

				int bytesRead;
				byte[] bytes = MemoryInspector.ReadBytes(hProcess, crashAddress, 16, out bytesRead);
				if (bytes == null || bytesRead < 2) return;

				BasicDisassembler.DecodedInstruction ins = BasicDisassembler.Decode(bytes, isTarget64Bit);
				if (ins.Success)
				{
					faultingInstructionText = ins.FullText;
					faultingInstructionNote = ins.Note;
					faultingInstructionLength = ins.Length;
				}
			}
			catch
			{
			}
		}

		// Builds a small disassembly around the crash using SharpDisasm
		// with a fallback to the built-in decoder.
		private void BuildMiniDisassembly()
		{
			try
			{
				DisassemblyHelper.MiniResult result = DisassemblyHelper.Build(
					hProcess, crashAddress, isTarget64Bit, 96, 128);

				if (result.Success)
				{
					miniDisassembly = result.Text;
					miniDisassemblyEngine = result.Engine;
				}
				else
				{
					miniDisassembly = null;
					miniDisassemblyEngine = null;
				}
			}
			catch
			{
				miniDisassembly = null;
				miniDisassemblyEngine = null;
			}
		}

		// Reads PE section entropy for the target.
		private void AnalyzeSectionEntropy()
		{
			try
			{
				sectionEntropyLines.Clear();
				List<EntropyAnalyzer.SectionEntropy> sections = EntropyAnalyzer.AnalyzeFile(targetPath);
				foreach (EntropyAnalyzer.SectionEntropy s in sections)
				{
					sectionEntropyLines.Add(
						"  " + s.Name.PadRight(10) +
						" size=0x" + s.Size.ToString("X8") +
						" entropy=" + s.Entropy.ToString("0.000") +
						" - " + s.Interpretation);
				}
			}
			catch
			{
			}
		}

		// Reads any companion logs next to the target.
		private void AnalyzeCompanionLogs()
		{
			try
			{
				companionLogContents = CompanionLogReader.FindAndRead(targetPath, 60, 3);
			}
			catch
			{
			}
		}

		// Snapshots the target's threads (state, wait reason, priority, CPU).
		private void AnalyzeThreads()
		{
			try
			{
				threadSnapshotLines.Clear();
				List<ThreadAnalyzer.ThreadSnapshot> threads = ThreadAnalyzer.Snapshot(currentPid);
				threadSnapshotLines.Add("  Total threads: " + threads.Count);
				foreach (ThreadAnalyzer.ThreadSnapshot t in threads)
				{
					threadSnapshotLines.Add(
						"  TID " + t.ThreadId.ToString().PadRight(8) +
						" state=" + (t.State ?? "?").PadRight(12) +
						" wait=" + (t.WaitReason ?? "?").PadRight(18) +
						" prio=" + (t.PriorityName ?? "?").PadRight(12) +
						" cpu=" + t.TotalProcessorTimeMs + "ms");
				}
			}
			catch
			{
			}
		}

		// Extracts a few notable strings from the executable.
		private void AnalyzeInterestingStrings()
		{
			try
			{
				interestingStrings.Clear();
				List<string> all = StringExtractor.ExtractFromFile(targetPath, 200, 8);
				foreach (string s in all)
				{
					interestingStrings.Add(s);
					if (interestingStrings.Count >= 60) break;
				}
			}
			catch
			{
			}
		}

		// Detects the engine/framework, if any.
		private void AnalyzeEngine()
		{
			try
			{
				detectedEngine = EngineDetector.Detect(targetPath, loadedModuleNames.Values);
			}
			catch
			{
			}
		}

		// Scans loaded modules for API hooks.
		private void AnalyzeApiHooks()
		{
			try
			{
				apiHooksDetected.Clear();
				List<ApiHookDetector.HookInfo> hooks = ApiHookDetector.DetectHooks(hProcess);
				foreach (ApiHookDetector.HookInfo h in hooks)
				{
					apiHooksDetected.Add(h.Module + "!" + h.Function + " - " + h.Reason);
				}
			}
			catch
			{
			}
		}

		// Reads the environment block of the target process.
		private void AnalyzeProcessEnvironment()
		{
			try
			{
				processEnvironment = ProcessEnvironmentReader.ReadEnvironment(hProcess, isTarget32Bit);
			}
			catch
			{
			}
		}

		// Finds recent WER reports for the target's file name.
		private void AnalyzeWerReports()
		{
			try
			{
				werReportLines = WerReportReader.FindReportsForProcess(Path.GetFileName(targetPath), 3);
			}
			catch
			{
			}
		}

		// Reads the embedded application manifest, if any.
		private void AnalyzeManifest()
		{
			try
			{
				manifestContent = ManifestReader.ReadManifest(targetPath);
			}
			catch
			{
			}
		}

		// Compares the target's PE imports against what actually loaded
		// and against disk. Emits [MISSING-DLL] lines for real problems.
		private void PopulateDependencyAnalysis()
		{
			try
			{
				List<string> imports = PeImportReader.GetImportedDllNames(targetPath);
				if (imports.Count == 0) return;

				List<string> loadedNames = new List<string>();
				foreach (string path in loadedModuleNames.Values)
				{
					try { loadedNames.Add(Path.GetFileName(path).ToLowerInvariant()); }
					catch { }
				}

				string gameDir = Path.GetDirectoryName(targetPath);

				// Direct imports.
				foreach (string imp in imports)
				{
					string lower = imp.ToLowerInvariant();
					if (loadedNames.Contains(lower)) continue;

					if (FileExistsOnDisk(imp, gameDir))
					{
						if (!probablyFineDlls.Contains(imp)) probablyFineDlls.Add(imp);
						continue;
					}

					if (!confirmedMissingDlls.Contains(imp)) confirmedMissingDlls.Add(imp);

					if (!reportedMissingDlls.Contains(lower))
					{
						reportedMissingDlls.Add(lower);

						AppendLogSafe("[MISSING-DLL] The program's PE header requires '" + imp +
							"' but it has NOT loaded, and it does NOT exist in any standard " +
							"search path (game folder, System32, SysWOW64).");

						string fix = RedistributableChecker.IdentifyRequiredRedistributable(imp);
						if (fix != null)
						{
							if (!redistributableSuggestions.Contains(fix))
								redistributableSuggestions.Add(fix);
							AppendLogSafe("[MISSING-DLL] Likely fix for '" + imp + "': " + fix);
						}
					}
				}

				// Indirect imports: dependencies of DLLs that shipped with
				// the target in the same folder.
				foreach (string loadedPath in loadedModuleNames.Values)
				{
					string loadedDir;
					try { loadedDir = Path.GetDirectoryName(loadedPath); }
					catch { continue; }
					if (!string.Equals(loadedDir, gameDir, StringComparison.OrdinalIgnoreCase)) continue;

					List<string> subImports = PeImportReader.GetImportedDllNames(loadedPath);
					foreach (string sub in subImports)
					{
						string subLower = sub.ToLowerInvariant();
						if (loadedNames.Contains(subLower)) continue;
						if (indirectMissingDlls.Contains(sub)) continue;
						if (!FileExistsOnDisk(sub, gameDir))
						{
							indirectMissingDlls.Add(sub);

							if (!reportedMissingDlls.Contains(subLower))
							{
								reportedMissingDlls.Add(subLower);
								AppendLogSafe("[MISSING-DLL] One of the program's own DLLs (" +
									Path.GetFileName(loadedPath) + ") requires '" + sub +
									"', which has NOT loaded and does NOT exist on disk.");

								string fix = RedistributableChecker.IdentifyRequiredRedistributable(sub);
								if (fix != null)
								{
									if (!redistributableSuggestions.Contains(fix))
										redistributableSuggestions.Add(fix);
									AppendLogSafe("[MISSING-DLL] Likely fix for '" + sub + "': " + fix);
								}
							}
						}
					}
				}
			}
			catch
			{
			}
		}

		// Writes a minidump next to the report, best-effort.
		private void TryWriteMiniDump()
		{
			try
			{
				if (hProcess == IntPtr.Zero) return;
				string dir = Path.GetDirectoryName(reportFilePath);
				string baseName = Path.GetFileNameWithoutExtension(reportFilePath);
				string dumpPath = Path.Combine(dir, baseName + ".dmp");

				bool ok = MiniDumpWriter.WriteDump(hProcess, currentPid, dumpPath);
				if (ok) crashMiniDumpPath = dumpPath;
			}
			catch
			{
				crashMiniDumpPath = null;
			}
		}

		// Pulls recent Windows Event Log entries related to the target and
		// checks Steam client state.
		private void TryFetchEventLogEntries()
		{
			try
			{
				string exeName = Path.GetFileName(targetPath);
				DateTime since = DateTime.Now.AddMinutes(-15);
				relatedSystemEvents = WindowsEventLogReader.GetRecentEntriesForProcess(exeName, since);
				if (relatedSystemEvents.Count > 0)
				{
					LogLine("[EventLog] Found " + relatedSystemEvents.Count +
						" Windows Event Log entries mentioning this program.");
				}
				steamClientRunning = SteamClientChecker.IsSteamClientRunning();
			}
			catch
			{
			}
		}

		// Uses DbgHelp to translate an address into a function name.
		// Returns null when no symbol is available.
		private string ResolveSymbolName(ulong address, out ulong displacement, out int win32Error)
		{
			displacement = 0;
			win32Error = 0;
			if (!symbolsInitialized)
			{
				win32Error = -1;
				return null;
			}

			IntPtr buffer = IntPtr.Zero;
			try
			{
				// SYMBOL_INFO is a variable-length struct: fixed header
				// followed by the name buffer.
				int bufferSize = NativeMethods.SYMBOL_INFO_FIXED_SIZE + NativeMethods.MAX_SYM_NAME_LEN;
				buffer = Marshal.AllocHGlobal(bufferSize);

				for (int i = 0; i < bufferSize; i++) Marshal.WriteByte(buffer, i, 0);
				Marshal.WriteInt32(buffer, 0, NativeMethods.SYMBOL_INFO_FIXED_SIZE);
				Marshal.WriteInt32(buffer, 80, NativeMethods.MAX_SYM_NAME_LEN);

				bool ok = NativeMethods.SymFromAddr(hProcess, address, out displacement, buffer);
				if (!ok)
				{
					win32Error = Marshal.GetLastWin32Error();
					return null;
				}

				IntPtr namePtr = new IntPtr(buffer.ToInt64() + NativeMethods.SYMBOL_INFO_NAME_OFFSET);
				string name = Marshal.PtrToStringAnsi(namePtr);
				if (string.IsNullOrEmpty(name)) return null;
				symbolsWereResolved = true;
				return name;
			}
			catch
			{
				return null;
			}
			finally
			{
				if (buffer != IntPtr.Zero)
				{
					try { Marshal.FreeHGlobal(buffer); } catch { }
				}
			}
		}

		// Walks the call stack of the given thread using DbgHelp's
		// StackWalk64, and formats each frame.
		private List<string> CaptureCallStack(uint tid)
		{
			List<string> frames = new List<string>();
			IntPtr hThreadLocal = IntPtr.Zero;

			try
			{
				if (!isTarget32Bit && !isTarget64Bit)
				{
					frames.Add("(unknown target architecture - cannot walk stack)");
					return frames;
				}

				if (symbolsInitialized)
				{
					try { NativeMethods.SymRefreshModuleList(hProcess); } catch { }
				}

				hThreadLocal = NativeMethods.OpenThread(
					NativeMethods.THREAD_GET_CONTEXT | NativeMethods.THREAD_QUERY_INFORMATION,
					false, tid);
				if (hThreadLocal == IntPtr.Zero)
				{
					frames.Add("(could not open the thread to read its registers)");
					return frames;
				}

				if (isTarget64Bit)
					CaptureCallStack64(hThreadLocal, frames);
				else
					CaptureCallStack32(hThreadLocal, frames);
			}
			catch (Exception ex)
			{
				frames.Add("(stack walk failed: " + ex.Message + ")");
			}
			finally
			{
				if (hThreadLocal != IntPtr.Zero)
				{
					try { NativeMethods.CloseHandle(hThreadLocal); } catch { }
				}
			}

			return frames;
		}

		// 32-bit stack walk path.
		private void CaptureCallStack32(IntPtr hThreadLocal, List<string> frames)
		{
			NativeMethods.WOW64_CONTEXT ctx = new NativeMethods.WOW64_CONTEXT();
			ctx.ContextFlags = NativeMethods.CONTEXT_FULL_X86;
			ctx.FloatSave.RegisterArea = new byte[80];
			ctx.ExtendedRegisters = new byte[512];

			if (!NativeMethods.Wow64GetThreadContext(hThreadLocal, ref ctx))
			{
				frames.Add("(failed to read the thread's CPU registers)");
				return;
			}

			// STACKFRAME64 is used even for 32-bit targets; DbgHelp
			// interprets the fields according to machine type.
			NativeMethods.STACKFRAME64 frame = new NativeMethods.STACKFRAME64();
			frame.Params = new ulong[4];
			frame.Reserved = new ulong[3];
			frame.KdHelp = new NativeMethods.KDHELP64();
			frame.KdHelp.Reserved = new ulong[5];

			frame.AddrPC.Offset = ctx.Eip;
			frame.AddrPC.Mode = NativeMethods.ADDR_MODE_FLAT;
			frame.AddrFrame.Offset = ctx.Ebp;
			frame.AddrFrame.Mode = NativeMethods.ADDR_MODE_FLAT;
			frame.AddrStack.Offset = ctx.Esp;
			frame.AddrStack.Mode = NativeMethods.ADDR_MODE_FLAT;

			int ctxSize = Marshal.SizeOf(ctx);
			IntPtr ctxPtr = Marshal.AllocHGlobal(ctxSize);
			try
			{
				Marshal.StructureToPtr(ctx, ctxPtr, false);
				WalkAndFormatFrames(NativeMethods.IMAGE_FILE_MACHINE_I386, hThreadLocal, ref frame, ctxPtr, frames);
			}
			finally
			{
				try { Marshal.FreeHGlobal(ctxPtr); } catch { }
			}
		}

		// 64-bit stack walk path.
		private void CaptureCallStack64(IntPtr hThreadLocal, List<string> frames)
		{
			NativeMethods.CONTEXT_AMD64 ctx = new NativeMethods.CONTEXT_AMD64();
			ctx.ContextFlags = (uint)NativeMethods.CONTEXT_FULL_AMD64;

			if (!NativeMethods.GetThreadContext(hThreadLocal, ref ctx))
			{
				frames.Add("(failed to read the thread's CPU registers)");
				return;
			}

			NativeMethods.STACKFRAME64 frame = new NativeMethods.STACKFRAME64();
			frame.Params = new ulong[4];
			frame.Reserved = new ulong[3];
			frame.KdHelp = new NativeMethods.KDHELP64();
			frame.KdHelp.Reserved = new ulong[5];

			frame.AddrPC.Offset = ctx.Rip;
			frame.AddrPC.Mode = NativeMethods.ADDR_MODE_FLAT;
			frame.AddrFrame.Offset = ctx.Rbp;
			frame.AddrFrame.Mode = NativeMethods.ADDR_MODE_FLAT;
			frame.AddrStack.Offset = ctx.Rsp;
			frame.AddrStack.Mode = NativeMethods.ADDR_MODE_FLAT;

			int ctxSize = Marshal.SizeOf(ctx);
			IntPtr ctxPtr = Marshal.AllocHGlobal(ctxSize);
			try
			{
				Marshal.StructureToPtr(ctx, ctxPtr, false);
				WalkAndFormatFrames(NativeMethods.IMAGE_FILE_MACHINE_AMD64, hThreadLocal, ref frame, ctxPtr, frames);
			}
			finally
			{
				try { Marshal.FreeHGlobal(ctxPtr); } catch { }
			}
		}

		// Common frame loop. Prefers symbol names; falls back to
		// module+offset, then to raw address. Records a diagnostic note
		// the first time symbol resolution fails, for the report.
		private void WalkAndFormatFrames(uint machineType, IntPtr hThreadLocal,
			ref NativeMethods.STACKFRAME64 frame, IntPtr ctxPtr, List<string> frames)
		{
			bool loggedSymbolDiagnostic = false;

			for (int i = 0; i < NativeMethods.MAX_STACK_FRAMES; i++)
			{
				bool ok = NativeMethods.StackWalk64(
					machineType, hProcess, hThreadLocal,
					ref frame, ctxPtr, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

				if (!ok || frame.AddrPC.Offset == 0) break;

				IntPtr moduleBase;
				string moduleName = ResolveModuleForAddress(new IntPtr((long)frame.AddrPC.Offset), out moduleBase);
				string moduleShortName = moduleName.StartsWith("Unknown") ? null : Path.GetFileName(moduleName);

				ulong relativeOffset = moduleBase != IntPtr.Zero
					? frame.AddrPC.Offset - (ulong)moduleBase.ToInt64()
					: frame.AddrPC.Offset;

				ulong displacement;
				int symbolError;
				string symbolName = ResolveSymbolName(frame.AddrPC.Offset, out displacement, out symbolError);

				if (symbolName != null)
				{
					string prefix = moduleShortName != null ? moduleShortName + "!" : "";
					frames.Add(prefix + symbolName + "+0x" + displacement.ToString("X"));
				}
				else if (moduleShortName != null)
				{
					frames.Add(moduleShortName + "+0x" + relativeOffset.ToString("X"));
				}
				else
				{
					frames.Add("0x" + frame.AddrPC.Offset.ToString("X"));
				}

				if (symbolName == null && !loggedSymbolDiagnostic)
				{
					loggedSymbolDiagnostic = true;
					if (symbolError == -1)
					{
						symbolDiagnosticNote = "the symbol engine failed to start";
					}
					else if (symbolError != 0)
					{
						symbolDiagnosticNote = "function-name lookup failed (Windows error " + symbolError + ")";
					}
					else
					{
						symbolDiagnosticNote = "no matching local symbol files were found on this machine";
					}
				}
			}
		}

		private string ResolveModuleForAddress(IntPtr address)
		{
			IntPtr baseUnused;
			return ResolveModuleForAddress(address, out baseUnused);
		}

		// Uses VirtualQueryEx + the loaded module map to name the module
		// that contains the given address. Returns the base address as
		// well so callers can compute an offset.
		private string ResolveModuleForAddress(IntPtr address, out IntPtr moduleBase)
		{
			moduleBase = IntPtr.Zero;
			try
			{
				NativeMethods.MEMORY_BASIC_INFORMATION mbi;
				IntPtr result = NativeMethods.VirtualQueryEx(
					hProcess, address, out mbi,
					(uint)Marshal.SizeOf(typeof(NativeMethods.MEMORY_BASIC_INFORMATION)));
				if (result != IntPtr.Zero)
				{
					string found;
					if (loadedModuleNames.TryGetValue(mbi.AllocationBase, out found))
					{
						moduleBase = mbi.AllocationBase;
						return found;
					}
				}
			}
			catch
			{
			}
			return "Unknown (not a currently tracked module)";
		}

		#endregion

		#region Report Generation

		// Computes SHA256 of the target file, streamed.
		private static string ComputeSha256(string filePath)
		{
			try
			{
				using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
				{
					byte[] hash = sha.ComputeHash(fs);
					StringBuilder sb = new StringBuilder();
					for (int i = 0; i < hash.Length; i++)
						sb.Append(hash[i].ToString("X2"));
					return sb.ToString();
				}
			}
			catch
			{
				return null;
			}
		}

		// Builds the report data, then writes the .txt and .md reports.
		// Falls back to a minimal emergency report if anything throws.
		private void WriteFinalAnalysis()
		{
			try { sessionEndedAt = DateTime.Now; }
			catch { }

			try { PopulateDependencyAnalysis(); } catch { }

			CrashReportData d = null;
			try { d = BuildDataForReports(); } catch { }

			string txt = null;
			if (d != null)
			{
				try { txt = BuildFullTxtReport(d); } catch { txt = null; }
			}

			if (string.IsNullOrEmpty(txt))
			{
				try { txt = BuildEmergencyReport(); } catch { txt = "Emergency report could not be built."; }
			}

			lastFinalAnalysisText = txt;

			try
			{
				if (!string.IsNullOrEmpty(reportFilePath))
					File.WriteAllText(reportFilePath, txt, Encoding.UTF8);
			}
			catch { }

			try
			{
				if (d != null)
				{
					string md = BuildFullMdReport(d);
					string mdPath = Path.ChangeExtension(reportFilePath, ".md");
					File.WriteAllText(mdPath, md, Encoding.UTF8);
				}
			}
			catch { }
		}

		// Minimal report when the full one could not be produced.
		private string BuildEmergencyReport()
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("==================================================");
			sb.AppendLine(" Emergency Crash Diagnostic Report");
			sb.AppendLine("==================================================");
			sb.AppendLine("Target program : " + (targetPath ?? "(unknown)"));
			sb.AppendLine("Started at     : " + sessionStartedAt.ToString("yyyy-MM-dd HH:mm:ss"));
			sb.AppendLine("Finished at    : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
			sb.AppendLine();
			sb.AppendLine("The full report could not be generated (an internal error occurred).");
			sb.AppendLine("This emergency report contains the raw event log collected during");
			sb.AppendLine("monitoring, which is often enough to identify the problem.");
			sb.AppendLine();
			sb.AppendLine("Note: watchdogFired = " + watchdogFired);
			sb.AppendLine();
			sb.AppendLine("==================================================");
			sb.AppendLine(" LIVE EVENT LOG");
			sb.AppendLine("==================================================");
			try
			{
				foreach (string line in allLogLines) sb.AppendLine(line);
			}
			catch { }
			sb.AppendLine();
			sb.AppendLine("==================================================");
			sb.AppendLine(" End of report");
			sb.AppendLine("==================================================");
			return sb.ToString();
		}

		// Full plain-text report. Sections are appended in a fixed order
		// so the layout is stable across runs.
		private string BuildFullTxtReport(CrashReportData d)
		{
			StringBuilder sb = new StringBuilder();

			sb.AppendLine("==================================================");
			sb.AppendLine(" Automated Crash / Exit Diagnostic Report");
			sb.AppendLine("==================================================");
			sb.AppendLine("Target program : " + d.TargetPath);
			sb.AppendLine("Started at     : " + d.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"));
			sb.AppendLine("Finished at    : " + d.FinishedAt.ToString("yyyy-MM-dd HH:mm:ss"));
			sb.AppendLine("Architecture   : " + (d.Architecture ?? "Unknown"));
			sb.AppendLine("Subsystem      : " + (d.SubsystemVersion ?? "n/a"));
			sb.AppendLine("Entry point    : 0x" + d.EntryPointRva.ToString("X8"));
			sb.AppendLine("Sections       : " + d.NumberOfSections);
			sb.AppendLine("DLL chars      : " + (d.DllCharacteristicsFlags ?? "(none)"));
			sb.AppendLine("Managed (.NET) : " + (d.IsDotNetAssembly ? "Yes" : "No"));
			if (!string.IsNullOrEmpty(d.Sha256Hash))
				sb.AppendLine("SHA256         : " + d.Sha256Hash);
			if (!string.IsNullOrEmpty(d.SignatureInfo))
				sb.AppendLine("Signature      : " + d.SignatureInfo);
			if (!string.IsNullOrEmpty(d.DetectedEngine))
				sb.AppendLine("Engine         : " + d.DetectedEngine);
			if (!string.IsNullOrEmpty(d.PackedHint))
				sb.AppendLine("Packer hint    : " + d.PackedHint);
			if (!string.IsNullOrEmpty(d.PeChecksumWarning))
				sb.AppendLine("Warning        : " + d.PeChecksumWarning);
			if (watchdogFired)
				sb.AppendLine("Watchdog       : The watchdog forced an exit from the debug loop. The target may have been hung or the debugger was stuck.");

			sb.AppendLine();
			sb.AppendLine("==================================================");
			sb.AppendLine(" PROBLEM");
			sb.AppendLine("==================================================");

			if (d.ResultLabel == "STOPPED")
			{
				sb.AppendLine("Result        : Stopped manually by the user.");
			}
			else if (d.ResultLabel == "CRASH")
			{
				sb.AppendLine("Result        : CRASH (unhandled exception)");
				sb.AppendLine("Status code   : " + d.StatusCodeHex);
				sb.AppendLine("Meaning       : " + d.Meaning);
				if (!string.IsNullOrEmpty(managedExceptionType))
				{
					sb.AppendLine("Managed exception : " + managedExceptionType);
					sb.AppendLine("Managed message   : " + managedExceptionMessage);
					if (managedStackTraceLines.Count > 0)
					{
						sb.AppendLine("Managed stack trace:");
						foreach (string frame in managedStackTraceLines)
							sb.AppendLine("  " + frame);
					}
				}
				sb.AppendLine("Address       : " + d.AddressHex);
				sb.AppendLine("Module        : " + d.ModuleName);
				if (!string.IsNullOrEmpty(d.ModuleOffsetHex))
					sb.AppendLine("Offset        : " + d.ModuleOffsetHex);
				if (d.SecondsUntilCrash >= 0)
					sb.AppendLine("Time to crash : " + d.SecondsUntilCrash + " second(s) after start");
				if (d.HaveAccessInfo)
					sb.AppendLine("Access type   : Attempted to " + d.AccessKind + " memory at " + d.AccessedAddressHex);
				if (d.LikelyNullPointer)
				{
					sb.AppendLine("Diagnosis     : This is very likely a NULL or near-NULL pointer bug.");
					sb.AppendLine("                The code tried to use a pointer or object that was never");
					sb.AppendLine("                initialized. This is a bug in the program itself - it cannot");
					sb.AppendLine("                be fixed by reinstalling or changing system settings.");
				}
				else if (d.LikelyDepViolation)
				{
					sb.AppendLine("Diagnosis     : DEP (Data Execution Prevention) violation - the program tried to");
					sb.AppendLine("                execute memory that Windows has marked as non-executable.");
					sb.AppendLine("                Common causes: packed/protected code, a security product");
					sb.AppendLine("                injecting into the process, or a JIT compilation bug.");
				}
				else if (d.LikelyWriteToReadOnly)
				{
					sb.AppendLine("Diagnosis     : Attempted to write to read-only memory. This usually");
					sb.AppendLine("                indicates an anti-tamper or DRM component trying to patch");
					sb.AppendLine("                code that Windows has protected.");
				}
			}
			else if (d.ResultLabel == "SILENT EXIT")
			{
				sb.AppendLine("Result        : SILENT EXIT (program closed itself with code 0)");
				sb.AppendLine("Meaning       : The program terminated itself very quickly after starting,");
				sb.AppendLine("                without producing an error code. This is almost always a");
				sb.AppendLine("                deliberate self-termination (failed self-check, missing");
				sb.AppendLine("                asset, invalid configuration, or an anti-tamper measure).");
				sb.AppendLine("                It is NOT a normal shutdown.");
				if (haveLastFirstChance)
				{
					sb.AppendLine("Last exception: 0x" + lastFirstChanceCode.ToString("X8") +
						" (" + ExplainStatusCode(lastFirstChanceCode) + ")");
				}
			}
			else if (d.ResultLabel == "ABNORMAL EXIT")
			{
				sb.AppendLine("Result        : Program terminated itself (no fatal exception)");
				sb.AppendLine("Status code   : " + d.StatusCodeHex);
				sb.AppendLine("Meaning       : " + d.Meaning);
			}
			else
			{
				sb.AppendLine("Result        : Program closed normally, no problems detected.");
			}

			if (!string.IsNullOrEmpty(d.FaultingInstructionText))
			{
				sb.AppendLine();
				sb.AppendLine("Faulting code : " + d.FaultingInstructionText);
				if (!string.IsNullOrEmpty(d.FaultingInstructionNote))
					sb.AppendLine("Explanation   : " + d.FaultingInstructionNote);
			}

			if (d.HangDetected)
			{
				sb.AppendLine();
				sb.AppendLine("Hang          : The target window became unresponsive.");
				sb.AppendLine("First hang at : " + d.HangStartTime.ToString("HH:mm:ss"));
				sb.AppendLine("Hang count    : " + d.HangCount);
			}

			if (d.ConfirmedMissingDlls.Count > 0 || d.IndirectMissingDlls.Count > 0 || d.ProbablyFineDlls.Count > 0)
			{
				sb.AppendLine();
				sb.AppendLine("==================================================");
				sb.AppendLine(" MISSING DEPENDENCIES");
				sb.AppendLine("==================================================");

				if (d.ConfirmedMissingDlls.Count > 0)
				{
					sb.AppendLine("Confirmed missing from disk:");
					foreach (string m in d.ConfirmedMissingDlls) sb.AppendLine("  - " + m);
					sb.AppendLine();
				}

				if (d.IndirectMissingDlls.Count > 0)
				{
					sb.AppendLine("Indirect missing (needed by one of the program's own DLLs):");
					foreach (string m in d.IndirectMissingDlls) sb.AppendLine("  - " + m);
					sb.AppendLine();
				}

				if (d.RedistributableSuggestions.Count > 0)
				{
					sb.AppendLine("Likely fix - install / repair:");
					foreach (string fix in d.RedistributableSuggestions) sb.AppendLine("  - " + fix);
					sb.AppendLine();
				}

				if (d.ProbablyFineDlls.Count > 0)
				{
					sb.AppendLine("Not yet loaded, but the file DOES exist on disk (probably NOT the cause):");
					foreach (string m in d.ProbablyFineDlls) sb.AppendLine("  - " + m);
					sb.AppendLine();
				}
			}

			sb.AppendLine();
			sb.AppendLine("==================================================");
			sb.AppendLine(" TECHNICAL DETAILS");
			sb.AppendLine("==================================================");

			if (d.RegisterLines.Count > 0)
			{
				sb.AppendLine("CPU registers at crash:");
				foreach (string line in d.RegisterLines) sb.AppendLine("  " + line);
				sb.AppendLine();
			}

			if (!string.IsNullOrEmpty(miniDisassembly))
			{
				sb.AppendLine("Mini disassembly around crash site:");
				if (!string.IsNullOrEmpty(miniDisassemblyEngine))
					sb.AppendLine("  (engine: " + miniDisassemblyEngine + ")");
				sb.AppendLine(miniDisassembly);
				sb.AppendLine();
			}

			if (!string.IsNullOrEmpty(d.CrashRegionState))
			{
				sb.AppendLine("Memory region at crash site:");
				sb.AppendLine("  Base        : 0x" + d.CrashRegionBase.ToString("X"));
				sb.AppendLine("  Size        : 0x" + d.CrashRegionSize.ToString("X"));
				sb.AppendLine("  State       : " + d.CrashRegionState);
				sb.AppendLine("  Protection  : " + d.CrashRegionProtect);
				sb.AppendLine("  Type        : " + d.CrashRegionType);
				if (!string.IsNullOrEmpty(d.CrashRegionDescription))
					sb.AppendLine("  Note        : " + d.CrashRegionDescription);
				sb.AppendLine();
			}

			if (!string.IsNullOrEmpty(d.CrashSiteHexDump))
			{
				sb.AppendLine("Memory around crash site:");
				sb.AppendLine(d.CrashSiteHexDump);
				if (!string.IsNullOrEmpty(d.CrashSitePattern))
					sb.AppendLine("Pattern     : " + d.CrashSitePattern);
				sb.AppendLine();
			}

			if (!string.IsNullOrEmpty(d.StackHexDump))
			{
				sb.AppendLine("Stack memory (top of stack):");
				if (d.StackRegionBase != 0)
				{
					sb.AppendLine("  Region base : 0x" + d.StackRegionBase.ToString("X"));
					sb.AppendLine("  Region size : 0x" + d.StackRegionSize.ToString("X"));
				}
				sb.AppendLine(d.StackHexDump);
				sb.AppendLine();
			}

			if (d.CallStack.Count > 0)
			{
				sb.AppendLine("Call stack (top = where it happened):");
				for (int i = 0; i < d.CallStack.Count; i++)
					sb.AppendLine("  #" + i + "  " + d.CallStack[i]);
				sb.AppendLine();

				if (d.SymbolsWereResolved)
				{
					sb.AppendLine("Note on symbols: function names were resolved from local symbol files.");
					sb.AppendLine("Symbol resolution is offline-only - the tool never contacts the");
					sb.AppendLine("internet at any point.");
				}
				else
				{
					// Long explanation of why symbol names may be missing.
					sb.AppendLine("Note on symbols:");
					sb.AppendLine("  Function names could not be resolved, so only module names and offsets");
					sb.AppendLine("  are shown. This does not affect the accuracy of the analysis - the");
					sb.AppendLine("  offset (for example \"game.exe+0x1A4F2\") is stable across runs and");
					sb.AppendLine("  machines, unlike a raw memory address.");
					sb.AppendLine();
					sb.AppendLine("  Why symbols might be unavailable:");
					if (!string.IsNullOrEmpty(d.SymbolDiagnosticNote))
						sb.AppendLine("    - " + d.SymbolDiagnosticNote + ".");
					sb.AppendLine("    - Symbol resolution in this tool is fully offline by design: it never");
					sb.AppendLine("      connects to the internet, and it does not use Microsoft's online");
					sb.AppendLine("      symbol server. It only looks for symbol files (PDB) that already");
					sb.AppendLine("      exist locally on this computer.");
					sb.AppendLine("    - Bundling all Windows symbol files with the tool is not practical:");
					sb.AppendLine("      the full set for a single Windows build alone can exceed several");
					sb.AppendLine("      gigabytes, would noticeably slow the tool down at startup, and would");
					sb.AppendLine("      still not match a different Windows build running on another machine.");
					sb.AppendLine("    - Symbols for the target program itself (its own EXE) are also not");
					sb.AppendLine("      bundled. Only the program's developer can provide them, and they");
					sb.AppendLine("      must be placed next to the target EXE to be used.");
					sb.AppendLine();
					sb.AppendLine("  The rest of this report is still fully valid - it just shows addresses");
					sb.AppendLine("  and offsets instead of human-readable function names.");
				}
				sb.AppendLine();
			}

			sb.AppendLine("Performance at crash:");
			if (d.CrashWorkingSetMb >= 0) sb.AppendLine("  Working set  : " + d.CrashWorkingSetMb + " MB");
			if (d.CrashHandleCount >= 0) sb.AppendLine("  Handles      : " + d.CrashHandleCount);
			if (d.CrashGdiObjects >= 0) sb.AppendLine("  GDI objects  : " + d.CrashGdiObjects);
			if (d.CrashUserObjects >= 0) sb.AppendLine("  USER objects : " + d.CrashUserObjects);
			if (d.AvailableSystemMemoryMb >= 0) sb.AppendLine("  Free RAM     : " + d.AvailableSystemMemoryMb + " MB");
			if (d.FreeDiskSpaceMb >= 0) sb.AppendLine("  Free disk    : " + (d.FreeDiskSpaceMb / 1024) + " GB");

			bool hasEnv = d.SectionEntropyLines.Count > 0 || d.ThreadSnapshotLines.Count > 0
				|| !string.IsNullOrEmpty(d.ManifestContent)
				|| d.SuspiciousModules.Count > 0 || d.AntiCheatModules.Count > 0
				|| d.ApiHooksDetected.Count > 0 || d.ProcessEnvironment.Count > 0
				|| d.RelatedSystemEvents.Count > 0 || d.WerReportLines.Count > 0
				|| d.DelayLoadedDlls.Count > 0 || !string.IsNullOrEmpty(d.CompatibilityFlags)
				|| d.HasCompatibilityShim || d.InterestingStrings.Count > 0;

			if (hasEnv)
			{
				sb.AppendLine();
				sb.AppendLine("==================================================");
				sb.AppendLine(" ENVIRONMENT");
				sb.AppendLine("==================================================");

				if (d.SectionEntropyLines.Count > 0)
				{
					sb.AppendLine("Section entropy:");
					foreach (string s in d.SectionEntropyLines) sb.AppendLine(s);
					sb.AppendLine();
				}

				if (d.ThreadSnapshotLines.Count > 0)
				{
					sb.AppendLine("Threads at crash:");
					foreach (string s in d.ThreadSnapshotLines) sb.AppendLine(s);
					sb.AppendLine();
				}

				if (d.HasCompatibilityShim)
					sb.AppendLine("Compatibility : Windows applied compatibility shims to this program.");

				if (!string.IsNullOrEmpty(d.CompatibilityFlags))
					sb.AppendLine("Manual settings : " + d.CompatibilityFlags);

				if (d.DelayLoadedDlls.Count > 0)
				{
					sb.AppendLine("Delay-loaded DLLs:");
					foreach (string s in d.DelayLoadedDlls) sb.AppendLine("  - " + s);
					sb.AppendLine();
				}

				if (d.SuspiciousModules.Count > 0)
				{
					sb.AppendLine("Third-party modules (overlays/hooks) detected:");
					foreach (string m in d.SuspiciousModules) sb.AppendLine("  - " + m);
					sb.AppendLine();
				}

				if (d.AntiCheatModules.Count > 0)
				{
					sb.AppendLine("Anti-cheat systems detected:");
					foreach (string m in d.AntiCheatModules) sb.AppendLine("  - " + m);
					sb.AppendLine();
				}

				if (d.ApiHooksDetected.Count > 0)
				{
					sb.AppendLine("API hooks detected:");
					foreach (string h in d.ApiHooksDetected) sb.AppendLine("  - " + h);
					sb.AppendLine();
				}

				if (!string.IsNullOrEmpty(d.ManifestContent))
				{
					sb.AppendLine("Application manifest:");
					sb.AppendLine(d.ManifestContent);
					sb.AppendLine();
				}

				if (d.ProcessEnvironment.Count > 0)
				{
					sb.AppendLine("Process environment:");
					foreach (string s in d.ProcessEnvironment) sb.AppendLine("  " + s);
					sb.AppendLine();
				}

				if (d.RelatedSystemEvents.Count > 0)
				{
					sb.AppendLine("Related Windows Event Log entries:");
					foreach (WindowsEventEntry e in d.RelatedSystemEvents)
						sb.AppendLine("  [" + e.TimeGenerated.ToString("HH:mm:ss") + "] " + e.Source + " (" + e.Level + "): " + e.Message);
					sb.AppendLine();
				}

				if (d.WerReportLines.Count > 0)
				{
					sb.AppendLine("Windows Error Reporting entries:");
					foreach (string s in d.WerReportLines) sb.AppendLine("  " + s);
					sb.AppendLine();
				}

				if (d.InterestingStrings.Count > 0)
				{
					sb.AppendLine("Notable strings in the executable:");
					foreach (string s in d.InterestingStrings) sb.AppendLine("  " + s);
					sb.AppendLine();
				}
			}

			if (d.Recommendations.Count > 0)
			{
				sb.AppendLine();
				sb.AppendLine("==================================================");
				sb.AppendLine(" WHAT YOU SHOULD TRY");
				sb.AppendLine("==================================================");
				for (int i = 0; i < d.Recommendations.Count; i++)
					sb.AppendLine("  " + (i + 1) + ". " + d.Recommendations[i]);
			}

			// Last 15 events are the most useful context.
			if (recentEvents.Count > 0)
			{
				sb.AppendLine();
				sb.AppendLine("==================================================");
				sb.AppendLine(" LAST EVENTS LEADING UP TO THIS RESULT");
				sb.AppendLine("==================================================");
				int startAt = recentEvents.Count > 15 ? recentEvents.Count - 15 : 0;
				for (int i = startAt; i < recentEvents.Count; i++)
					sb.AppendLine("  " + recentEvents[i]);
			}

			sb.AppendLine();
			sb.AppendLine("==================================================");
			sb.AppendLine(" LIVE EVENT LOG");
			sb.AppendLine("==================================================");
			foreach (string line in allLogLines) sb.AppendLine(line);

			sb.AppendLine();
			sb.AppendLine("==================================================");
			sb.AppendLine(" End of report");
			sb.AppendLine("==================================================");

			return sb.ToString();
		}

		// Markdown version of the report, suitable for GitHub issues.
		// Tables and code fences are used so the layout stays readable.
		private string BuildFullMdReport(CrashReportData d)
		{
			StringBuilder md = new StringBuilder();

			md.AppendLine("# Automated Crash / Exit Diagnostic Report");
			md.AppendLine();
			md.AppendLine("| Property | Value |");
			md.AppendLine("|----------|-------|");
			md.AppendLine("| Target | `" + MdEsc(d.TargetPath) + "` |");
			md.AppendLine("| Started | " + d.StartedAt.ToString("yyyy-MM-dd HH:mm:ss") + " |");
			md.AppendLine("| Finished | " + d.FinishedAt.ToString("yyyy-MM-dd HH:mm:ss") + " |");
			md.AppendLine("| Architecture | " + MdEsc(d.Architecture ?? "Unknown") + " |");
			md.AppendLine("| Subsystem | " + MdEsc(d.SubsystemVersion ?? "n/a") + " |");
			md.AppendLine("| Entry point | `0x" + d.EntryPointRva.ToString("X8") + "` |");
			md.AppendLine("| Sections | " + d.NumberOfSections + " |");
			md.AppendLine("| DLL characteristics | " + MdEsc(d.DllCharacteristicsFlags ?? "(none)") + " |");
			md.AppendLine("| Managed (.NET) | " + (d.IsDotNetAssembly ? "Yes" : "No") + " |");
			if (!string.IsNullOrEmpty(d.Sha256Hash))
				md.AppendLine("| SHA256 | `" + MdEsc(d.Sha256Hash) + "` |");
			if (!string.IsNullOrEmpty(d.SignatureInfo))
				md.AppendLine("| Signature | " + MdEsc(d.SignatureInfo) + " |");
			if (!string.IsNullOrEmpty(d.DetectedEngine))
				md.AppendLine("| Engine | " + MdEsc(d.DetectedEngine) + " |");
			if (!string.IsNullOrEmpty(d.PackedHint))
				md.AppendLine("| Packer hint | " + MdEsc(d.PackedHint) + " |");
			md.AppendLine();

			md.AppendLine("## Problem");
			md.AppendLine();

			if (d.ResultLabel == "STOPPED")
			{
				md.AppendLine("> The program was stopped manually by the user.");
			}
			else if (d.ResultLabel == "CRASH")
			{
				md.AppendLine("> **CRASH** (unhandled exception) - the program stopped because of a fatal error.");
				md.AppendLine();
				md.AppendLine("| Property | Value |");
				md.AppendLine("|----------|-------|");
				md.AppendLine("| Status code | `" + MdEsc(d.StatusCodeHex) + "` |");
				md.AppendLine("| Meaning | " + MdEsc(d.Meaning) + " |");
				md.AppendLine("| Address | `" + MdEsc(d.AddressHex) + "` |");
				md.AppendLine("| Module | `" + MdEsc(d.ModuleName) + "` |");
				if (!string.IsNullOrEmpty(d.ModuleOffsetHex))
					md.AppendLine("| Offset | `" + MdEsc(d.ModuleOffsetHex) + "` |");
				if (d.SecondsUntilCrash >= 0)
					md.AppendLine("| Time to crash | " + d.SecondsUntilCrash + " second(s) after start |");
				if (d.HaveAccessInfo)
					md.AppendLine("| Access type | Attempted to " + MdEsc(d.AccessKind) + " memory at `" + MdEsc(d.AccessedAddressHex) + "` |");
				md.AppendLine();

				if (d.LikelyNullPointer)
				{
					md.AppendLine("**Diagnosis:** This is very likely a NULL or near-NULL pointer bug.");
					md.AppendLine();
					md.AppendLine("The code tried to use a pointer or object that was never initialized. This is a bug in the program itself - it cannot be fixed by reinstalling or changing system settings.");
				}
				else if (d.LikelyDepViolation)
				{
					md.AppendLine("**Diagnosis:** DEP (Data Execution Prevention) violation - the program tried to execute memory that Windows has marked as non-executable.");
					md.AppendLine();
					md.AppendLine("Common causes: packed/protected code, a security product injecting into the process, or a JIT compilation bug.");
				}
				else if (d.LikelyWriteToReadOnly)
				{
					md.AppendLine("**Diagnosis:** Attempted to write to read-only memory. This usually indicates an anti-tamper or DRM component trying to patch code that Windows has protected.");
				}
			}
			else if (d.ResultLabel == "SILENT EXIT")
			{
				md.AppendLine("> **SILENT EXIT** - the program terminated itself with code 0, very shortly after start.");
				md.AppendLine();
				md.AppendLine("This is almost always a deliberate self-termination (failed self-check, missing asset, invalid configuration, or an anti-tamper measure). It is NOT a normal shutdown.");
				if (haveLastFirstChance)
				{
					md.AppendLine();
					md.AppendLine("Last non-fatal exception before exit: `0x" + lastFirstChanceCode.ToString("X8") + "` (" + MdEsc(ExplainStatusCode(lastFirstChanceCode)) + ")");
				}
			}
			else if (d.ResultLabel == "ABNORMAL EXIT")
			{
				md.AppendLine("> **ABNORMAL EXIT** - the program terminated itself without a fatal exception.");
				md.AppendLine();
				md.AppendLine("| Property | Value |");
				md.AppendLine("|----------|-------|");
				md.AppendLine("| Status code | `" + MdEsc(d.StatusCodeHex) + "` |");
				md.AppendLine("| Meaning | " + MdEsc(d.Meaning) + " |");
			}
			else
			{
				md.AppendLine("> **CLEAN EXIT** - the program closed normally, no problems detected.");
			}

			if (!string.IsNullOrEmpty(d.FaultingInstructionText))
			{
				md.AppendLine();
				md.AppendLine("**Faulting code:**");
				md.AppendLine();
				md.AppendLine("```");
				md.AppendLine(d.FaultingInstructionText);
				md.AppendLine("```");
				if (!string.IsNullOrEmpty(d.FaultingInstructionNote))
				{
					md.AppendLine();
					md.AppendLine("> " + d.FaultingInstructionNote);
				}
			}

			if (d.HangDetected)
			{
				md.AppendLine();
				md.AppendLine("**Hang detected** - the target window became unresponsive (first at " + d.HangStartTime.ToString("HH:mm:ss") + ", total " + d.HangCount + " hang event(s)).");
			}

			if (d.ConfirmedMissingDlls.Count > 0 || d.IndirectMissingDlls.Count > 0 || d.ProbablyFineDlls.Count > 0)
			{
				md.AppendLine();
				md.AppendLine("## Missing Dependencies");
				md.AppendLine();

				if (d.ConfirmedMissingDlls.Count > 0)
				{
					md.AppendLine("### Confirmed Missing");
					md.AppendLine();
					foreach (string m in d.ConfirmedMissingDlls) md.AppendLine("- `" + MdEsc(m) + "`");
					md.AppendLine();
				}

				if (d.IndirectMissingDlls.Count > 0)
				{
					md.AppendLine("### Indirect Missing");
					md.AppendLine();
					md.AppendLine("These are needed by one of the program's own DLLs, not by the EXE directly:");
					md.AppendLine();
					foreach (string m in d.IndirectMissingDlls) md.AppendLine("- `" + MdEsc(m) + "`");
					md.AppendLine();
				}

				if (d.RedistributableSuggestions.Count > 0)
				{
					md.AppendLine("### Likely Fix");
					md.AppendLine();
					foreach (string fix in d.RedistributableSuggestions) md.AppendLine("- " + MdEsc(fix));
					md.AppendLine();
				}

				if (d.ProbablyFineDlls.Count > 0)
				{
					md.AppendLine("### Not Yet Loaded (file exists on disk)");
					md.AppendLine();
					foreach (string m in d.ProbablyFineDlls) md.AppendLine("- `" + MdEsc(m) + "`");
					md.AppendLine();
				}
			}

			md.AppendLine();
			md.AppendLine("## Technical Details");
			md.AppendLine();

			if (d.RegisterLines.Count > 0)
			{
				md.AppendLine("### CPU Registers at Crash");
				md.AppendLine();
				md.AppendLine("```");
				foreach (string line in d.RegisterLines) md.AppendLine(line);
				md.AppendLine("```");
				md.AppendLine();
			}

			if (!string.IsNullOrEmpty(miniDisassembly))
			{
				md.AppendLine("### Mini Disassembly Around Crash Site");
				md.AppendLine();
				if (!string.IsNullOrEmpty(miniDisassemblyEngine))
				{
					md.AppendLine("> Engine: " + miniDisassemblyEngine);
					md.AppendLine();
				}
				md.AppendLine("```asm");
				md.AppendLine(miniDisassembly.TrimEnd());
				md.AppendLine("```");
				md.AppendLine();
			}

			if (!string.IsNullOrEmpty(d.CrashRegionState))
			{
				md.AppendLine("### Memory Region at Crash Site");
				md.AppendLine();
				md.AppendLine("| Property | Value |");
				md.AppendLine("|----------|-------|");
				md.AppendLine("| Base | `0x" + d.CrashRegionBase.ToString("X") + "` |");
				md.AppendLine("| Size | `0x" + d.CrashRegionSize.ToString("X") + "` |");
				md.AppendLine("| State | " + MdEsc(d.CrashRegionState) + " |");
				md.AppendLine("| Protection | " + MdEsc(d.CrashRegionProtect) + " |");
				md.AppendLine("| Type | " + MdEsc(d.CrashRegionType) + " |");
				md.AppendLine();
				if (!string.IsNullOrEmpty(d.CrashRegionDescription))
				{
					md.AppendLine("> " + d.CrashRegionDescription);
					md.AppendLine();
				}
			}

			if (!string.IsNullOrEmpty(d.CrashSiteHexDump))
			{
				md.AppendLine("### Memory Around Crash Site");
				md.AppendLine();
				md.AppendLine("```");
				md.AppendLine(d.CrashSiteHexDump);
				md.AppendLine("```");
				if (!string.IsNullOrEmpty(d.CrashSitePattern))
				{
					md.AppendLine();
					md.AppendLine("**Pattern:** " + d.CrashSitePattern);
				}
				md.AppendLine();
			}

			if (!string.IsNullOrEmpty(d.StackHexDump))
			{
				md.AppendLine("### Stack Memory");
				md.AppendLine();
				if (d.StackRegionBase != 0)
				{
					md.AppendLine("Stack region: base `0x" + d.StackRegionBase.ToString("X") + "`, size `0x" + d.StackRegionSize.ToString("X") + "`");
					md.AppendLine();
				}
				md.AppendLine("```");
				md.AppendLine(d.StackHexDump);
				md.AppendLine("```");
				md.AppendLine();
			}

			if (d.CallStack.Count > 0)
			{
				md.AppendLine("### Call Stack");
				md.AppendLine();
				md.AppendLine("```");
				for (int i = 0; i < d.CallStack.Count; i++)
					md.AppendLine("#" + i + "  " + d.CallStack[i]);
				md.AppendLine("```");
				md.AppendLine();

				if (d.SymbolsWereResolved)
				{
					md.AppendLine("> Symbols were resolved from local symbol files. Symbol resolution is **offline-only** - the tool never contacts the internet at any point.");
				}
				else
				{
					md.AppendLine("> **Note on symbols:** Function names could not be resolved, so only module names and offsets are shown. This does not affect the accuracy of the analysis - the offset (for example `game.exe+0x1A4F2`) is stable across runs and machines, unlike a raw memory address.");
					md.AppendLine();
					md.AppendLine("Why symbols might be unavailable:");
					md.AppendLine();
					if (!string.IsNullOrEmpty(d.SymbolDiagnosticNote))
						md.AppendLine("- " + d.SymbolDiagnosticNote + ".");
					md.AppendLine("- Symbol resolution in this tool is **fully offline by design**: it never connects to the internet, and it does not use Microsoft's online symbol server. It only looks for symbol files (PDB) that already exist locally on this computer.");
					md.AppendLine("- Bundling all Windows symbol files with the tool is not practical: the full set for a single Windows build alone can exceed several gigabytes, would noticeably slow the tool down at startup, and would still not match a different Windows build running on another machine.");
					md.AppendLine("- Symbols for the target program itself (its own EXE) are also not bundled. Only the program's developer can provide them, and they must be placed next to the target EXE to be used.");
					md.AppendLine();
					md.AppendLine("The rest of this report is still fully valid - it just shows addresses and offsets instead of human-readable function names.");
				}
				md.AppendLine();
			}

			md.AppendLine("### Performance at Crash");
			md.AppendLine();
			md.AppendLine("| Metric | Value |");
			md.AppendLine("|--------|-------|");
			if (d.CrashWorkingSetMb >= 0) md.AppendLine("| Working set | " + d.CrashWorkingSetMb + " MB |");
			if (d.CrashHandleCount >= 0) md.AppendLine("| Handles | " + d.CrashHandleCount + " |");
			if (d.CrashGdiObjects >= 0) md.AppendLine("| GDI objects | " + d.CrashGdiObjects + " |");
			if (d.CrashUserObjects >= 0) md.AppendLine("| USER objects | " + d.CrashUserObjects + " |");
			if (d.AvailableSystemMemoryMb >= 0) md.AppendLine("| Free RAM | " + d.AvailableSystemMemoryMb + " MB |");
			if (d.FreeDiskSpaceMb >= 0) md.AppendLine("| Free disk | " + (d.FreeDiskSpaceMb / 1024) + " GB |");
			md.AppendLine();

			bool hasEnv = d.SectionEntropyLines.Count > 0 || d.ThreadSnapshotLines.Count > 0
				|| !string.IsNullOrEmpty(d.ManifestContent)
				|| d.SuspiciousModules.Count > 0 || d.AntiCheatModules.Count > 0
				|| d.ApiHooksDetected.Count > 0 || d.ProcessEnvironment.Count > 0
				|| d.RelatedSystemEvents.Count > 0 || d.WerReportLines.Count > 0
				|| d.DelayLoadedDlls.Count > 0 || !string.IsNullOrEmpty(d.CompatibilityFlags)
				|| d.HasCompatibilityShim || d.InterestingStrings.Count > 0;

			if (hasEnv)
			{
				md.AppendLine("## Environment");
				md.AppendLine();

				if (d.SectionEntropyLines.Count > 0)
				{
					md.AppendLine("### Section Entropy");
					md.AppendLine();
					md.AppendLine("```");
					foreach (string s in d.SectionEntropyLines) md.AppendLine(s);
					md.AppendLine("```");
					md.AppendLine();
				}

				if (d.ThreadSnapshotLines.Count > 0)
				{
					md.AppendLine("### Threads at Crash");
					md.AppendLine();
					md.AppendLine("```");
					foreach (string s in d.ThreadSnapshotLines) md.AppendLine(s);
					md.AppendLine("```");
					md.AppendLine();
				}

				if (d.HasCompatibilityShim || !string.IsNullOrEmpty(d.CompatibilityFlags))
				{
					md.AppendLine("### Compatibility Settings");
					md.AppendLine();
					if (d.HasCompatibilityShim)
						md.AppendLine("> Windows applied compatibility shims to this program.");
					if (!string.IsNullOrEmpty(d.CompatibilityFlags))
						md.AppendLine("> Manual settings: `" + MdEsc(d.CompatibilityFlags) + "`");
					md.AppendLine();
				}

				if (d.DelayLoadedDlls.Count > 0)
				{
					md.AppendLine("### Delay-Loaded DLLs");
					md.AppendLine();
					foreach (string s in d.DelayLoadedDlls) md.AppendLine("- `" + MdEsc(s) + "`");
					md.AppendLine();
				}

				if (d.SuspiciousModules.Count > 0)
				{
					md.AppendLine("### Third-Party Modules (Overlays/Hooks)");
					md.AppendLine();
					foreach (string m in d.SuspiciousModules) md.AppendLine("- " + MdEsc(m));
					md.AppendLine();
				}

				if (d.AntiCheatModules.Count > 0)
				{
					md.AppendLine("### Anti-Cheat Systems");
					md.AppendLine();
					foreach (string m in d.AntiCheatModules) md.AppendLine("- " + MdEsc(m));
					md.AppendLine();
				}

				if (d.ApiHooksDetected.Count > 0)
				{
					md.AppendLine("### API Hooks Detected");
					md.AppendLine();
					foreach (string h in d.ApiHooksDetected) md.AppendLine("- " + MdEsc(h));
					md.AppendLine();
				}

				if (!string.IsNullOrEmpty(d.ManifestContent))
				{
					md.AppendLine("### Application Manifest");
					md.AppendLine();
					md.AppendLine("```xml");
					md.AppendLine(d.ManifestContent);
					md.AppendLine("```");
					md.AppendLine();
				}

				if (d.ProcessEnvironment.Count > 0)
				{
					md.AppendLine("### Process Environment");
					md.AppendLine();
					md.AppendLine("```");
					foreach (string s in d.ProcessEnvironment) md.AppendLine(s);
					md.AppendLine("```");
					md.AppendLine();
				}

				if (d.RelatedSystemEvents.Count > 0)
				{
					md.AppendLine("### Related Windows Event Log Entries");
					md.AppendLine();
					md.AppendLine("| Time | Source | Level | Message |");
					md.AppendLine("|------|--------|-------|---------|");
					foreach (WindowsEventEntry e in d.RelatedSystemEvents)
						md.AppendLine("| " + e.TimeGenerated.ToString("HH:mm:ss") + " | " + MdEsc(e.Source) + " | " + MdEsc(e.Level) + " | " + MdEsc(e.Message) + " |");
					md.AppendLine();
				}

				if (d.WerReportLines.Count > 0)
				{
					md.AppendLine("### Windows Error Reporting");
					md.AppendLine();
					md.AppendLine("```");
					foreach (string s in d.WerReportLines) md.AppendLine(s);
					md.AppendLine("```");
					md.AppendLine();
				}

				if (d.InterestingStrings.Count > 0)
				{
					md.AppendLine("### Notable Strings in the Executable");
					md.AppendLine();
					md.AppendLine("```");
					foreach (string s in d.InterestingStrings) md.AppendLine(s);
					md.AppendLine("```");
					md.AppendLine();
				}
			}

			if (d.Recommendations.Count > 0)
			{
				md.AppendLine("## What You Should Try");
				md.AppendLine();
				for (int i = 0; i < d.Recommendations.Count; i++)
					md.AppendLine((i + 1) + ". " + MdEsc(d.Recommendations[i]));
				md.AppendLine();
			}

			if (recentEvents.Count > 0)
			{
				md.AppendLine("## Last Events Leading Up to This Result");
				md.AppendLine();
				md.AppendLine("```");
				int startAt = recentEvents.Count > 15 ? recentEvents.Count - 15 : 0;
				for (int i = startAt; i < recentEvents.Count; i++)
					md.AppendLine(recentEvents[i]);
				md.AppendLine("```");
				md.AppendLine();
			}

			md.AppendLine("## Live Event Log");
			md.AppendLine();
			md.AppendLine("```");
			foreach (string line in allLogLines) md.AppendLine(line);
			md.AppendLine("```");

			md.AppendLine();
			md.AppendLine("---");
			md.AppendLine();
			md.AppendLine("*End of report.*");

			return md.ToString();
		}

		// Minimal Markdown escaping for table cells and inline code.
		private static string MdEsc(string s)
		{
			if (string.IsNullOrEmpty(s)) return "";
			return s.Replace("|", "\\|").Replace("`", "'");
		}

		// Collects all captured data into a CrashReportData, then runs the
		// pattern analyzer and recommendation engine on it.
		private CrashReportData BuildDataForReports()
		{
			CrashReportData d = new CrashReportData();
			d.TargetPath = targetPath;
			d.StartedAt = sessionStartedAt;
			d.FinishedAt = sessionEndedAt != default(DateTime) ? sessionEndedAt : DateTime.Now;
			d.Architecture = currentPeInfo.Success ? currentPeInfo.MachineName : "Unknown";
			d.SubsystemVersion = currentPeInfo.Success ? (currentPeInfo.SubsystemName + " " + currentPeInfo.SubsystemMajor + "." + currentPeInfo.SubsystemMinor) : "n/a";
			d.TargetProcessor = currentPeInfo.Success ? currentPeInfo.MachineName : "Unknown";

			if (currentPeInfo.Success)
			{
				d.EntryPointRva = currentPeInfo.EntryPointRva;
				d.IsDotNetAssembly = currentPeInfo.IsDotNet;
				d.HasTlsCallbacks = currentPeInfo.HasTls;
				d.HasResources = currentPeInfo.HasResources;
				d.HasRelocations = currentPeInfo.HasRelocations;
				d.HasDebugInfo = currentPeInfo.HasDebugInfo;
				d.HasDelayImports = currentPeInfo.HasDelayImports;
				d.IsLikelyPacked = !string.IsNullOrEmpty(currentPeInfo.PackedHint);
				d.PackedHint = currentPeInfo.PackedHint;
				d.DllCharacteristicsFlags = currentPeInfo.DllCharacteristicsFlags;
				d.NumberOfSections = currentPeInfo.NumberOfSections;
				d.SectionInfo = currentPeInfo.SectionInfo;
			}

			if (userForcedStop) d.ResultLabel = "STOPPED";
			else if (wasFatalCrash) d.ResultLabel = "CRASH";
			else if (wasSilentExit) d.ResultLabel = "SILENT EXIT";
			else if (finalStatusCode != 0) d.ResultLabel = "ABNORMAL EXIT";
			else d.ResultLabel = "CLEAN EXIT";

			if (wasFatalCrash || finalStatusCode != 0)
			{
				d.StatusCodeHex = "0x" + finalStatusCode.ToString("X8");
				d.Meaning = ExplainStatusCode(finalStatusCode);
			}

			if (wasFatalCrash)
			{
				d.AddressHex = "0x" + crashAddress.ToString("X");
				d.ModuleName = crashModuleName;
				if (crashModuleBase != IntPtr.Zero)
				{
					d.ModuleOffsetHex = "0x" + crashModuleOffset.ToString("X");
				}
				d.HaveAccessInfo = haveAccessInfo;
				if (haveAccessInfo)
				{
					d.AccessKind = accessType == 0 ? "read" : (accessType == 1 ? "write" : "execute (DEP)");
					d.AccessedAddressHex = "0x" + accessedAddress.ToString("X");
					long addrValue = accessedAddress.ToInt64();
					d.LikelyNullPointer = addrValue >= 0 && addrValue < 0x10000;
					d.LikelyDepViolation = accessType == 8;
					d.LikelyWriteToReadOnly = accessType == 1;
				}
				d.CrashWorkingSetMb = crashWorkingSetBytes >= 0 ? crashWorkingSetBytes / 1024 / 1024 : -1;
				d.CrashHandleCount = crashHandleCount;
				d.CrashGdiObjects = crashGdiObjects;
				d.CrashUserObjects = crashUserObjects;
				d.SecondsUntilCrash = (long)(DateTime.Now - sessionStartedAt).TotalSeconds;
			}

			d.CallStack = capturedCallStack;
			d.SymbolDiagnosticNote = symbolDiagnosticNote;
			d.SymbolPathUsed = symbolPathUsed;
			d.SymbolsInitialized = symbolsInitialized;
			d.SymbolsWereResolved = symbolsWereResolved;
			d.ConfirmedMissingDlls = confirmedMissingDlls;
			d.IndirectMissingDlls = indirectMissingDlls;
			d.ProbablyFineDlls = probablyFineDlls;
			d.RedistributableSuggestions = redistributableSuggestions;
			d.PeChecksumWarning = peChecksumWarning;
			d.AvailableSystemMemoryMb = crashAvailableSystemMemoryMb;
			d.FreeDiskSpaceMb = crashFreeDiskSpaceMb;
			d.SuspiciousModules = suspiciousModulesFound;
			d.AntiCheatModules = antiCheatModulesFound;
			d.HasCompatibilityShim = HasCompatibilityShim();
			d.CompatibilityFlags = compatibilityFlagsInfo;
			d.SignatureInfo = signatureInfo;
			d.AllLogLines = allLogLines;
			d.HangDetected = hangDetected || (hangCount > 0);
			d.HangStartTime = hangStartTime;
			d.HangCount = hangCount;
			d.RelatedSystemEvents = relatedSystemEvents;
			d.SteamApiLoaded = steamApiLoaded;
			d.SteamClientRunning = steamClientRunning;

			d.CrashRegionState = crashRegionState;
			d.CrashRegionProtect = crashRegionProtect;
			d.CrashRegionType = crashRegionType;
			d.CrashRegionDescription = crashRegionDescription;
			d.CrashRegionBase = crashRegionBase;
			d.CrashRegionSize = crashRegionSize;
			d.CrashSiteHexDump = crashSiteHexDump;
			d.CrashSitePattern = crashSitePattern;
			d.CrashSiteMemoryFilePath = crashSiteMemoryFilePath != null ? Path.GetFileName(crashSiteMemoryFilePath) : null;
			d.FaultingInstructionText = faultingInstructionText;
			d.FaultingInstructionNote = faultingInstructionNote;
			d.FaultingInstructionLength = faultingInstructionLength;
			d.SectionEntropyLines = sectionEntropyLines;
			d.CompanionLogContents = companionLogContents;
			d.ThreadSnapshotLines = threadSnapshotLines;
			d.InterestingStrings = interestingStrings;

			d.RegisterLines = registerLines;
			d.StackHexDump = stackHexDump;
			d.StackRegionBase = stackRegionBase;
			d.StackRegionSize = stackRegionSize;
			d.DetectedEngine = detectedEngine;
			d.ApiHooksDetected = apiHooksDetected;
			d.ProcessEnvironment = processEnvironment;
			d.WerReportLines = werReportLines;
			d.ManifestContent = manifestContent;

			try { d.DelayLoadedDlls = PeImportReader.GetDelayImportedDllNames(targetPath); }
			catch { }

			if (!string.IsNullOrEmpty(crashScreenshotPath))
			{
				d.ScreenshotFileName = Path.GetFileName(crashScreenshotPath);
			}
			if (!string.IsNullOrEmpty(crashMiniDumpPath))
			{
				d.MiniDumpFileName = Path.GetFileName(crashMiniDumpPath);
			}

			try { d.InDownloadsFolder = targetPath.IndexOf("Downloads", StringComparison.OrdinalIgnoreCase) >= 0; }
			catch { d.InDownloadsFolder = false; }

			// Final heuristic pass and recommendations.
			d.KnownIssueMatches = CrashPatternAnalyzer.Analyze(d);
			d.Recommendations = RecommendationEngine.BuildRecommendations(d);

			try
			{
				if (string.IsNullOrEmpty(sha256Hash) && !string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
					sha256Hash = ComputeSha256(targetPath);
				d.Sha256Hash = sha256Hash;
			}
			catch { }

			return d;
		}

		// Status codes that always imply a missing/broken dependency.
		private bool IsDependencyRelatedStatus(uint code)
		{
			return code == 0xC0000135 || code == 0xC0000139 || code == 0xC0000142 || code == 0xC000007B;
		}

		// Checks the standard search paths for a DLL file.
		private bool FileExistsOnDisk(string dllName, string gameDir)
		{
			try
			{
				if (File.Exists(Path.Combine(gameDir, dllName))) return true;
				string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
				if (File.Exists(Path.Combine(system32, dllName))) return true;
				string windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
				string sysWow64 = Path.Combine(windowsDir, "SysWOW64");
				if (File.Exists(Path.Combine(sysWow64, dllName))) return true;
			}
			catch
			{
			}
			return false;
		}

		// True if any loaded module is a Windows compatibility shim.
		private bool HasCompatibilityShim()
		{
			foreach (string path in loadedModuleNames.Values)
			{
				string fileName = Path.GetFileName(path).ToLowerInvariant();
				if (fileName == "aclayers.dll" || fileName == "acgenral.dll" || fileName == "acspecfc.dll")
					return true;
			}
			return false;
		}

		#endregion

		#region Status / Exception Code Dictionary

		// Maps a Windows status code (NTSTATUS or Win32) to a short
		// explanation. Also unwraps 0x8007xxxx into the underlying Win32
		// error when applicable.
		private string ExplainStatusCode(uint code)
		{
			if ((code & 0xFFFF0000) == 0x80070000)
			{
				int win32Code = (int)(code & 0xFFFF);
				string win32Meaning = ExplainWin32Error(win32Code);
				if (win32Code == 1001)
					return "Stack Overflow (wrapped Win32 error - typically .NET runtime terminating the process after a stack overflow)";
				return "Wrapped Win32 error " + win32Code + " - " + win32Meaning;
			}

			switch (code)
			{
				case 0xC0000005: return "Access Violation - the program tried to use invalid memory (likely a bad or null pointer)";
				case 0xC00000FD: return "Stack Overflow - likely infinite or excessively deep recursion";
				case 0xC0000094: return "Integer Divide By Zero";
				case 0xC000001D: return "Illegal Instruction - invalid CPU instruction encountered";
				case 0xC0000135: return "DLL Not Found - a required library could not be located";
				case 0xC0000139: return "Entry Point Not Found - an incompatible version of a library is present";
				case 0xC0000142: return "DLL Initialization Failed";
				case 0xC000007B: return "Bad Image Format - a required DLL is the wrong architecture (32-bit vs 64-bit) or is corrupted";
				case 0x80000003: return "Breakpoint - a debug breakpoint (usually not a real error)";
				case 0xE06D7363: return "C++ Exception (thrown from native C++ code)";
				case 0xC0000409: return "Stack Buffer Overrun detected (security check failure)";
				case 0xC0000025: return "Non-continuable Exception";
				case 0xC0000096: return "Privileged Instruction - attempted to execute a kernel-mode instruction";
				case 0xC0000090: return "Floating Point Invalid Operation";
				case 0xC0000091: return "Floating Point Overflow";
				case 0xC0000092: return "Floating Point Stack Check";
				case 0xC0000008: return "Invalid Handle - handle already closed or never valid";
				case 0xC0000017: return "Not Enough Memory - allocation request could not be satisfied";
				case 0xC0000028: return "Bad Stack - invalid stack encountered during an exception";
				case 0xC000009A: return "Insufficient System Resources - handles, memory, or GDI objects exhausted";
				case 0xC0000221: return "Image Checksum Mismatch - corrupted or modified file";
				case 0xC0000603: return "Invalid Image Hash - signature/integrity check failed";
				case 0xC0000022: return "Access Denied - permission was refused";
				case 0xC0000034: return "Object Name Not Found - looked for a file/resource that doesn't exist";
				case 0xC000003A: return "Path Not Found";
				case 0xC0000043: return "Sharing Violation - file being used exclusively by another process";
				case 0xC0000602: return "Fail Fast Exception - deliberate self-termination on unrecoverable error";
				case 0xC00002B4: return "Direct3D Device Removed/Hung - GPU stopped responding";
				case 0x887A0005: return "DXGI Device Removed - graphics driver crashed or was reset";
				case 0x887A0006: return "DXGI Device Hung - GPU stopped responding";
				case 0xC0000813: return "Assembly/Manifest Error - required SxS assembly missing or misconfigured";
				case 0xC000026B: return "Application Manifest Error";
				case 0xE0434352: return ".NET CLR Exception - unhandled exception in managed code";
				case 0x4000001F: return "First-chance notification on WOW64 startup (routine, not an error)";
				case 0x04242420: return ".NET debugger notification (routine)";
				case 0xC0000420: return "Assertion Failure - internal self-check failed";
				case 0xC000008C: return "Array Bounds Exceeded";
				case 0xC0000006: return "In-Page I/O Error - disk read failure";
				case 0xC0000374: return "Heap Corruption Detected - memory allocator damaged";
				case 0xC0000417: return "Invalid CRT Parameter";
				case 0x80000001: return "Guard Page Violation - stack growth detected";
				case 0x80000002: return "Datatype Misalignment";
				case 0x80000004: return "Single Step (debugger notification)";
				default: return "Code not in the current known list - may need manual lookup";
			}
		}

		// Plain Win32 error code -> short meaning. Used for CreateProcess
		// failures and for unwrapped 0x8007xxxx codes.
		private string ExplainWin32Error(int code)
		{
			switch (code)
			{
				case 2: return "File Not Found";
				case 5: return "Access Denied - try running as Administrator, or check antivirus";
				case 193: return "Bad EXE Format - architecture mismatch or corrupted file";
				case 216: return "This program requires a different/newer processor";
				case 740: return "Elevation Required - needs Administrator";
				case 1157: return "A required DLL could not be found";
				case 1001: return "Stack Overflow - recursion went too deep";
				case 126: return "Module Not Found - a required DLL is missing or has a bad path";
				case 127: return "Procedure Not Found - a DLL is present but missing an expected export";
				case 998: return "Invalid Access to Memory Location";
				case 487: return "Attempt to access invalid address";
				default: return "Windows error code not in the current known list";
			}
		}

		#endregion

		#region Logging

		// Adds a line to the session log and mirrors it in the list view.
		// The call is safe from any thread.
		private void LogLine(string text)
		{
			string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text;

			try { recentEvents.Add(line); } catch { }
			if (recentEvents.Count > RECENT_EVENTS_LIMIT) recentEvents.RemoveAt(0);

			try { allLogLines.Add(line); } catch { }

			if (this.InvokeRequired)
			{
				try { this.BeginInvoke(new MethodInvoker(delegate { AppendToUI(line); })); }
				catch { }
			}
			else
			{
				AppendToUI(line);
			}
		}

		// Appends to the UI list view, respecting the current search filter.
		private void AppendToUI(string line)
		{
			try
			{
				string filter = textBoxSearch.Text;
				if (!string.IsNullOrEmpty(filter) && line.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
					return;
				AddColoredItem(line);
			}
			catch
			{
			}
		}

		// Adds a colored row to the log list view and scrolls it into view.
		private void AddColoredItem(string line)
		{
			try
			{
				ListViewItem item = new ListViewItem(line);
				item.ForeColor = GetLineColor(line);
				listView1.Items.Add(item);
				if (listView1.Items.Count > 0) listView1.EnsureVisible(listView1.Items.Count - 1);
			}
			catch
			{
			}
		}

		// Keyword-based coloring for the log list view. Matching is done
		// by exact substring, so a line like "[!!! FATAL CRASH !!!]"
		// always wins over the generic rules.
		private Color GetLineColor(string line)
		{
			if (darkModeEnabled)
			{
				if (line.Contains("FATAL CRASH") || line.Contains("Abnormal Exit") || line.Contains("Silent Exit")) return Color.FromArgb(255, 107, 107);
				if (line.Contains("[MISSING-DLL]")) return Color.FromArgb(255, 180, 0);
				if (line.Contains("[Error]") || line.Contains("[Warning]")) return Color.FromArgb(220, 220, 154);
				if (line.Contains("Exception - Non-fatal")) return Color.FromArgb(220, 220, 154);
				if (line.Contains("[Hang]")) return Color.FromArgb(255, 165, 0);
				if (line.Contains("[WOW64]")) return Color.FromArgb(128, 128, 128);
				if (line.Contains("[Watchdog]")) return Color.FromArgb(200, 130, 255);
				if (line.Contains("closed normally") || line.Contains("created successfully") || line.Contains("Stopped manually"))
					return Color.FromArgb(106, 153, 85);
				if (line.Contains("[Info]")) return Color.FromArgb(156, 220, 254);
				return Color.FromArgb(212, 212, 212);
			}
			else
			{
				if (line.Contains("FATAL CRASH") || line.Contains("Abnormal Exit") || line.Contains("Silent Exit")) return Color.DarkRed;
				if (line.Contains("[MISSING-DLL]")) return Color.DarkOrange;
				if (line.Contains("[Error]") || line.Contains("[Warning]") || line.Contains("Exception - Non-fatal")) return Color.DarkOrange;
				if (line.Contains("[Hang]")) return Color.OrangeRed;
				if (line.Contains("[WOW64]")) return Color.Gray;
				if (line.Contains("[Watchdog]")) return Color.MediumPurple;
				if (line.Contains("closed normally") || line.Contains("created successfully") || line.Contains("Stopped manually")) return Color.DarkGreen;
				if (line.Contains("[Info]")) return Color.DarkBlue;
				return Color.Black;
			}
		}

		#endregion
	}

	// ---------------------------------------------------------------------
	// Custom menu renderer so the MenuStrip respects theme colors on all
	// Windows versions.
	// ---------------------------------------------------------------------
	internal sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
	{
		private Color backColor;
		private Color textColor;
		private Color hoverColor;
		private Color borderColor;

		public ThemedMenuRenderer(Color back, Color text, Color hover, Color border)
			: base(new ThemedColorTable(back, hover))
		{
			this.backColor = back;
			this.textColor = text;
			this.hoverColor = hover;
			this.borderColor = border;
		}

		// Force our text color on every menu item, ignoring the theme
		// defaults the base renderer would use.
		protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
		{
			e.TextColor = textColor;
			base.OnRenderItemText(e);
		}

		// Flat fill for the menu strip background.
		protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
		{
			try
			{
				using (SolidBrush b = new SolidBrush(backColor))
					e.Graphics.FillRectangle(b, e.AffectedBounds);
			}
			catch { }
		}

		// Highlight only the selected / pressed item; leave the rest flat.
		protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
		{
			try
			{
				if (e.Item.Selected || e.Item.Pressed)
				{
					using (SolidBrush b = new SolidBrush(hoverColor))
						e.Graphics.FillRectangle(b, new Rectangle(Point.Empty, e.Item.Size));
				}
				else
				{
					base.OnRenderMenuItemBackground(e);
				}
			}
			catch { }
		}

		// Single bottom border line, drawn in our border color.
		protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
		{
			try
			{
				using (Pen p = new Pen(borderColor))
					e.Graphics.DrawLine(p, 0, e.ToolStrip.Height - 1, e.ToolStrip.Width, e.ToolStrip.Height - 1);
			}
			catch { }
		}
	}

	// Color table that maps every professional-renderer color we care
	// about onto the current theme.
	internal sealed class ThemedColorTable : ProfessionalColorTable
	{
		private Color backColor;
		private Color hoverColor;

		public ThemedColorTable(Color back, Color hover)
		{
			this.backColor = back;
			this.hoverColor = hover;
		}

		public override Color MenuItemSelected { get { return hoverColor; } }
		public override Color MenuItemSelectedGradientBegin { get { return hoverColor; } }
		public override Color MenuItemSelectedGradientEnd { get { return hoverColor; } }
		public override Color MenuItemPressedGradientBegin { get { return hoverColor; } }
		public override Color MenuItemPressedGradientMiddle { get { return hoverColor; } }
		public override Color MenuItemPressedGradientEnd { get { return hoverColor; } }
		public override Color MenuItemBorder { get { return hoverColor; } }
		public override Color MenuBorder { get { return backColor; } }
		public override Color ToolStripDropDownBackground { get { return backColor; } }
		public override Color ImageMarginGradientBegin { get { return backColor; } }
		public override Color ImageMarginGradientMiddle { get { return backColor; } }
		public override Color ImageMarginGradientEnd { get { return backColor; } }
		public override Color SeparatorDark { get { return hoverColor; } }
		public override Color SeparatorLight { get { return backColor; } }
	}
}