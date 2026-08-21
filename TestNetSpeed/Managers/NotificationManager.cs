using DpiCommon.Helper;
using FontAwesome.Sharp;
using Timer = System.Windows.Forms.Timer;

namespace TestNetSpeed.Managers
{
    /// <summary>
    /// Reusable in-app toast service — call ShowSuccess/ShowError/ShowWarning/
    /// ShowInfo from anywhere (no UI thread plumbing needed as long as the
    /// caller is already on it). Toasts stack bottom-right, fade in/out, and
    /// auto-dismiss; multiple toasts fired in quick succession stack instead
    /// of overlapping.
    /// </summary>
    public class NotificationManager
    {
        private static readonly Lazy<NotificationManager> _instance = new(() => new NotificationManager());
        public static NotificationManager Instance => _instance.Value;

        private const int DisplayMs = 3200;
        private const int FadeStepMs = 15;
        private const double FadeStep = 0.12;
        private const double MaxOpacity = 0.97;

        // Active toasts, oldest first — used to stack new ones above them.
        private readonly List<Form> _activeToasts = new();

        private NotificationManager() { }

        public void ShowSuccess(string message, string title = "Success") =>
            Show(title, message, Color.FromArgb(34, 197, 94), IconChar.CircleCheck);

        public void ShowError(string message, string title = "Error") =>
            Show(title, message, Color.FromArgb(239, 68, 68), IconChar.CircleXmark);

        public void ShowWarning(string message, string title = "Warning") =>
            Show(title, message, Color.FromArgb(245, 158, 11), IconChar.TriangleExclamation);

        public void ShowInfo(string message, string title = "Info") =>
            Show(title, message, Color.FromArgb(59, 130, 246), IconChar.CircleInfo);

        // ════════════════════════════════════════════════════════════════════
        //  BUILD + SHOW
        // ════════════════════════════════════════════════════════════════════

        private void Show(string title, string message, Color color, IconChar icon)
        {
            int w = DpiAwareService.Scale(340);
            int h = DpiAwareService.Scale(76);

            var toast = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Size = new Size(w, h),
                BackColor = color,
                ShowInTaskbar = false,
                TopMost = true,
                Opacity = 0
            };

            toast.Location = NextSlotLocation(toast.Size);

            var iconBox = new IconPictureBox
            {
                IconChar = icon,
                IconColor = Color.White,
                IconSize = DpiAwareService.Scale(28),
                Size = DpiAwareService.ScaledSize(40, 40),
                Location = DpiAwareService.ScaledLocation(15, 18),
                BackColor = Color.Transparent
            };
            toast.Controls.Add(iconBox);

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Location = DpiAwareService.ScaledLocation(65, 14),
                AutoSize = true
            };
            toast.Controls.Add(lblTitle);

            var lblMessage = new Label
            {
                Text = message,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Location = DpiAwareService.ScaledLocation(65, 36),
                Size = DpiAwareService.ScaledSize(w - 65 - 40, 34),
                AutoEllipsis = true
            };
            toast.Controls.Add(lblMessage);

            var btnClose = new IconButton
            {
                IconChar = IconChar.Xmark,
                IconColor = Color.White,
                IconSize = DpiAwareService.Scale(13),
                Size = DpiAwareService.ScaledSize(22, 22),
                Location = new Point(w - DpiAwareService.Scale(30), DpiAwareService.Scale(7)),
                BackColor = Color.FromArgb(70, 255, 255, 255),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(120, 255, 255, 255);
            toast.Controls.Add(btnClose);

            _activeToasts.Add(toast);
            toast.FormClosed += (s, e) => _activeToasts.Remove(toast);

            bool dismissing = false;
            var dismissTimer = new Timer { Interval = DisplayMs };
            void Dismiss()
            {
                if (dismissing) return;
                dismissing = true;
                dismissTimer.Stop();
                dismissTimer.Dispose();
                FadeOutAndClose(toast);
            }
            dismissTimer.Tick += (s, e) => Dismiss();

            btnClose.Click += (s, e) => Dismiss();
            toast.Click += (s, e) => Dismiss();
            lblMessage.Click += (s, e) => Dismiss();

            toast.Show();
            dismissTimer.Start();
            FadeIn(toast);
        }

        /// <summary>Bottom-right corner, stacked above whatever toasts are already showing.</summary>
        private Point NextSlotLocation(Size toastSize)
        {
            int margin = DpiAwareService.Scale(16);
            int gap = DpiAwareService.Scale(10);
            var area = Screen.PrimaryScreen.WorkingArea;

            int stackedHeight = _activeToasts.Sum(t => t.Height + gap);

            return new Point(
                area.Right - toastSize.Width - margin,
                area.Bottom - toastSize.Height - margin - stackedHeight);
        }

        // ════════════════════════════════════════════════════════════════════
        //  FADE ANIMATION
        // ════════════════════════════════════════════════════════════════════

        private static void FadeIn(Form form)
        {
            var timer = new Timer { Interval = FadeStepMs };
            timer.Tick += (s, e) =>
            {
                if (form.IsDisposed) { timer.Stop(); timer.Dispose(); return; }

                double next = form.Opacity + FadeStep;
                if (next >= MaxOpacity)
                {
                    form.Opacity = MaxOpacity;
                    timer.Stop();
                    timer.Dispose();
                }
                else
                {
                    form.Opacity = next;
                }
            };
            timer.Start();
        }

        private static void FadeOutAndClose(Form form)
        {
            var timer = new Timer { Interval = FadeStepMs };
            timer.Tick += (s, e) =>
            {
                if (form.IsDisposed) { timer.Stop(); timer.Dispose(); return; }

                double next = form.Opacity - FadeStep;
                if (next <= 0)
                {
                    timer.Stop();
                    timer.Dispose();
                    form.Close();
                }
                else
                {
                    form.Opacity = next;
                }
            };
            timer.Start();
        }
    }
}
