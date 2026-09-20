/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen
 * 
 * 
 * Designer stub for the Can I Run It form.
 * The actual UI is built programmatically in Can_I_Run_It.cs.
 */
namespace TestApp
{
	partial class Can_I_Run_It
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

		// Minimal InitializeComponent: the real layout is created in
		// BuildUI() inside Can_I_Run_It.cs. Only form-level properties
		// are set here so the designer file stays in sync with the form.
		private void InitializeComponent()
		{
			this.SuspendLayout();
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Name = "Can_I_Run_It";
			this.Text = "Can I Run It? - Program Readiness & Protection Analyzer";
			this.ResumeLayout(false);
		}
	}
}