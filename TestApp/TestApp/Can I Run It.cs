/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen
 * 
 * Program Readiness & Protection Analyzer window.
 *
 * Static analyzer (no execution, no debugger, no hooks). Works on
 * both 32-bit and 64-bit EXE/DLL files. Follows the main form's theme
 * in real time via MainForm.IsDarkMode.
 */
using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace TestApp
{
	public partial class Can_I_Run_It : Form
	{
		// Top bar controls: target path, browse, analyze.
		private TextBox txtTarget;
		private Button btnBrowse;
		private Button btnAnalyze;
		private Button btnSave;

		// Results grid and status/caption labels.
		private ListView lvResults;
		private Label lblStatus;
		private Label lblCaption;

		// Cached last analysis, used by the Save report button.
		private ReadinessResult lastResult;

		public Can_I_Run_It()
		{
			InitializeComponent();
			BuildUI();
			ApplyTheme();
		}

		// Lets the caller pre-fill the target path (e.g. from MainForm).
		public void SetInitialTarget(string path)
		{
			try { txtTarget.Text = path; } catch { }
		}

		// Called by MainForm when the theme changes. Marshals to the UI
		// thread when needed.
		public void RefreshTheme()
		{
			if (this.IsDisposed) return;
			try
			{
				if (this.InvokeRequired)
					this.BeginInvoke(new MethodInvoker(ApplyTheme));
				else
					ApplyTheme();
			}
			catch { }
		}

		// Swallow F11 to avoid the form entering an unexpected fullscreen
		// mode via the default Form handling.
		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			if (keyData == Keys.F11) return true;
			return base.ProcessCmdKey(ref msg, keyData);
		}

		// Builds all child controls in code (no designer layout).
		private void BuildUI()
		{
			this.Text = "Can I Run It? - Program Readiness & Protection Analyzer";
			this.ClientSize = new Size(1020, 640);
			this.StartPosition = FormStartPosition.CenterScreen;
			this.FormBorderStyle = FormBorderStyle.FixedSingle;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.ShowIcon = false;
			this.Font = new Font("Segoe UI", 9f);

			lblCaption = new Label();
			lblCaption.Text = "Target file:";
			lblCaption.Location = new Point(12, 15);
			lblCaption.Size = new Size(75, 20);
			this.Controls.Add(lblCaption);

			txtTarget = new TextBox();
			txtTarget.Location = new Point(92, 12);
			txtTarget.Size = new Size(700, 22);
			txtTarget.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			this.Controls.Add(txtTarget);

			btnBrowse = new Button();
			btnBrowse.Text = "Browse...";
			btnBrowse.Location = new Point(800, 10);
			btnBrowse.Size = new Size(90, 26);
			btnBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			btnBrowse.Click += new EventHandler(BtnBrowseClick);
			this.Controls.Add(btnBrowse);

			btnAnalyze = new Button();
			btnAnalyze.Text = "Analyze";
			btnAnalyze.Location = new Point(898, 10);
			btnAnalyze.Size = new Size(110, 26);
			btnAnalyze.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			btnAnalyze.Click += new EventHandler(BtnAnalyzeClick);
			this.Controls.Add(btnAnalyze);

			// Results list: grouped, detailed view, four columns.
			lvResults = new ListView();
			lvResults.Location = new Point(12, 46);
			lvResults.Size = new Size(996, 530);
			lvResults.View = View.Details;
			lvResults.FullRowSelect = true;
			lvResults.GridLines = true;
			lvResults.ShowGroups = true;
			lvResults.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			lvResults.Columns.Add("Status", 70);
			lvResults.Columns.Add("Item", 220);
			lvResults.Columns.Add("Requirement", 300);
			lvResults.Columns.Add("Your System", 400);
			this.Controls.Add(lvResults);

			lblStatus = new Label();
			lblStatus.Location = new Point(12, 588);
			lblStatus.Size = new Size(830, 20);
			lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			lblStatus.Text = "Ready.";
			this.Controls.Add(lblStatus);

			btnSave = new Button();
			btnSave.Text = "Save report...";
			btnSave.Location = new Point(886, 584);
			btnSave.Size = new Size(122, 26);
			btnSave.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
			btnSave.Click += new EventHandler(BtnSaveClick);
			btnSave.Enabled = false;
			this.Controls.Add(btnSave);
		}

		// Reads the current theme from MainForm and repaints every control.
		private void ApplyTheme()
		{
			bool dark = false;
			try { dark = MainForm.IsDarkMode; } catch { }

			Color formBack, panelBack, listBack, text, border;

			if (dark)
			{
				formBack = Color.FromArgb(18, 18, 18);
				panelBack = Color.FromArgb(30, 30, 30);
				listBack = Color.FromArgb(24, 24, 24);
				text = Color.FromArgb(224, 224, 224);
				border = Color.FromArgb(60, 60, 60);
			}
			else
			{
				formBack = Color.White;
				panelBack = Color.FromArgb(240, 240, 240);
				listBack = Color.White;
				text = Color.FromArgb(26, 26, 26);
				border = Color.FromArgb(200, 200, 200);
			}

			this.BackColor = formBack;
			this.ForeColor = text;

			lblCaption.ForeColor = text;
			lblStatus.ForeColor = text;

			txtTarget.BackColor = panelBack;
			txtTarget.ForeColor = text;

			lvResults.BackColor = listBack;
			lvResults.ForeColor = text;

			Button[] allButtons = new Button[] { btnBrowse, btnAnalyze, btnSave };
			foreach (Button b in allButtons)
			{
				b.UseVisualStyleBackColor = false;
				b.FlatStyle = FlatStyle.Flat;
				b.FlatAppearance.BorderColor = border;
				b.BackColor = panelBack;
				b.ForeColor = text;
			}

			// Recolor existing items according to their status
			foreach (ListViewItem it in lvResults.Items)
			{
				it.ForeColor = GetItemColor(it, dark);
			}
		}

		// Maps the status column ("OK", "FAIL", "WARN", "INFO") to a color.
		private Color GetItemColor(ListViewItem it, bool dark)
		{
			if (it.SubItems.Count < 1) return dark ? Color.FromArgb(212, 212, 212) : Color.Black;
			string status = it.SubItems[0].Text;

			switch (status)
			{
				case "OK":
					return dark ? Color.FromArgb(106, 153, 85) : Color.DarkGreen;
				case "FAIL":
					return dark ? Color.FromArgb(255, 107, 107) : Color.DarkRed;
				case "WARN":
					return dark ? Color.FromArgb(220, 220, 154) : Color.DarkOrange;
				case "INFO":
					return dark ? Color.FromArgb(156, 220, 254) : Color.DarkBlue;
				default:
					return dark ? Color.FromArgb(212, 212, 212) : Color.Black;
			}
		}

		// File picker for the target EXE/DLL.
		private void BtnBrowseClick(object sender, EventArgs e)
		{
			OpenFileDialog dlg = new OpenFileDialog();
			dlg.Filter = "Executables and libraries (*.exe;*.dll)|*.exe;*.dll|All files (*.*)|*.*";
			dlg.Title = "Select the program to analyze";
			if (dlg.ShowDialog(this) == DialogResult.OK)
			{
				txtTarget.Text = dlg.FileName;
			}
		}

		// Runs the static analyzer and pushes the results into the list.
		private void BtnAnalyzeClick(object sender, EventArgs e)
		{
			string path = txtTarget.Text.Trim();
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				MessageBox.Show(this, "Please select a valid file first.", "Notice",
					MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			lvResults.Items.Clear();
			lvResults.Groups.Clear();
			lblStatus.Text = "Analyzing...";
			btnAnalyze.Enabled = false;
			Application.DoEvents();

			try
			{
				ReadinessResult result = ReadinessAnalyzer.Analyze(path);
				lastResult = result;
				DisplayResult(result);
				btnSave.Enabled = true;
			}
			catch (Exception ex)
			{
				lblStatus.Text = "Analysis failed: " + ex.Message;
			}
			finally
			{
				btnAnalyze.Enabled = true;
			}
		}

		// Adds the seven groups, counts statuses and updates the summary line.
		private void DisplayResult(ReadinessResult r)
		{
			int okCount = 0, failCount = 0, warnCount = 0;

			AddGroup("System & Architecture", r.System, ref okCount, ref failCount, ref warnCount);
			AddGroup("Runtimes & Libraries", r.Runtimes, ref okCount, ref failCount, ref warnCount);
			AddGroup("Permissions", r.Permissions, ref okCount, ref failCount, ref warnCount);
			AddGroup("Protection / DRM", r.Protection, ref okCount, ref failCount, ref warnCount);
			AddGroup("Language / Compiler", r.Fingerprint, ref okCount, ref failCount, ref warnCount);
			AddGroup("Capabilities", r.Capabilities, ref okCount, ref failCount, ref warnCount);
			AddGroup("Risk Indicators", r.Risks, ref okCount, ref failCount, ref warnCount);

			string summary = string.Format(
				"Architecture: {0}  |  Subsystem: {1}  |  .NET: {2}  |  Signed: {3}  |  OK: {4}  |  FAIL: {5}  |  WARN: {6}",
				r.Architecture, r.SubsystemVersion,
				r.IsDotNet ? "Yes" : "No",
				r.IsSigned ? "Yes" : "No",
				okCount, failCount, warnCount);
			lblStatus.Text = summary;

			ApplyTheme();
		}

		// Inserts one ListViewGroup and appends its items, updating counters.
		private void AddGroup(string title, System.Collections.Generic.List<ReadinessItem> items,
			ref int okCount, ref int failCount, ref int warnCount)
		{
			if (items == null || items.Count == 0) return;

			ListViewGroup grp = new ListViewGroup(title);
			lvResults.Groups.Add(grp);

			foreach (ReadinessItem item in items)
			{
				if (item.Status == "OK") okCount++;
				else if (item.Status == "FAIL") failCount++;
				else if (item.Status == "WARN") warnCount++;

				ListViewItem it = new ListViewItem(item.Status);
				it.SubItems.Add(item.Item ?? "");
				it.SubItems.Add(item.Requirement ?? "");
				it.SubItems.Add(item.YourSystem ?? "");
				it.Group = grp;
				lvResults.Items.Add(it);
			}
		}

		// Exports the cached analysis as a UTF-8 text report.
		private void BtnSaveClick(object sender, EventArgs e)
		{
			if (lastResult == null) return;

			SaveFileDialog dlg = new SaveFileDialog();
			dlg.Filter = "Text report (*.txt)|*.txt|All files (*.*)|*.*";
			dlg.FileName = Path.GetFileNameWithoutExtension(lastResult.TargetPath) + "_readiness.txt";
			if (dlg.ShowDialog(this) != DialogResult.OK) return;

			try
			{
				StringBuilder sb = new StringBuilder();
				sb.AppendLine("==================================================");
				sb.AppendLine(" Program Readiness & Protection Report");
				sb.AppendLine("==================================================");
				sb.AppendLine("Target       : " + lastResult.TargetPath);
				sb.AppendLine("Architecture : " + lastResult.Architecture);
				sb.AppendLine("Subsystem    : " + lastResult.SubsystemVersion);
				sb.AppendLine("Managed .NET : " + (lastResult.IsDotNet ? "Yes" : "No"));
				sb.AppendLine("Digitally signed : " + (lastResult.IsSigned ? "Yes" : "No"));
				sb.AppendLine("Analyzed at  : " + lastResult.ScannedAt.ToString("yyyy-MM-dd HH:mm:ss"));
				sb.AppendLine();

				WriteSection(sb, "SYSTEM & ARCHITECTURE", lastResult.System);
				WriteSection(sb, "RUNTIMES & LIBRARIES", lastResult.Runtimes);
				WriteSection(sb, "PERMISSIONS", lastResult.Permissions);
				WriteSection(sb, "PROTECTION / DRM", lastResult.Protection);
				WriteSection(sb, "LANGUAGE / COMPILER", lastResult.Fingerprint);
				WriteSection(sb, "CAPABILITIES", lastResult.Capabilities);
				WriteSection(sb, "RISK INDICATORS", lastResult.Risks);

				sb.AppendLine("==================================================");
				sb.AppendLine(" End of report");
				sb.AppendLine("==================================================");

				File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
				MessageBox.Show(this, "Report saved successfully.", "Saved",
					MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "Failed to save report: " + ex.Message, "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		// Writes one block of the report: header plus one paragraph per item.
		private void WriteSection(StringBuilder sb, string title, System.Collections.Generic.List<ReadinessItem> items)
		{
			if (items == null || items.Count == 0) return;
			sb.AppendLine("--------------------------------------------------");
			sb.AppendLine(" " + title);
			sb.AppendLine("--------------------------------------------------");
			foreach (ReadinessItem item in items)
			{
				sb.AppendLine("  [" + item.Status.PadRight(4) + "] " + item.Item);
				sb.AppendLine("         Requirement : " + (item.Requirement ?? ""));
				sb.AppendLine("         Your System : " + (item.YourSystem ?? ""));
			}
			sb.AppendLine();
		}
	}
}