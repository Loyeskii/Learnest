using System;
using System.Collections.Generic;
using System.Windows.Threading;
using Learnest.Data;
using Learnest.Services;

namespace Learnest.Views
{
    /// <summary>
    /// Periodically checks the profile for items approaching their deadline and raises an alert.
    ///
    /// Two triggers drive the flashing reminder:
    ///   * a timer tick (settings.AlertCheckSeconds, 60s by default), and
    ///   * a focus event - whenever the pin widget gains mouse focus it re-checks immediately,
    ///     which is what the "捕获鼠标焦点时检测" requirement asks for.
    ///
    /// Only unfinished items are ever reported.
    /// </summary>
    public sealed class ReminderService : IDisposable
    {
        private readonly MainWindow _shell;
        private readonly DispatcherTimer _timer;
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);
        private DateTime _lastCheck = DateTime.MinValue;
        private bool _primed;

        public ReminderService(MainWindow shell)
        {
            _shell = shell;
            _timer = new DispatcherTimer(DispatcherPriority.Background);
            _timer.Tick += OnTick;
        }

        public DateTime LastCheck { get { return _lastCheck; } }

        public void Start()
        {
            int seconds = 60;
            if (AppState.SessionOrNull != null && AppState.SessionOrNull.Settings.AlertCheckSeconds > 4)
            {
                seconds = AppState.SessionOrNull.Settings.AlertCheckSeconds;
            }
            _timer.Interval = TimeSpan.FromSeconds(seconds);
            _timer.Start();
            // Do not flash the user immediately on start-up: record the current state silently.
            List<TaskQuery.AlertHit> hits = Check();
            _primed = true;
        }

        public void Stop()
        {
            _timer.Stop();
        }

        /// <summary>Re-arm the alert bookkeeping, e.g. after a container switch.</summary>
        public void Reset()
        {
            _seen.Clear();
            _primed = false;
        }

        private void OnTick(object sender, EventArgs e)
        {
            // Follow setting changes without restarting the app.
            if (AppState.SessionOrNull != null)
            {
                int seconds = AppState.SessionOrNull.Settings.AlertCheckSeconds > 4
                    ? AppState.SessionOrNull.Settings.AlertCheckSeconds : 60;
                if (Math.Abs(_timer.Interval.TotalSeconds - seconds) > 1)
                {
                    _timer.Interval = TimeSpan.FromSeconds(seconds);
                }
            }
            List<TaskQuery.AlertHit> hits = Check();
            if (_shell != null) { _shell.OnReminderTick(hits, _newThisCheck); }
        }

        private int _newThisCheck;

        /// <summary>
        /// Run one check.  Returns every item inside the alert window; <see cref="_newThisCheck"/>
        /// holds how many of them had not been seen before.
        /// </summary>
        public List<TaskQuery.AlertHit> Check()
        {
            List<TaskQuery.AlertHit> hits = new List<TaskQuery.AlertHit>();
            _newThisCheck = 0;
            if (AppState.SessionOrNull == null) { return hits; }

            try
            {
                hits = TaskQuery.Alerts(DateTime.Now);
            }
            catch
            {
                return hits;
            }
            _lastCheck = DateTime.Now;

            HashSet<string> current = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < hits.Count; i++)
            {
                string key = hits[i].Key;
                current.Add(key);
                if (!_seen.Contains(key)) { _newThisCheck++; }
            }
            // Keep only keys that are still in range so a dismissed item can alert again later.
            _seen.Clear();
            foreach (string key in current) { _seen.Add(key); }
            if (!_primed) { _newThisCheck = 0; }
            return hits;
        }

        /// <summary>Force a focus-triggered check (called by the pin widget on activation).</summary>
        public List<TaskQuery.AlertHit> CheckOnFocus()
        {
            List<TaskQuery.AlertHit> hits = Check();
            if (AppState.SessionOrNull != null && AppState.SessionOrNull.Settings.AlertOnFocus)
            {
                if (WidgetWindow.Current != null)
                {
                    WidgetWindow.Current.SyncReminderState(hits);
                }
            }
            return hits;
        }

        public void Dispose()
        {
            try
            {
                _timer.Stop();
                _timer.Tick -= OnTick;
            }
            catch
            {
            }
        }
    }
}
