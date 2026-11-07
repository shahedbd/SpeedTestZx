using SpeedTestZx.Model;
using SpeedTestZx.UserControls.Prompts;

namespace SpeedTestZx.Managers
{
    /// <summary>
    /// Owns the Pro upgrade overlay and every "may the user do this" UI gate.
    /// Callers only ask Show()/ShowPro()/CloseAll(); this class decides whether
    /// to proceed or to prompt. Access itself is always
    /// EntitlementManager.HasAccess — nothing here reads the Store.
    ///
    /// The overlay sits over the content area only, so the sidebar and header
    /// stay usable while it is open.
    /// </summary>
    public sealed class PromptManager
    {
        private readonly ProPromptOverlay _prompt;

        /// <summary>The main window's prompt manager, for pages and the header.</summary>
        public static PromptManager? Current { get; private set; }

        public PromptManager(Control host)
        {
            _prompt = new ProPromptOverlay();
            host.Controls.Add(_prompt);
            _prompt.BringToFront();

            Current = this;
        }

        /// <summary>
        /// Runs a gate: invokes <paramref name="onAccessible"/> when the user
        /// has <paramref name="feature"/>; otherwise shows the upgrade prompt,
        /// wired to re-enter this same gate after a successful purchase so the
        /// user lands where they were going.
        /// </summary>
        public void Show(ProFeature feature, Action onAccessible)
        {
            if (EntitlementManager.Instance.HasAccess(feature))
            {
                CloseAll();
                onAccessible();
            }
            else
            {
                _prompt.ShowPrompt(feature, () => Show(feature, onAccessible));
            }
        }

        /// <summary>Opens the prompt with generic copy — the header's Go Pro button.</summary>
        public void ShowPro(Action? onUnlocked = null) => _prompt.ShowPrompt(null, onUnlocked);

        /// <summary>Re-themes the overlay — call from MainForm.ApplyTheme().</summary>
        public void ApplyTheme() => _prompt.ApplyTheme();

        /// <summary>Closes the prompt — call when navigating to another page.</summary>
        public void CloseAll() => _prompt.ClosePrompt();
    }
}
