using SpeedTestZx.Helper;
using SpeedTestZx.Managers;
using SpeedTestZx.Model;
using SpeedTestZx.UserControls;
using Windows.Services.Store;

namespace SpeedTestZx.UserControls.Prompts
{
    /// <summary>
    /// Upsell overlay: dimmed backdrop, centred ProPromptCard and the Microsoft
    /// Store purchase flow. Ported from WiFiPasswordViewer.
    ///
    /// It has a "Not now": Free users keep using the free
    /// pages and must never feel trapped. Owned by PromptManager.
    /// </summary>
    public sealed class ProPromptOverlay : DpiAwareUserControl
    {
        private ProPromptCard? _card;
        private ProFeature? _feature;
        private Action? _onUnlocked;

        public ProPromptOverlay()
        {
            Dock = DockStyle.Fill;
            BackColor = Backdrop;
            Visible = false;

            // Safety net for small windows / 200% DPI: a card taller than the
            // content area scrolls instead of losing its CTA off the bottom.
            AutoScroll = true;

            Resize += (s, e) => FitCard();
        }

        /// <summary>Height of the full card at the current DPI, once measured.</summary>
        private int _fullCardHeight;
        private bool _building;

        /// <summary>Dimming scrim — dark over a dark app, light over a light one.</summary>
        private Color Backdrop => themeManager.IsDarkTheme
            ? Color.FromArgb(170, 0, 0, 0)
            : Color.FromArgb(150, 240, 242, 245);

        // ─────────────────────────────────────────────────────────────────────
        // PUBLIC API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Shows the prompt for <paramref name="feature"/> (null = generic copy,
        /// from the header button). <paramref name="onUnlocked"/> runs after a
        /// successful purchase or restore, so the caller can re-open the page or
        /// retry the action the user asked for.
        /// </summary>
        public void ShowPrompt(ProFeature? feature, Action? onUnlocked)
        {
            _feature = feature;
            _onUnlocked = onUnlocked;

            // Visible first: a hidden control is skipped by Dock layout, so its
            // size — which picks the full or compact card — would be stale.
            Visible = true;
            BringToFront();

            // Rebuilt per show: the subhead names the feature, and it keeps the
            // card in step with the current theme, DPI and window height.
            if (_card is not { IsBusy: true })
                BuildCard();

            CenterCard();
        }

        public void ClosePrompt()
        {
            Visible = false;
            _onUnlocked = null;
        }

        /// <summary>
        /// Re-themes the overlay. The card colours every label at build time, so
        /// it is rebuilt rather than re-coloured piece by piece.
        /// </summary>
        public void ApplyTheme()
        {
            BackColor = Backdrop;
            _fullCardHeight = 0; // re-measure: DPI may have changed with the theme rebuild

            if (_card != null && !_card.IsBusy)
                BuildCard();
        }

        // ─────────────────────────────────────────────────────────────────────
        // CARD
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Full card when it fits, compact one when the window is too short
        /// (small screens, 125-200% DPI). AutoScroll covers anything still taller.
        /// </summary>
        private void BuildCard()
        {
            _building = true;
            try
            {
                if (_fullCardHeight == 0)
                {
                    // Measure once per DPI/theme; the card's height is content-driven.
                    using var probe = CreateCard(compact: false);
                    _fullCardHeight = probe.Height;
                }

                var previous = _card;
                _card = CreateCard(compact: ShouldBeCompact);
                _card.BuyRequested += Card_BuyRequested;
                _card.DismissRequested += (s, e) => ClosePrompt();
                _card.RestoreRequested += Card_RestoreRequested;

                Controls.Add(_card);

                if (previous != null)
                {
                    Controls.Remove(previous);
                    previous.Dispose();
                }

                AutoScrollPosition = Point.Empty;
                CenterCard();
                PerformLayout();
            }
            finally
            {
                _building = false;
            }
        }

        // Top|Left, not None: AutoScroll only counts children anchored to the
        // top-left when sizing the scroll area, so an un-anchored card never
        // got a scrollbar. CenterCard positions it by hand anyway.
        private ProPromptCard CreateCard(bool compact) =>
            new(themeManager, _dpiScale, ProPromptContent.ForFeature(_feature), compact)
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

        private bool ShouldBeCompact => _fullCardHeight > ClientSize.Height;

        /// <summary>Resize: switch layouts when the fit changes, then recentre.</summary>
        private void FitCard()
        {
            if (_building || _card == null) return;

            CenterCard();

            // Deferred: rebuilt mid-resize, the scroll area kept the old card's
            // extent and left empty scrollbars behind.
            if (Visible && !_card.IsBusy && _card.IsCompact != ShouldBeCompact && IsHandleCreated)
                BeginInvoke(new Action(() =>
                {
                    if (!IsDisposed && _card is { IsBusy: false } card && card.IsCompact != ShouldBeCompact)
                        BuildCard();
                }));
        }

        private void CenterCard()
        {
            if (_card == null) return;

            // Clamped so a card taller than the overlay still shows its top; the
            // scroll offset is added because Location is relative to the scrolled view.
            _card.Location = new Point(
                Math.Max(0, (ClientSize.Width - _card.Width) / 2) + AutoScrollPosition.X,
                Math.Max(0, (ClientSize.Height - _card.Height) / 2) + AutoScrollPosition.Y);
        }

        // ─────────────────────────────────────────────────────────────────────
        // PURCHASE
        // ─────────────────────────────────────────────────────────────────────

        private async void Card_BuyRequested(object? sender, EventArgs e)
        {
            var card = _card;
            if (card == null || card.IsBusy) return;

            card.SetBusy(true);
            try
            {
                var status = await EntitlementManager.Instance.PurchaseProAsync();

                switch (status)
                {
                    case StorePurchaseStatus.Succeeded or StorePurchaseStatus.AlreadyPurchased:
                        await CompleteUnlockAsync();
                        break;

                    case StorePurchaseStatus.NotPurchased:
                        // User closed the Store dialog — not an error.
                        break;

                    case null:
                        NotificationManager.Instance.ShowWarning("Microsoft Store is not available.", AppConfig.AppName);
                        break;

                    default:
                        NotificationManager.Instance.ShowWarning(
                            "Couldn't reach the Microsoft Store. Try again.", "Microsoft Store");
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            finally
            {
                if (!card.IsDisposed) card.SetBusy(false);
            }
        }

        /// <summary>
        /// The Store can take a moment to report a fresh purchase as active:
        /// retry once after 2 s, then tell the user rather than leave them unsure.
        /// </summary>
        private async Task CompleteUnlockAsync()
        {
            var entitlements = EntitlementManager.Instance;

            if (!entitlements.HasAccess(UserTier.Pro))
            {
                await Task.Delay(2000);
                await entitlements.RefreshAsync();
            }

            if (!entitlements.HasAccess(UserTier.Pro))
            {
                NotificationManager.Instance.ShowInfo(
                    "Purchase complete. Unlocking can take a minute — use \"Refresh status\" on this card shortly.",
                    AppConfig.AppName);
                return;
            }

            Unlocked();
        }

        private async void Card_RestoreRequested(object? sender, EventArgs e)
        {
            var card = _card;
            if (card == null || card.IsBusy) return;

            card.SetBusy(true);
            try
            {
                await EntitlementManager.Instance.RefreshAsync();

                if (EntitlementManager.Instance.HasAccess(UserTier.Pro))
                    Unlocked();
                else
                    NotificationManager.Instance.ShowInfo(
                        "No Pro purchase was found for this Microsoft account.", AppConfig.AppName);
            }
            finally
            {
                if (!card.IsDisposed) card.SetBusy(false);
            }
        }

        private void Unlocked()
        {
            var onUnlocked = _onUnlocked;
            ClosePrompt();

            NotificationManager.Instance.ShowSuccess(
                $"{AppConfig.AppName} Pro is now unlocked. Thank you!", AppConfig.AppName);

            onUnlocked?.Invoke();
        }
    }
}
