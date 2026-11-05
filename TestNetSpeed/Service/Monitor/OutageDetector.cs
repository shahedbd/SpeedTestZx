using TestNetSpeed.Helper;
using TestNetSpeed.Model;

namespace TestNetSpeed.Service.Monitor
{
    /// <summary>
    /// Turns the 10 s probe stream into outages (T35):
    ///   • 3 lost probes in a row to 1.1.1.1 start an outage (dated from the
    ///     first lost probe);
    ///   • 2 successes in a row end it (dated from the first success).
    /// Cause: when the router also stopped answering (or there was no
    /// gateway at all) for at least half the outage, it's "Router / Wi-Fi";
    /// otherwise the router was fine and it's "ISP / internet".
    /// </summary>
    public sealed class OutageDetector
    {
        private const int LostToStart = 3;
        private const int OkToEnd = 2;

        private int _lostStreak, _okStreak;
        private DateTime _firstLostAt, _firstOkAt;
        private int _routerDown, _routerUp;

        public Outage? Current { get; private set; }

        public event EventHandler<Outage>? Started;
        public event EventHandler<Outage>? Ended;

        /// <param name="internetOk">1.1.1.1 answered.</param>
        /// <param name="routerOk">Gateway answered; null when there was no gateway (adapter down).</param>
        public void OnProbe(DateTime at, bool internetOk, bool? routerOk)
        {
            if (Current != null)
            {
                if (routerOk == true) _routerUp++; else _routerDown++;
            }

            if (!internetOk)
            {
                _okStreak = 0;
                if (_lostStreak++ == 0) _firstLostAt = at;

                if (Current == null && _lostStreak >= LostToStart)
                    Begin(routerOk);
                return;
            }

            _lostStreak = 0;
            if (Current == null) return;

            if (_okStreak++ == 0) _firstOkAt = at;
            if (_okStreak >= OkToEnd)
                Finish(_firstOkAt);
        }

        private void Begin(bool? routerOk)
        {
            _routerDown = routerOk == true ? 0 : LostToStart;
            _routerUp = routerOk == true ? LostToStart : 0;

            Current = new Outage { Start = _firstLostAt, Cause = CauseNow() };
            MonitorStore.SaveOutage(Current);
            Started?.Invoke(this, Current);
        }

        private void Finish(DateTime end)
        {
            var outage = Current!;
            outage.End = end;
            outage.Cause = CauseNow();
            Current = null;
            _okStreak = 0;

            MonitorStore.SaveOutage(outage);
            Ended?.Invoke(this, outage);
        }

        private string CauseNow() =>
            _routerDown >= _routerUp ? Outage.CauseRouter : Outage.CauseIsp;

        /// <summary>
        /// Closes an outage left open by a crash or shutdown at the last minute
        /// the log recorded after it started — the time the app wasn't running
        /// is "not recorded", never counted as downtime.
        /// </summary>
        public static void CloseLeftOpen()
        {
            foreach (var open in MonitorStore.LoadOutages().Where(o => o.End == null))
            {
                var last = MonitorStore.ReadMinutes(open.Start, DateTime.Now).LastOrDefault();
                open.End = last != null && last.Time > open.Start ? last.Time.AddMinutes(1) : open.Start;
                MonitorStore.SaveOutage(open);
            }
        }

        /// <summary>Stops tracking (app closing): the open outage is closed by <see cref="CloseLeftOpen"/> next start.</summary>
        public void Reset()
        {
            Current = null;
            _lostStreak = _okStreak = 0;
        }
    }
}
