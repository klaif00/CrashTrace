/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen 
 * 
 * A small standalone window that lets the user pick a file, run a
 * static dependency scan on it, and view / save the results.
 *
 * This does NOT launch the target or attach to it. It is completely
 * independent of the debugger, so it can even be used on programs
 * that cannot be run at all (wrong architecture, missing dependency,
 * corrupted installer output, etc.).
 *
 * Theme is applied on demand via RefreshTheme(), which the main form
 * calls whenever its own theme changes.
 */
using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace TestApp
{
	internal class DllScanForm : Form
	{
		// Top bar controls.
		private TextBox txtTarget;
		private Button btnBrowse;
		private Button btnScan;
		private Button btnSave;

		// Results list and status/caption labels.
		private ListView lvResults;
		private Label lblStatus;
		private Label lblCaption;

		// Cached last scan, used by the Save report button.
		private DllScanResult lastResult;

		public DllScanForm()
		{
			BuildUI();
			ApplyTheme();
		}

		// Lets the caller pre-fill the target path (e.g. from MainForm).
		public void SetInitialTarget(string path)
		{
			try { txtTarget.Text = path; } catch { }
		}

		// Called by MainForm when its theme changes.
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

		// Block F11 (fullscreen) explicitly. Other keys pass through.
		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			if (keyData == Keys.F11) return true;
			return base.ProcessCmdKey(ref msg, keyData);
		}

		// Builds all child controls in code (no designer layout).
		private void BuildUI()
		{
			this.Text = "DLL Dependency Scanner";
			this.ClientSize = new Size(920, 600);
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
			txtTarget.Size = new Size(626, 22);
			txtTarget.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			this.Controls.Add(txtTarget);

			btnBrowse = new Button();
			btnBrowse.Text = "Browse...";
			btnBrowse.Location = new Point(726, 10);
			btnBrowse.Size = new Size(85, 26);
			btnBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			btnBrowse.Click += new EventHandler(BtnBrowseClick);
			this.Controls.Add(btnBrowse);

			btnScan = new Button();
			btnScan.Text = "Scan";
			btnScan.Location = new Point(819, 10);
			btnScan.Size = new Size(85, 26);
			btnScan.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			btnScan.Click += new EventHandler(BtnScanClick);
			this.Controls.Add(btnScan);

			// Results grid: grouped, detailed view, three columns.
			lvResults = new ListView();
			lvResults.Location = new Point(12, 46);
			lvResults.Size = new Size(892, 490);
			lvResults.View = View.Details;
			lvResults.FullRowSelect = true;
			lvResults.GridLines = true;
			lvResults.ShowGroups = true;
			lvResults.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			lvResults.Columns.Add("Status", 90);
			lvResults.Columns.Add("Name", 260);
			lvResults.Columns.Add("Source / Location", 530);
			this.Controls.Add(lvResults);

			lblStatus = new Label();
			lblStatus.Location = new Point(12, 546);
			lblStatus.Size = new Size(700, 20);
			lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			lblStatus.Text = "Ready.";
			this.Controls.Add(lblStatus);

			btnSave = new Button();
			btnSave.Text = "Save report...";
			btnSave.Location = new Point(782, 543);
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

			Button[] allButtons = new Button[] { btnBrowse, btnScan, btnSave };
			foreach (Button b in allButtons)
			{
				b.UseVisualStyleBackColor = false;
				b.FlatStyle = FlatStyle.Flat;
				b.FlatAppearance.BorderColor = border;
				b.BackColor = panelBack;
				b.ForeColor = text;
			}

			// Keep red/green status colors consistent with the current mode.
			foreach (ListViewItem it in lvResults.Items)
			{
				if (it.SubItems.Count < 1) continue;
				string status = it.SubItems[0].Text;
				if (status == "MISSING")
					it.ForeColor = dark ? Color.FromArgb(255, 107, 107) : Color.DarkRed;
				else if (status == "OK")
					it.ForeColor = dark ? Color.FromArgb(106, 153, 85) : Color.DarkGreen;
				else
					it.ForeColor = text;
			}
		}

		// File picker for the target EXE/DLL.
		private void BtnBrowseClick(object sender, EventArgs e)
		{
			OpenFileDialog dlg = new OpenFileDialog();
			dlg.Filter = "Executables and libraries (*.exe;*.dll)|*.exe;*.dll|All files (*.*)|*.*";
			dlg.Title = "Select file to scan";
			if (dlg.ShowDialog(this) == DialogResult.OK)
			{
				txtTarget.Text = dlg.FileName;
			}
		}

		// Runs the static scanner and pushes the results into the list.
		private void BtnScanClick(object sender, EventArgs e)
		{
			string path = txtTarget.Text.Trim();
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				MessageBox.Show(this, "Please select a valid file first.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			lvResults.Items.Clear();
			lvResults.Groups.Clear();
			lblStatus.Text = "Scanning...";
			btnScan.Enabled = false;
			Application.DoEvents();

			try
			{
				DllScanResult result = DllScanner.Scan(path);
				lastResult = result;
				DisplayResult(result);
				btnSave.Enabled = true;
			}
			catch (Exception ex)
			{
				lblStatus.Text = "Scan failed: " + ex.Message;
			}
			finally
			{
				btnScan.Enabled = true;
			}
		}

		// Groups results into "Native DLL imports" and ".NET assembly
		// references", then updates the status summary.
		private void DisplayResult(DllScanResult result)
		{
			int found = 0;
			int missing = 0;

			if (result.DirectImports.Count > 0)
			{
				ListViewGroup grp = new ListViewGroup("Native DLL imports (from PE header)");
				lvResults.Groups.Add(grp);

				foreach (ScannedDll sd in result.DirectImports)
				{
					ListViewItem it = new ListViewItem(sd.Found ? "OK" : "MISSING");
					it.SubItems.Add(sd.Name);
					it.SubItems.Add(sd.Found ? (sd.Source + " - " + sd.FoundPath) : "(not found in any standard search path)");
					if (sd.Found) found++; else missing++;
					it.Group = grp;
					lvResults.Items.Add(it);
				}
			}

			if (result.IsDotNet && result.DotNetReferences.Count > 0)
			{
				ListViewGroup grp = new ListViewGroup(".NET assembly references (from CLR metadata)");
				lvResults.Groups.Add(grp);

				foreach (ScannedDll sd in result.DotNetReferences)
				{
					ListViewItem it = new ListViewItem(sd.Found ? "OK" : "MISSING");
					it.SubItems.Add(sd.Name);
					it.SubItems.Add(sd.Found ? (sd.Source + " - " + sd.FoundPath) : "(not found in GAC, game folder, or .NET Framework folders)");
					if (sd.Found) found++; else missing++;
					it.Group = grp;
					lvResults.Items.Add(it);
				}
			}

			// Nothing detected: show a single explanatory row.
			if (result.DirectImports.Count == 0 && result.DotNetReferences.Count == 0)
			{
				ListViewGroup grp = new ListViewGroup("Scan result");
				lvResults.Groups.Add(grp);
				ListViewItem it = new ListViewItem("NOTE");
				it.SubItems.Add("No dependencies detected.");
				it.SubItems.Add(result.Note ?? "The file may not be a valid PE / .NET assembly.");
				it.Group = grp;
				lvResults.Items.Add(it);
			}

			lblStatus.Text = string.Format(
				"Architecture: {0}   |   .NET: {1}   |   Found: {2}   |   Missing: {3}",
				result.Architecture,
				result.IsDotNet ? "Yes" : "No",
				found, missing);

			ApplyTheme();
		}

		// Exports the cached scan as a UTF-8 text report.
		private void BtnSaveClick(object sender, EventArgs e)
		{
			if (lastResult == null) return;

			SaveFileDialog dlg = new SaveFileDialog();
			dlg.Filter = "Text report (*.txt)|*.txt|All files (*.*)|*.*";
			dlg.FileName = Path.GetFileNameWithoutExtension(lastResult.TargetPath) + "_dllscan.txt";
			if (dlg.ShowDialog(this) != DialogResult.OK) return;

			try
			{
				StringBuilder sb = new StringBuilder();
				sb.AppendLine("==================================================");
				sb.AppendLine(" DLL Dependency Scan Report");
				sb.AppendLine("==================================================");
				sb.AppendLine("Target       : " + lastResult.TargetPath);
				sb.AppendLine("Architecture : " + lastResult.Architecture);
				sb.AppendLine("Is .NET      : " + (lastResult.IsDotNet ? "Yes" : "No"));
				sb.AppendLine("Scanned at   : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
				sb.AppendLine();

				if (lastResult.DirectImports.Count > 0)
				{
					sb.AppendLine("--------------------------------------------------");
					sb.AppendLine(" Native DLL imports (from PE header)");
					sb.AppendLine("--------------------------------------------------");
					foreach (ScannedDll sd in lastResult.DirectImports)
					{
						sb.AppendLine("  [" + (sd.Found ? "OK     " : "MISSING") + "] " + sd.Name);
						if (sd.Found)
							sb.AppendLine("             -> " + sd.Source + " - " + sd.FoundPath);
					}
					sb.AppendLine();
				}

				if (lastResult.DotNetReferences.Count > 0)
				{
					sb.AppendLine("--------------------------------------------------");
					sb.AppendLine(" .NET assembly references (from CLR metadata)");
					sb.AppendLine("--------------------------------------------------");
					foreach (ScannedDll sd in lastResult.DotNetReferences)
					{
						sb.AppendLine("  [" + (sd.Found ? "OK     " : "MISSING") + "] " + sd.Name);
						if (sd.Found)
							sb.AppendLine("             -> " + sd.Source + " - " + sd.FoundPath);
					}
					sb.AppendLine();
				}

				sb.AppendLine("==================================================");
				sb.AppendLine(" End of scan");
				sb.AppendLine("==================================================");

				File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
				MessageBox.Show(this, "Report saved successfully.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "Failed to save report: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}
	}
}