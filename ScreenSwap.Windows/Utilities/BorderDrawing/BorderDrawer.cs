using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows.Forms;

namespace ScreenSwap.Windows.Utilities.BorderDrawing;

[SupportedOSPlatform("windows6.1")]
public static class BorderDrawer
{
    private static readonly List<OverlayForm> overlayForms = [];
    private static SynchronizationContext uiContext;

    public static void Initialize(SynchronizationContext context) => uiContext = context;

    public static void SelectScreen(IReadOnlyList<Rectangle> bounds, Color highlightColor, string textOverlay = null)
    {
        Post(() =>
        {
            // Reuse existing forms where possible, create or destroy to match count.
            while (overlayForms.Count < bounds.Count)
                overlayForms.Add(new OverlayForm());

            while (overlayForms.Count > bounds.Count)
            {
                overlayForms[^1].Hide();
                overlayForms.RemoveAt(overlayForms.Count - 1);
            }

            for (var i = 0; i < bounds.Count; i++)
                overlayForms[i].Show(bounds[i], highlightColor, i == 0 ? textOverlay : null);
        });
    }

    public static void ClearScreen()
    {
        Post(() =>
        {
            foreach (var form in overlayForms)
                form.Hide();
        });
    }

    private static void Post(Action action)
    {
        if (uiContext is null)
        {
            action();
            return;
        }

        uiContext.Post(_ => action(), null);
    }

    [SupportedOSPlatform("windows6.1")]
    private sealed class OverlayForm : Form
    {
        private Color color;
        private string text;
        private int penSize;

        public OverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            TransparencyKey = Color.Black;
            TopMost = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        public void Show(Rectangle screenBounds, Color highlightColor, string textOverlay)
        {
            color = highlightColor;
            text = textOverlay;
            penSize = (int)Math.Clamp(Math.Min(screenBounds.Width, screenBounds.Height) * 0.02, 4, 8);

            SetBounds(screenBounds.X, screenBounds.Y, screenBounds.Width, screenBounds.Height);

            if (!Visible)
                Show();

            Invalidate();
            BringToFront();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using var pen = new Pen(color, penSize);

            // Inset by two-thirds penSize — stroke center is at the boundary, outer edge exactly at the overlay edge.
            var inset = (int)(penSize * 0.8);
            e.Graphics.DrawRectangle(pen, inset, inset, Width - (2 * inset), Height - (2 * inset));

            if (!string.IsNullOrEmpty(text))
            {
                using var font = new Font("Segoe UI", 16, FontStyle.Regular, GraphicsUnit.Point);
                using var brush = new SolidBrush(Color.Black);
                e.Graphics.DrawString(text, font, brush, new PointF(10, 10));
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }
    }
}
