using System.Diagnostics;
using SpeedTestZx.Helper;
using SpeedTestZx.Helper.Report;
using SpeedTestZx.Managers;
using SpeedTestZx.Model;

namespace SpeedTestZx.UserControls.Pages.Report
{
    /// <summary>IspReportCardControl — "Export PDF report" (T34, Pro).</summary>
    public partial class IspReportCardControl
    {
        private bool _exporting;

        private async Task ExportPdfAsync()
        {
            if (_exporting || _data == null) return;

            if (!EntitlementManager.Instance.HasAccess(ProFeature.ReportPdf))
            {
                PromptManager.Current?.Show(ProFeature.ReportPdf, () => { });
                return;
            }

            if (_data.Tests.Count == 0)
            {
                NotificationManager.Instance.ShowInfo("There are no tests in this range to report on.", "ISP Report Card");
                return;
            }

            using var dialog = new SaveFileDialog
            {
                Title = "Save ISP report",
                Filter = "PDF document (*.pdf)|*.pdf",
                FileName = $"ISP-Report-{_data.From:yyyy-MM-dd}-to-{_data.To.AddMinutes(-1):yyyy-MM-dd}.pdf",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            };
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;

            var data = _data;
            string path = dialog.FileName;

            _exporting = true;
            _btnExport.Enabled = false;
            _btnExport.Text = "  Creating PDF…";
            try
            {
                await Task.Run(() => PdfReportBuilder.Write(data, path));
                NotificationManager.Instance.ShowSuccess($"Saved {Path.GetFileName(path)}", "ISP Report Card");

                try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
                catch { /* no PDF viewer — the file is still saved */ }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                NotificationManager.Instance.ShowError("Couldn't create the PDF. Is the file open in another app?", "ISP Report Card");
            }
            finally
            {
                _exporting = false;
                if (!IsDisposed)
                {
                    _btnExport.Enabled = true;
                    _btnExport.Text = "  Export PDF report";
                }
            }
        }
    }
}
