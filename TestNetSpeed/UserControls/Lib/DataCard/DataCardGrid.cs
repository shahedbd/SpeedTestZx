// UserControls/Lib/DataCard/DataCardGrid.cs
using System.ComponentModel;

namespace TestNetSpeed.UserControls.Lib.DataCard
{
    /// <summary>
    /// Single-row strip of equally sized <see cref="DataCardControl"/>s with a
    /// fixed gap between them. Columns are added as cards are added, so the
    /// same control works for 2, 3, 4 … cards.
    /// </summary>
    [DesignerCategory("Code")]
    public class DataCardGrid : TableLayoutPanel
    {
        private readonly List<DataCardControl> _cards = new();
        private int _gap = 10;

        public DataCardGrid()
        {
            RowCount = 1;
            ColumnCount = 0;
            BackColor = Color.Transparent;
            Padding = Padding.Empty;
            Margin = Padding.Empty;
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None;
            RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            SetStyle(ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.AllPaintingInWmPaint, true);
        }

        /// <summary>Horizontal gap between cards at 96 DPI.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Gap
        {
            get => _gap;
            set
            {
                _gap = Math.Max(0, value);
                ApplyMargins();
            }
        }

        public IReadOnlyList<DataCardControl> Cards => _cards;

        /// <summary>Appends a card in a new equal-width column and returns it.</summary>
        public DataCardControl AddCard(DataCardControl card)
        {
            SuspendLayout();

            _cards.Add(card);
            ColumnCount = _cards.Count;

            ColumnStyles.Clear();
            float pct = 100f / _cards.Count;
            for (int i = 0; i < _cards.Count; i++)
                ColumnStyles.Add(new ColumnStyle(SizeType.Percent, pct));

            card.Dock = DockStyle.Fill;
            Controls.Add(card, _cards.Count - 1, 0);
            ApplyMargins();

            ResumeLayout(true);
            return card;
        }

        /// <summary>Applies one palette to every card (e.g. on theme change).</summary>
        public void ApplyPalette(DataCardPalette palette)
        {
            foreach (var card in _cards)
                card.Palette = palette;
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            ApplyMargins();
        }

        // Split the gap across neighbours; outer edges stay flush.
        private void ApplyMargins()
        {
            int half = (int)Math.Round(_gap * DeviceDpi / 96f / 2f);

            for (int i = 0; i < _cards.Count; i++)
            {
                int left = i == 0 ? 0 : half;
                int right = i == _cards.Count - 1 ? 0 : half;
                _cards[i].Margin = new Padding(left, 0, right, 0);
            }
        }
    }
}
