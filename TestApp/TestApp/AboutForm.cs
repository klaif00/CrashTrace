/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen
 * 
 * About window for the tool.
 *
 * A borderless, rounded window with:
 *   - an animated aurora background (hue-shifting blobs)
 *   - a frosted-glass overlay on top so the colours stay subtle
 *   - a moving neon separator
 *   - a custom close button in the top-right corner and a larger one
 *     at the bottom
 *   - drag-to-move anywhere, ESC closes, follows the main theme live.
 */
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace TestApp
{
	internal class AboutForm : Form
	{
		// Animation loop state and moving neon separator position/direction.
		private Timer animationTimer;
		private double animationTime;
		private int separatorPosition;
		private int separatorDirection = 1;

		// Clickable regions, recalculated on resize and used for hit-testing.
		private Rectangle closeButtonRect;
		private Rectangle linkRect;
		private Rectangle footerCloseRect;

		// Hover flags for the GitHub link and the footer Close button.
		private bool linkHover;
		private bool footerCloseHover;

		// Window drag state (true while the user is dragging the form).
		private bool dragging;
		private Point dragOffset;

		// Colors (refreshed when theme changes)
		private Color formBack;
		private Color textColor;
		private Color subtleTextColor;
		private Color accentColor;
		private Color glassColor;
		private Color glassBorder;
		private Color neonColor;

		// Builds the borderless, rounded, double-buffered window and starts the animation timer.
		public AboutForm()
		{
			SetStyle(
				ControlStyles.AllPaintingInWmPaint |
				ControlStyles.UserPaint |
				ControlStyles.OptimizedDoubleBuffer |
				ControlStyles.ResizeRedraw, true);

			FormBorderStyle = FormBorderStyle.None;
			StartPosition = FormStartPosition.CenterParent;
			ClientSize = new Size(480, 360);
			ShowInTaskbar = false;
			KeyPreview = true;
			DoubleBuffered = true;
			BackColor = Color.Black;

			UpdateLayout();
			ApplyColors();

			// Timer drives the aurora hue shift and the bouncing neon separator.
			animationTimer = new Timer();
			animationTimer.Interval = 40;
			animationTimer.Tick += delegate
			{
				animationTime += 0.03;

				int maxPos = ClientSize.Width - 120;
				separatorPosition += separatorDirection * 4;
				if (separatorPosition >= maxPos)
				{
					separatorPosition = maxPos;
					separatorDirection = -1;
				}
				else if (separatorPosition <= 0)
				{
					separatorPosition = 0;
					separatorDirection = 1;
				}

				Invalidate();
			};
			animationTimer.Start();
		}

		// Recomputes the clickable rectangles and the rounded region of the form.
		private void UpdateLayout()
		{
			closeButtonRect = new Rectangle(ClientSize.Width - 42, 12, 28, 28);

			try
			{
				using (GraphicsPath path = GetRoundedPath(
					new Rectangle(0, 0, ClientSize.Width, ClientSize.Height), 16))
				{
					Region old = this.Region;
					this.Region = new Region(path);
					if (old != null) old.Dispose();
				}
			}
			catch { }
		}

		// Called on resize; re-applies the rounded region and layout rectangles.
		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);
			UpdateLayout();
		}

		// Picks the palette based on the main form's current theme (dark or light).
		private void ApplyColors()
		{
			bool dark = false;
			try { dark = MainForm.IsDarkMode; } catch { }

			if (dark)
			{
				formBack = Color.FromArgb(14, 14, 18);
				textColor = Color.FromArgb(235, 235, 240);
				subtleTextColor = Color.FromArgb(165, 165, 175);
				accentColor = Color.FromArgb(120, 200, 255);
				glassColor = Color.FromArgb(180, 20, 20, 28);
				glassBorder = Color.FromArgb(70, 100, 100, 130);
				neonColor = Color.FromArgb(78, 220, 200);
			}
			else
			{
				formBack = Color.FromArgb(240, 240, 245);
				textColor = Color.FromArgb(30, 30, 35);
				subtleTextColor = Color.FromArgb(95, 95, 105);
				accentColor = Color.FromArgb(0, 100, 180);
				glassColor = Color.FromArgb(175, 255, 255, 255);
				glassBorder = Color.FromArgb(60, 160, 160, 180);
				neonColor = Color.FromArgb(0, 130, 220);
			}
		}

		// Public entry point the main form calls when the theme changes.
		// Marshals to the UI thread if needed.
		public void RefreshTheme()
		{
			if (this.IsDisposed) return;
			try
			{
				if (this.InvokeRequired)
					this.BeginInvoke(new MethodInvoker(RefreshThemeInternal));
				else
					RefreshThemeInternal();
			}
			catch { }
		}

		// Re-reads the palette and forces a repaint.
		private void RefreshThemeInternal()
		{
			ApplyColors();
			Invalidate();
		}

		// Suppressed on purpose: all drawing happens in OnPaint to avoid flicker.
		protected override void OnPaintBackground(PaintEventArgs e)
		{
			// Everything is painted in OnPaint to avoid flicker.
		}

		// Main render pass: aurora, glass, neon separator, then content on top.
		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			g.InterpolationMode = InterpolationMode.HighQualityBicubic;

			DrawAurora(g);
			DrawGlass(g);
			DrawNeonSeparator(g);
			
			DrawContent(g);
		}

		// Fills the background and paints three soft, slowly moving color blobs.
		private void DrawAurora(Graphics g)
		{
			Rectangle rect = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);

			using (SolidBrush baseBrush = new SolidBrush(formBack))
				g.FillRectangle(baseBrush, rect);

			// The base hue slowly shifts so the whole palette drifts over time.
			double baseHue = (animationTime * 0.08) % 1.0;

			Color[] colors = new Color[]
			{
				HsvToRgb(baseHue, 0.75, 0.65),
				HsvToRgb((baseHue + 0.33) % 1.0, 0.75, 0.65),
				HsvToRgb((baseHue + 0.66) % 1.0, 0.75, 0.65)
			};

			// Each blob orbits around the center at a different phase.
			for (int i = 0; i < 3; i++)
			{
				double phase = animationTime * 0.5 + (i * Math.PI * 2.0 / 3.0);
				double cx = rect.Width * 0.5 + Math.Cos(phase) * rect.Width * 0.35;
				double cy = rect.Height * 0.5 + Math.Sin(phase) * rect.Height * 0.35;

				int radius = (int)(Math.Max(rect.Width, rect.Height) * 0.7);

				Rectangle blobRect = new Rectangle(
					(int)(cx - radius), (int)(cy - radius),
					radius * 2, radius * 2);

				try
				{
					using (GraphicsPath path = new GraphicsPath())
					{
						path.AddEllipse(blobRect);
						using (PathGradientBrush brush = new PathGradientBrush(path))
						{
							// Solid center fading to fully transparent edges.
							brush.CenterColor = Color.FromArgb(160, colors[i]);
							brush.SurroundColors = new Color[] { Color.FromArgb(0, colors[i]) };
							brush.FocusScales = new PointF(0.1f, 0.1f);
							g.FillPath(brush, path);
						}
					}
				}
				catch { }
			}
		}

		// Draws the frosted-glass panel that sits over the aurora.
		private void DrawGlass(Graphics g)
		{
			Rectangle glass = new Rectangle(8, 8, ClientSize.Width - 16, ClientSize.Height - 16);

			try
			{
				using (GraphicsPath path = GetRoundedPath(glass, 14))
				{
					using (SolidBrush brush = new SolidBrush(glassColor))
						g.FillPath(brush, path);

					using (Pen pen = new Pen(glassBorder, 1f))
						g.DrawPath(pen, path);

					// Subtle top inner highlight
					
						
				}
			}
			catch { }
		}

		// Draws the horizontal separator with a moving neon glow on top.
		private void DrawNeonSeparator(Graphics g)
		{
			int y = 165;
			int startX = 60;
			int width = ClientSize.Width - 120;
			if (width <= 0) return;

			// Base track line.
			using (SolidBrush baseBrush = new SolidBrush(Color.FromArgb(80, glassBorder)))
				g.FillRectangle(baseBrush, startX, y - 1, width, 2);

			// Glow rectangle that slides left and right along the track.
			int glowCenter = startX + separatorPosition;
			int glowHalfWidth = 60;

			Rectangle glowRect = new Rectangle(
				glowCenter - glowHalfWidth, y - 3,
				glowHalfWidth * 2, 6);

			try
			{
				using (LinearGradientBrush brush = new LinearGradientBrush(
					glowRect, Color.Transparent, Color.Transparent,
					LinearGradientMode.Horizontal))
				{
					// Transparent -> neon -> transparent blend for the moving glow.
					ColorBlend blend = new ColorBlend(5);
					blend.Colors = new Color[]
					{
						Color.FromArgb(0, neonColor),
						Color.FromArgb(100, neonColor),
						Color.FromArgb(255, neonColor),
						Color.FromArgb(100, neonColor),
						Color.FromArgb(0, neonColor)
					};
					blend.Positions = new float[] { 0f, 0.3f, 0.5f, 0.7f, 1f };
					brush.InterpolationColors = blend;
					g.FillRectangle(brush, glowRect);
				}
			}
			catch { }
		}

		// Paints the title, version, author, the GitHub link and the Close button.
		private void DrawContent(Graphics g)
		{
			int centerX = ClientSize.Width / 2;

			using (Font f = new Font("Segoe UI", 19f, FontStyle.Bold))
			using (SolidBrush b = new SolidBrush(accentColor))
			{
				string t = "Crash Diagnostic Tool";
				SizeF sz = g.MeasureString(t, f);
				g.DrawString(t, f, b, centerX - sz.Width / 2, 28);
			}

			using (Font f = new Font("Segoe UI", 10f, FontStyle.Regular))
			using (SolidBrush b = new SolidBrush(subtleTextColor))
			{
				string t = "The first version";
				SizeF sz = g.MeasureString(t, f);
				g.DrawString(t, f, b, centerX - sz.Width / 2, 72);
			}

			using (Font f = new Font("Segoe UI", 9f, FontStyle.Regular))
			using (SolidBrush b = new SolidBrush(subtleTextColor))
			{
				string t = "Version 1.0.0";
				SizeF sz = g.MeasureString(t, f);
				g.DrawString(t, f, b, centerX - sz.Width / 2, 102);
			}

			using (Font f = new Font("Segoe UI", 10.5f, FontStyle.Regular))
			using (SolidBrush b = new SolidBrush(textColor))
			{
				string t = "Developed by Limen";
				SizeF sz = g.MeasureString(t, f);
				g.DrawString(t, f, b, centerX - sz.Width / 2, 190);
			}

			// GitHub link text; drawn in neon and underlined on hover.
			// Its rectangle is cached for hit-testing.
			using (Font f = new Font("Segoe UI", 9.5f, FontStyle.Regular))
			{
				string t = "github.com/klaif00/CrashTrace";
				SizeF sz = g.MeasureString(t, f);
				int lx = (int)(centerX - sz.Width / 2);
				int ly = 218;
				linkRect = new Rectangle(lx, ly, (int)sz.Width, (int)sz.Height);

				Color c = linkHover
					? Color.FromArgb(255, neonColor)
					: neonColor;

				using (SolidBrush b = new SolidBrush(c))
					g.DrawString(t, f, b, lx, ly);

				if (linkHover)
				{
					using (Pen p = new Pen(c, 1f))
						g.DrawLine(p, lx, ly + sz.Height - 2, lx + sz.Width, ly + sz.Height - 2);
				}
			}

			// Footer Close button, brightens slightly on hover.
			footerCloseRect = new Rectangle(centerX - 60, 262, 120, 36);

			Color btnBg = footerCloseHover
				? Color.FromArgb(130, 255, 255, 255)
				: Color.FromArgb(60, 255, 255, 255);

			try
			{
				using (GraphicsPath path = GetRoundedPath(footerCloseRect, 8))
				{
					using (SolidBrush bg = new SolidBrush(btnBg))
						g.FillPath(bg, path);
					using (Pen border = new Pen(glassBorder, 1f))
						g.DrawPath(border, path);
				}
			}
			catch { }

			using (Font f = new Font("Segoe UI", 10f, FontStyle.Regular))
			using (SolidBrush b = new SolidBrush(textColor))
			{
				string t = "Close";
				SizeF sz = g.MeasureString(t, f);
				g.DrawString(t, f, b,
					footerCloseRect.X + (footerCloseRect.Width - sz.Width) / 2,
					footerCloseRect.Y + (footerCloseRect.Height - sz.Height) / 2);
			}
		}

		// Builds a rounded rectangle path (used for the window, the glass panel and the Close button).
		private static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
		{
			GraphicsPath path = new GraphicsPath();
			int d = radius * 2;
			path.AddArc(rect.X, rect.Y, d, d, 180, 90);
			path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
			path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
			path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
			path.CloseFigure();
			return path;
		}

		// Converts an HSV color to an RGB Color. H is normalized to 0..1.
		private static Color HsvToRgb(double h, double s, double v)
		{
			h = h - Math.Floor(h);
			int i = (int)(h * 6);
			double f = h * 6 - i;
			double p = v * (1 - s);
			double q = v * (1 - f * s);
			double t = v * (1 - (1 - f) * s);
			double r, g, b;

			switch (i % 6)
			{
				case 0: r = v; g = t; b = p; break;
				case 1: r = q; g = v; b = p; break;
				case 2: r = p; g = v; b = t; break;
				case 3: r = p; g = q; b = v; break;
				case 4: r = t; g = p; b = v; break;
				default: r = v; g = p; b = q; break;
			}

			return Color.FromArgb(
				Math.Min(255, (int)(r * 255)),
				Math.Min(255, (int)(g * 255)),
				Math.Min(255, (int)(b * 255)));
		}

		// Starts a window drag, unless the click landed on an interactive element.
		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);

			if (e.Button != MouseButtons.Left) return;

			// Don't start a drag when clicking interactive elements.
			
			if (footerCloseRect.Contains(e.Location)) return;
			if (linkRect.Contains(e.Location)) return;

			dragging = true;
			Point screen = PointToScreen(e.Location);
			dragOffset = new Point(this.Location.X - screen.X, this.Location.Y - screen.Y);
		}

		// Moves the window while dragging and updates hover states / cursor.
		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);

			if (dragging)
			{
				Point screen = PointToScreen(e.Location);
				this.Location = new Point(screen.X + dragOffset.X, screen.Y + dragOffset.Y);
			}

			bool needRefresh = false;

			
			

			bool newLink = linkRect.Contains(e.Location);
			if (newLink != linkHover) { linkHover = newLink; needRefresh = true; }

			bool newFooter = footerCloseRect.Contains(e.Location);
			if (newFooter != footerCloseHover) { footerCloseHover = newFooter; needRefresh = true; }

			
				
			else if (dragging)
				Cursor = Cursors.SizeAll;
			else
				Cursor = Cursors.Default;

			if (needRefresh) Invalidate();
		}

		// Ends drag, and handles clicks on the footer Close button and the GitHub link.
		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);
			dragging = false;

			if (e.Button != MouseButtons.Left) return;

			if (footerCloseRect.Contains(e.Location))
			{
				Close();
				return;
			}

			// Opens the project repository in the default browser.
			if (linkRect.Contains(e.Location))
			{
				try { Process.Start("https://github.com/klaif00/CrashTrace"); } catch { }
			}
		}

		// Clears hover flags when the mouse leaves the window.
		protected override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);

			bool needRefresh = false;
			
			if (linkHover) { linkHover = false; needRefresh = true; }
			if (footerCloseHover) { footerCloseHover = false; needRefresh = true; }

			if (needRefresh) Invalidate();
		}

		// ESC closes the window.
		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			if (keyData == Keys.Escape) { Close(); return true; }
			return base.ProcessCmdKey(ref msg, keyData);
		}

		// Stops and disposes the animation timer before the form is released.
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				try { if (animationTimer != null) { animationTimer.Stop(); animationTimer.Dispose(); } }
				catch { }
			}
			base.Dispose(disposing);
		}
	}
}