/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Created by SharpDevelop.
 * Developed by Limen
 *
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
namespace TestApp
{
	partial class MainForm
	{
		private System.ComponentModel.IContainer components = null;

		// Standard designer dispose: releases the components container,
		// then lets the base Form clean up.
		protected override void Dispose(bool disposing)
		{
			if (disposing) {
				if (components != null) {
					components.Dispose();
				}
			}
			base.Dispose(disposing);
		}

		// Designer-generated layout for MainForm. Control fields are
		// declared at the bottom. The form is a fixed-size tool window
		// with drag-and-drop enabled and a menu strip as the main menu.
		private void InitializeComponent()
		{
			this.menuStripMain = new System.Windows.Forms.MenuStrip();
			this.fileMenu = new System.Windows.Forms.ToolStripMenuItem();
			this.fileOpenItem = new System.Windows.Forms.ToolStripMenuItem();
			this.fileSep1 = new System.Windows.Forms.ToolStripSeparator();
			this.fileExitItem = new System.Windows.Forms.ToolStripMenuItem();
			this.toolsMenu = new System.Windows.Forms.ToolStripMenuItem();
			this.toolsDllScanItem = new System.Windows.Forms.ToolStripMenuItem();
			this.toolsCanIRunItItem = new System.Windows.Forms.ToolStripMenuItem();
			this.toolsSep1 = new System.Windows.Forms.ToolStripSeparator();
			this.toolsCopyItem = new System.Windows.Forms.ToolStripMenuItem();
			this.toolsOpenFolderItem = new System.Windows.Forms.ToolStripMenuItem();
			this.helpMenu = new System.Windows.Forms.ToolStripMenuItem();
			this.helpAboutItem = new System.Windows.Forms.ToolStripMenuItem();
			this.button1 = new System.Windows.Forms.Button();
			this.button2 = new System.Windows.Forms.Button();
			this.button3 = new System.Windows.Forms.Button();
			this.buttonDarkMode = new System.Windows.Forms.Button();
			this.buttonCopy = new System.Windows.Forms.Button();
			this.buttonOpenFolder = new System.Windows.Forms.Button();
			this.buttonDllScan = new System.Windows.Forms.Button();
			this.buttonCanIRunIt = new System.Windows.Forms.Button();
			this.listView1 = new System.Windows.Forms.ListView();
			this.pictureBoxIcon = new System.Windows.Forms.PictureBox();
			this.labelFileInfo = new System.Windows.Forms.Label();
			this.textBoxSearch = new System.Windows.Forms.TextBox();
			this.labelStatus = new System.Windows.Forms.Label();
			this.progressBar1 = new System.Windows.Forms.ProgressBar();
			this.menuStripMain.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.pictureBoxIcon)).BeginInit();
			this.SuspendLayout();
			// 
			// menuStripMain
			// 
			this.menuStripMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
			this.fileMenu,
			this.toolsMenu,
			this.helpMenu});
			this.menuStripMain.Location = new System.Drawing.Point(0, 0);
			this.menuStripMain.Name = "menuStripMain";
			this.menuStripMain.Size = new System.Drawing.Size(700, 24);
			this.menuStripMain.TabIndex = 20;
			this.menuStripMain.Text = "menuStripMain";
			// 
			// fileMenu
			// 
			this.fileMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
			this.fileOpenItem,
			this.fileSep1,
			this.fileExitItem});
			this.fileMenu.Name = "fileMenu";
			this.fileMenu.Size = new System.Drawing.Size(37, 20);
			this.fileMenu.Text = "&File";
			// 
			// fileOpenItem
			// 
			this.fileOpenItem.Name = "fileOpenItem";
			this.fileOpenItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
			this.fileOpenItem.Size = new System.Drawing.Size(180, 22);
			this.fileOpenItem.Text = "&Open File...";
			this.fileOpenItem.Click += new System.EventHandler(this.MenuOpenClick);
			// 
			// fileSep1
			// 
			this.fileSep1.Name = "fileSep1";
			this.fileSep1.Size = new System.Drawing.Size(177, 6);
			// 
			// fileExitItem
			// 
			this.fileExitItem.Name = "fileExitItem";
			this.fileExitItem.Size = new System.Drawing.Size(180, 22);
			this.fileExitItem.Text = "E&xit";
			this.fileExitItem.Click += new System.EventHandler(this.MenuExitClick);
			// 
			// toolsMenu
			// 
			this.toolsMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
			this.toolsDllScanItem,
			this.toolsCanIRunItItem,
			this.toolsSep1,
			this.toolsCopyItem,
			this.toolsOpenFolderItem});
			this.toolsMenu.Name = "toolsMenu";
			this.toolsMenu.Size = new System.Drawing.Size(47, 20);
			this.toolsMenu.Text = "&Tools";
			// 
			// toolsDllScanItem
			// 
			this.toolsDllScanItem.Name = "toolsDllScanItem";
			this.toolsDllScanItem.Size = new System.Drawing.Size(200, 22);
			this.toolsDllScanItem.Text = "&DLL Scan...";
			this.toolsDllScanItem.Click += new System.EventHandler(this.MenuDllScanClick);
			// 
			// toolsCanIRunItItem
			// 
			this.toolsCanIRunItItem.Name = "toolsCanIRunItItem";
			this.toolsCanIRunItItem.Size = new System.Drawing.Size(200, 22);
			this.toolsCanIRunItItem.Text = "&Can I Run It?...";
			this.toolsCanIRunItItem.Click += new System.EventHandler(this.MenuCanIRunItClick);
			// 
			// toolsSep1
			// 
			this.toolsSep1.Name = "toolsSep1";
			this.toolsSep1.Size = new System.Drawing.Size(197, 6);
			// 
			// toolsCopyItem
			// 
			this.toolsCopyItem.Name = "toolsCopyItem";
			this.toolsCopyItem.Size = new System.Drawing.Size(200, 22);
			this.toolsCopyItem.Text = "Copy &Analysis";
			this.toolsCopyItem.Click += new System.EventHandler(this.MenuCopyClick);
			// 
			// toolsOpenFolderItem
			// 
			this.toolsOpenFolderItem.Name = "toolsOpenFolderItem";
			this.toolsOpenFolderItem.Size = new System.Drawing.Size(200, 22);
			this.toolsOpenFolderItem.Text = "&Open Report Folder";
			this.toolsOpenFolderItem.Click += new System.EventHandler(this.MenuOpenFolderClick);
			// 
			// helpMenu
			// 
			this.helpMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
			this.helpAboutItem});
			this.helpMenu.Name = "helpMenu";
			this.helpMenu.Size = new System.Drawing.Size(44, 20);
			this.helpMenu.Text = "&Help";
			// 
			// helpAboutItem
			// 
			this.helpAboutItem.Name = "helpAboutItem";
			this.helpAboutItem.Size = new System.Drawing.Size(180, 22);
			this.helpAboutItem.Text = "&About";
			this.helpAboutItem.Click += new System.EventHandler(this.MenuAboutClick);
			// 
			// pictureBoxIcon
			// 
			this.pictureBoxIcon.Location = new System.Drawing.Point(12, 36);
			this.pictureBoxIcon.Name = "pictureBoxIcon";
			this.pictureBoxIcon.Size = new System.Drawing.Size(32, 32);
			this.pictureBoxIcon.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
			this.pictureBoxIcon.TabIndex = 10;
			this.pictureBoxIcon.TabStop = false;
			// 
			// labelFileInfo
			// 
			this.labelFileInfo.Location = new System.Drawing.Point(52, 36);
			this.labelFileInfo.Name = "labelFileInfo";
			this.labelFileInfo.Size = new System.Drawing.Size(500, 32);
			this.labelFileInfo.TabIndex = 11;
			this.labelFileInfo.Text = "No file selected.";
			// 
			// textBoxSearch
			// 
			this.textBoxSearch.Location = new System.Drawing.Point(560, 40);
			this.textBoxSearch.Name = "textBoxSearch";
			this.textBoxSearch.Size = new System.Drawing.Size(128, 20);
			this.textBoxSearch.TabIndex = 12;
			this.textBoxSearch.TextChanged += new System.EventHandler(this.TextBoxSearchTextChanged);
			// 
			// listView1
			// 
			this.listView1.Location = new System.Drawing.Point(12, 74);
			this.listView1.Name = "listView1";
			this.listView1.Size = new System.Drawing.Size(676, 380);
			this.listView1.TabIndex = 2;
			this.listView1.UseCompatibleStateImageBehavior = false;
			this.listView1.SelectedIndexChanged += new System.EventHandler(this.ListView1SelectedIndexChanged);
			// 
			// button1 - Select
			// 
			this.button1.Location = new System.Drawing.Point(12, 464);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(160, 25);
			this.button1.TabIndex = 0;
			this.button1.Text = "Select";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(this.Button1Click);
			// 
			// button2 - Start
			// 
			this.button2.Location = new System.Drawing.Point(180, 464);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(160, 25);
			this.button2.TabIndex = 1;
			this.button2.Text = "Start";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(this.Button2Click);
			// 
			// button3 - Stop
			// 
			this.button3.Location = new System.Drawing.Point(348, 464);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(160, 25);
			this.button3.TabIndex = 3;
			this.button3.Text = "Stop";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(this.Button3Click);
			// 
			// buttonDarkMode
			// 
			this.buttonDarkMode.Location = new System.Drawing.Point(516, 464);
			this.buttonDarkMode.Name = "buttonDarkMode";
			this.buttonDarkMode.Size = new System.Drawing.Size(172, 25);
			this.buttonDarkMode.TabIndex = 13;
			this.buttonDarkMode.Text = "Dark Mode";
			this.buttonDarkMode.UseVisualStyleBackColor = true;
			this.buttonDarkMode.Click += new System.EventHandler(this.ButtonDarkModeClick);
			// 
			// buttonCopy
			// 
			this.buttonCopy.Location = new System.Drawing.Point(12, 494);
			this.buttonCopy.Name = "buttonCopy";
			this.buttonCopy.Size = new System.Drawing.Size(160, 25);
			this.buttonCopy.TabIndex = 14;
			this.buttonCopy.Text = "Copy Analysis";
			this.buttonCopy.UseVisualStyleBackColor = true;
			this.buttonCopy.Click += new System.EventHandler(this.ButtonCopyClick);
			// 
			// buttonOpenFolder
			// 
			this.buttonOpenFolder.Location = new System.Drawing.Point(180, 494);
			this.buttonOpenFolder.Name = "buttonOpenFolder";
			this.buttonOpenFolder.Size = new System.Drawing.Size(160, 25);
			this.buttonOpenFolder.TabIndex = 15;
			this.buttonOpenFolder.Text = "Open Folder";
			this.buttonOpenFolder.UseVisualStyleBackColor = true;
			this.buttonOpenFolder.Click += new System.EventHandler(this.ButtonOpenFolderClick);
			// 
			// buttonDllScan
			// 
			this.buttonDllScan.Location = new System.Drawing.Point(348, 494);
			this.buttonDllScan.Name = "buttonDllScan";
			this.buttonDllScan.Size = new System.Drawing.Size(160, 25);
			this.buttonDllScan.TabIndex = 18;
			this.buttonDllScan.Text = "DLL Scan";
			this.buttonDllScan.UseVisualStyleBackColor = true;
			this.buttonDllScan.Click += new System.EventHandler(this.ButtonDllScanClick);
			// 
			// buttonCanIRunIt
			// 
			this.buttonCanIRunIt.Location = new System.Drawing.Point(516, 494);
			this.buttonCanIRunIt.Name = "buttonCanIRunIt";
			this.buttonCanIRunIt.Size = new System.Drawing.Size(172, 25);
			this.buttonCanIRunIt.TabIndex = 19;
			this.buttonCanIRunIt.Text = "Can I Run It?";
			this.buttonCanIRunIt.UseVisualStyleBackColor = true;
			this.buttonCanIRunIt.Click += new System.EventHandler(this.ButtonCanIRunItClick);
			// 
			// labelStatus
			// 
			this.labelStatus.Location = new System.Drawing.Point(12, 526);
			this.labelStatus.Name = "labelStatus";
			this.labelStatus.Size = new System.Drawing.Size(340, 20);
			this.labelStatus.TabIndex = 16;
			this.labelStatus.Text = "Status: Idle";
			// 
			// progressBar1
			// 
			this.progressBar1.Location = new System.Drawing.Point(358, 526);
			this.progressBar1.Name = "progressBar1";
			this.progressBar1.Size = new System.Drawing.Size(330, 18);
			this.progressBar1.TabIndex = 17;
			this.progressBar1.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
			this.progressBar1.MarqueeAnimationSpeed = 0;
			// 
			// MainForm
			// 
			this.AllowDrop = true;
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(700, 557);
			this.Controls.Add(this.progressBar1);
			this.Controls.Add(this.labelStatus);
			this.Controls.Add(this.buttonCanIRunIt);
			this.Controls.Add(this.buttonDllScan);
			this.Controls.Add(this.buttonOpenFolder);
			this.Controls.Add(this.buttonCopy);
			this.Controls.Add(this.buttonDarkMode);
			this.Controls.Add(this.textBoxSearch);
			this.Controls.Add(this.labelFileInfo);
			this.Controls.Add(this.pictureBoxIcon);
			this.Controls.Add(this.button3);
			this.Controls.Add(this.listView1);
			this.Controls.Add(this.button2);
			this.Controls.Add(this.button1);
			this.Controls.Add(this.menuStripMain);
			this.MainMenuStrip = this.menuStripMain;
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "MainForm";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "TestApp - Crash Diagnostic Tool";
			this.DragEnter += new System.Windows.Forms.DragEventHandler(this.MainFormDragEnter);
			this.DragDrop += new System.Windows.Forms.DragEventHandler(this.MainFormDragDrop);
			this.menuStripMain.ResumeLayout(false);
			this.menuStripMain.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.pictureBoxIcon)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();
		}

		// Control fields. Names here must match the names used in
		// MainForm.cs. The numbered buttons (button1, button2, button3)
		// are the historical names for Select / Start / Stop.
		private System.Windows.Forms.MenuStrip menuStripMain;
		private System.Windows.Forms.ToolStripMenuItem fileMenu;
		private System.Windows.Forms.ToolStripMenuItem fileOpenItem;
		private System.Windows.Forms.ToolStripSeparator fileSep1;
		private System.Windows.Forms.ToolStripMenuItem fileExitItem;
		private System.Windows.Forms.ToolStripMenuItem toolsMenu;
		private System.Windows.Forms.ToolStripMenuItem toolsDllScanItem;
		private System.Windows.Forms.ToolStripMenuItem toolsCanIRunItItem;
		private System.Windows.Forms.ToolStripSeparator toolsSep1;
		private System.Windows.Forms.ToolStripMenuItem toolsCopyItem;
		private System.Windows.Forms.ToolStripMenuItem toolsOpenFolderItem;
		private System.Windows.Forms.ToolStripMenuItem helpMenu;
		private System.Windows.Forms.ToolStripMenuItem helpAboutItem;
		private System.Windows.Forms.ProgressBar progressBar1;
		private System.Windows.Forms.Label labelStatus;
		private System.Windows.Forms.Button buttonOpenFolder;
		private System.Windows.Forms.Button buttonCopy;
		private System.Windows.Forms.Button buttonDarkMode;
		private System.Windows.Forms.TextBox textBoxSearch;
		private System.Windows.Forms.Label labelFileInfo;
		private System.Windows.Forms.PictureBox pictureBoxIcon;
		private System.Windows.Forms.Button button3;
		private System.Windows.Forms.ListView listView1;
		private System.Windows.Forms.Button button2;
		private System.Windows.Forms.Button button1;
		private System.Windows.Forms.Button buttonDllScan;
		private System.Windows.Forms.Button buttonCanIRunIt;
	}
}