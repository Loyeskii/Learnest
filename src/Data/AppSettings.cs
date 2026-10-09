using System;
using System.Collections.Generic;
using System.Globalization;
using Learnest.Core;

namespace Learnest.Data
{
    /// <summary>
    /// Everything the user can configure, stored inside the container (table app_settings) plus
    /// the window geometry.  Nothing here is machine scoped, which is what makes a container
    /// folder copyable between devices.
    /// </summary>
    public sealed class AppSettings
    {
        // Appearance
        public string Theme = ThemeMode.System;          // system | light | dark
        public string AccentColor = "#2563EB";
        public bool AutoExtractAccent = true;
        public string BackgroundImage = "";              // container relative path
        public string BackgroundMode = Data.BackgroundMode.Fill;
        public double BackgroundBlur = 0;                // 0..40 px
        public double BackgroundScrim = 0.35;            // 0..1 darkening/scrim
        public bool BackgroundScrimLight;
        public double UiScale = 1.0;
        public bool CompactDensity;
        public bool MicaWindow = true;

        // Language
        public string Language = "";                     // "" = follow system, or a pack code

        // Window geometry (also container scoped so a copied container restores its layout)
        public double MainWindowLeft = double.NaN;
        public double MainWindowTop = double.NaN;
        public double MainWindowWidth = 1280;
        public double MainWindowHeight = 820;
        public string MainWindowState = "normal";
        public string LastView = "dashboard";

        // Pin widget
        public bool WidgetVisible;
        public bool WidgetTopmost = true;
        public double WidgetLeft = double.NaN;
        public double WidgetTop = double.NaN;
        public double WidgetWidth = 340;
        public double WidgetHeight = 460;
        public double WidgetOpacity = 0.96;
        public string WidgetFilter = "upcoming";         // upcoming | today | week | all
        public string WidgetSort = "dueAsc";             // dueAsc | dueDesc | modifiedDesc | modifiedAsc | createdDesc | createdAsc | priority
        public bool WidgetShowCompleted;
        public bool WidgetShowToolbar = true;

        // Alerts
        public int AlertLeadHours = 72;
        public bool AlertFlash = true;
        public bool AlertOnFocus = true;
        public bool AlertSound;
        /// <summary>How many times the widget pulses when a new reminder appears (then it settles).</summary>
        public int FlashCount = 3;
        public int AlertFlashIntervalMs = 700;
        public int AlertCheckSeconds = 60;

        // Behaviour
        public bool ConfirmDelete = true;
        public bool AutoBackupOnExit;
        public int AutoBackupKeep = 10;
        public string LastBackupAt = "";
        public bool MinimiseToTray;
        public bool CheckUpdates;
        public double TimelineZoom = 1.0;
        public string TimelineSpan = "90";               // days shown by default
        public bool TimelineShowCreated = true;
        public bool TimelineShowUpdated = true;
        public bool TimelineShowDue = true;

        public AppSettings Clone()
        {
            return (AppSettings)MemberwiseClone();
        }

        // ------------------------------------------------------------ persistence
        public static AppSettings LoadFrom(SqliteDatabase db)
        {
            AppSettings s = new AppSettings();
            Dictionary<string, string> values = ReadAll(db);
            s.Theme = GetString(values, "themeMode", s.Theme);
            s.AccentColor = GetString(values, "accentColor", s.AccentColor);
            s.AutoExtractAccent = GetBool(values, "autoExtractAccent", s.AutoExtractAccent);
            s.BackgroundImage = GetString(values, "backgroundImage", s.BackgroundImage);
            s.BackgroundMode = GetString(values, "backgroundMode", s.BackgroundMode);
            s.BackgroundBlur = GetDouble(values, "backgroundBlur", s.BackgroundBlur);
            s.BackgroundScrim = GetDouble(values, "backgroundScrim", s.BackgroundScrim);
            s.BackgroundScrimLight = GetBool(values, "backgroundScrimLight", s.BackgroundScrimLight);
            s.UiScale = GetDouble(values, "uiScale", s.UiScale);
            s.CompactDensity = GetBool(values, "compactDensity", s.CompactDensity);
            s.MicaWindow = GetBool(values, "micaWindow", s.MicaWindow);
            s.Language = GetString(values, "language", s.Language);
            s.MainWindowLeft = GetDouble(values, "mainWindowLeft", s.MainWindowLeft);
            s.MainWindowTop = GetDouble(values, "mainWindowTop", s.MainWindowTop);
            s.MainWindowWidth = GetDouble(values, "mainWindowWidth", s.MainWindowWidth);
            s.MainWindowHeight = GetDouble(values, "mainWindowHeight", s.MainWindowHeight);
            s.MainWindowState = GetString(values, "mainWindowState", s.MainWindowState);
            s.LastView = GetString(values, "lastView", s.LastView);
            s.WidgetVisible = GetBool(values, "widgetVisible", s.WidgetVisible);
            s.WidgetTopmost = GetBool(values, "widgetTopmost", s.WidgetTopmost);
            s.WidgetLeft = GetDouble(values, "widgetLeft", s.WidgetLeft);
            s.WidgetTop = GetDouble(values, "widgetTop", s.WidgetTop);
            s.WidgetWidth = GetDouble(values, "widgetWidth", s.WidgetWidth);
            s.WidgetHeight = GetDouble(values, "widgetHeight", s.WidgetHeight);
            s.WidgetOpacity = GetDouble(values, "widgetOpacity", s.WidgetOpacity);
            s.WidgetFilter = GetString(values, "widgetFilter", s.WidgetFilter);
            s.WidgetSort = GetString(values, "widgetSort", s.WidgetSort);
            s.WidgetShowCompleted = GetBool(values, "widgetShowCompleted", s.WidgetShowCompleted);
            s.WidgetShowToolbar = GetBool(values, "widgetShowToolbar", s.WidgetShowToolbar);
            s.AlertLeadHours = (int)GetDouble(values, "alertLeadHours", s.AlertLeadHours);
            s.AlertFlash = GetBool(values, "alertFlash", s.AlertFlash);
            s.AlertOnFocus = GetBool(values, "alertOnFocus", s.AlertOnFocus);
            s.AlertSound = GetBool(values, "alertSound", s.AlertSound);
            s.FlashCount = (int)GetDouble(values, "flashCount", s.FlashCount);
            s.AlertFlashIntervalMs = (int)GetDouble(values, "alertFlashIntervalMs", s.AlertFlashIntervalMs);
            s.AlertCheckSeconds = (int)GetDouble(values, "alertCheckSeconds", s.AlertCheckSeconds);
            s.ConfirmDelete = GetBool(values, "confirmDelete", s.ConfirmDelete);
            s.AutoBackupOnExit = GetBool(values, "autoBackupOnExit", s.AutoBackupOnExit);
            s.AutoBackupKeep = (int)GetDouble(values, "autoBackupKeep", s.AutoBackupKeep);
            s.LastBackupAt = GetString(values, "lastBackupAt", s.LastBackupAt);
            s.MinimiseToTray = GetBool(values, "minimiseToTray", s.MinimiseToTray);
            s.CheckUpdates = GetBool(values, "checkUpdates", s.CheckUpdates);
            s.TimelineZoom = GetDouble(values, "timelineZoom", s.TimelineZoom);
            s.TimelineSpan = GetString(values, "timelineSpan", s.TimelineSpan);
            s.TimelineShowCreated = GetBool(values, "timelineShowCreated", s.TimelineShowCreated);
            s.TimelineShowUpdated = GetBool(values, "timelineShowUpdated", s.TimelineShowUpdated);
            s.TimelineShowDue = GetBool(values, "timelineShowDue", s.TimelineShowDue);
            if (!ThemeMode.IsValid(s.Theme)) { s.Theme = ThemeMode.System; }
            if (s.AlertLeadHours <= 0) { s.AlertLeadHours = 72; }
            if (s.FlashCount < 1) { s.FlashCount = 1; }
            if (s.FlashCount > 20) { s.FlashCount = 20; }
            if (s.AlertFlashIntervalMs < 200) { s.AlertFlashIntervalMs = 700; }
            return s;
        }

        public void SaveTo(SqliteDatabase db)
        {
            db.Transaction(delegate
            {
                Write(db, "themeMode", Theme);
                Write(db, "accentColor", AccentColor);
                Write(db, "autoExtractAccent", AutoExtractAccent);
                Write(db, "backgroundImage", BackgroundImage);
                Write(db, "backgroundMode", BackgroundMode);
                Write(db, "backgroundBlur", BackgroundBlur);
                Write(db, "backgroundScrim", BackgroundScrim);
                Write(db, "backgroundScrimLight", BackgroundScrimLight);
                Write(db, "uiScale", UiScale);
                Write(db, "compactDensity", CompactDensity);
                Write(db, "micaWindow", MicaWindow);
                Write(db, "language", Language);
                Write(db, "mainWindowLeft", MainWindowLeft);
                Write(db, "mainWindowTop", MainWindowTop);
                Write(db, "mainWindowWidth", MainWindowWidth);
                Write(db, "mainWindowHeight", MainWindowHeight);
                Write(db, "mainWindowState", MainWindowState);
                Write(db, "lastView", LastView);
                Write(db, "widgetVisible", WidgetVisible);
                Write(db, "widgetTopmost", WidgetTopmost);
                Write(db, "widgetLeft", WidgetLeft);
                Write(db, "widgetTop", WidgetTop);
                Write(db, "widgetWidth", WidgetWidth);
                Write(db, "widgetHeight", WidgetHeight);
                Write(db, "widgetOpacity", WidgetOpacity);
                Write(db, "widgetFilter", WidgetFilter);
                Write(db, "widgetSort", WidgetSort);
                Write(db, "widgetShowCompleted", WidgetShowCompleted);
                Write(db, "widgetShowToolbar", WidgetShowToolbar);
                Write(db, "alertLeadHours", AlertLeadHours);
                Write(db, "alertFlash", AlertFlash);
                Write(db, "alertOnFocus", AlertOnFocus);
                Write(db, "alertSound", AlertSound);
                Write(db, "flashCount", FlashCount);
                Write(db, "alertFlashIntervalMs", AlertFlashIntervalMs);
                Write(db, "alertCheckSeconds", AlertCheckSeconds);
                Write(db, "confirmDelete", ConfirmDelete);
                Write(db, "autoBackupOnExit", AutoBackupOnExit);
                Write(db, "autoBackupKeep", AutoBackupKeep);
                Write(db, "lastBackupAt", LastBackupAt);
                Write(db, "minimiseToTray", MinimiseToTray);
                Write(db, "checkUpdates", CheckUpdates);
                Write(db, "timelineZoom", TimelineZoom);
                Write(db, "timelineSpan", TimelineSpan);
                Write(db, "timelineShowCreated", TimelineShowCreated);
                Write(db, "timelineShowUpdated", TimelineShowUpdated);
                Write(db, "timelineShowDue", TimelineShowDue);
            });
        }

        private static Dictionary<string, string> ReadAll(SqliteDatabase db)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            using (SqliteStatement statement = db.Prepare("SELECT key, value FROM app_settings;"))
            {
                while (statement.Step())
                {
                    string key = statement.GetString(0);
                    if (key == null) { continue; }
                    values[key] = statement.GetString(1);
                }
            }
            return values;
        }

        private static void Write(SqliteDatabase db, string key, string value)
        {
            if (value == null) { value = ""; }
            db.ExecuteNonQuery(
                "INSERT INTO app_settings(key, value) VALUES(?, ?) " +
                "ON CONFLICT(key) DO UPDATE SET value = excluded.value;", key, value);
        }

        private static void Write(SqliteDatabase db, string key, bool value) { Write(db, key, value ? "1" : "0"); }

        private static void Write(SqliteDatabase db, string key, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) { Write(db, key, ""); return; }
            Write(db, key, value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void Write(SqliteDatabase db, string key, int value)
        {
            Write(db, key, value.ToString(CultureInfo.InvariantCulture));
        }

        private static string GetString(Dictionary<string, string> values, string key, string fallback)
        {
            string value;
            if (values.TryGetValue(key, out value) && value != null && value.Length > 0) { return value; }
            return fallback;
        }

        private static bool GetBool(Dictionary<string, string> values, string key, bool fallback)
        {
            string value;
            if (!values.TryGetValue(key, out value) || value == null || value.Length == 0) { return fallback; }
            return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static double GetDouble(Dictionary<string, string> values, string key, double fallback)
        {
            string value;
            if (!values.TryGetValue(key, out value) || string.IsNullOrEmpty(value)) { return fallback; }
            double parsed;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)) { return parsed; }
            return fallback;
        }
    }
}
