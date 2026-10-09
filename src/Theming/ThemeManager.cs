using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Learnest.Core;
using Learnest.Data;

namespace Learnest.Theming
{
    /// <summary>
    /// The resolved colour set for one theme + accent combination.  Values are Material Design 3
    /// style roles (surface containers, primary/secondary/tertiary, outline, semantic colours)
    /// tuned for desktop readability at small text sizes.
    /// </summary>
    public sealed class ThemePalette
    {
        public bool Dark;

        public Color Primary;
        public Color OnPrimary;
        public Color PrimaryContainer;
        public Color OnPrimaryContainer;
        public Color SecondaryContainer;
        public Color OnSecondaryContainer;
        public Color Tertiary;
        public Color Error;
        public Color OnError;
        public Color Warning;
        public Color Success;

        public Color Surface;
        public Color SurfaceContainerLow;
        public Color SurfaceContainer;
        public Color SurfaceContainerHigh;
        public Color SurfaceContainerHighest;
        public Color SurfaceDim;
        public Color OnSurface;
        public Color OnSurfaceVariant;
        public Color OnSurfaceFaint;
        public Color Outline;
        public Color OutlineVariant;
        public Color Scrim;
        public Color Shadow;
        public Color InverseSurface;
        public Color InverseOnSurface;

        public Color NavigationSurface;
        public Color OnNavigationSurface;

        public Color Selection;
        public Color Hover;

        public static ThemePalette Create(bool dark, Color accent)
        {
            ThemePalette p = new ThemePalette();
            p.Dark = dark;
            Color primary = ColorUtil.NormaliseAccent(accent, dark);
            p.Primary = primary;
            p.OnPrimary = ColorUtil.ContrastText(primary);

            if (dark)
            {
                p.Surface = Color.FromRgb(0x0E, 0x13, 0x1B);
                p.SurfaceContainerLow = Color.FromRgb(0x14, 0x1A, 0x24);
                p.SurfaceContainer = Color.FromRgb(0x19, 0x20, 0x2B);
                p.SurfaceContainerHigh = Color.FromRgb(0x1F, 0x27, 0x34);
                p.SurfaceContainerHighest = Color.FromRgb(0x26, 0x2F, 0x3E);
                p.SurfaceDim = Color.FromRgb(0x0A, 0x0E, 0x14);
                p.OnSurface = Color.FromRgb(0xE7, 0xEC, 0xF4);
                p.OnSurfaceVariant = Color.FromRgb(0xA6, 0xB0, 0xC0);
                p.OnSurfaceFaint = Color.FromRgb(0x74, 0x7F, 0x91);
                p.Outline = Color.FromRgb(0x41, 0x4C, 0x5E);
                p.OutlineVariant = Color.FromRgb(0x2A, 0x33, 0x42);
                p.PrimaryContainer = ColorUtil.Blend(p.SurfaceContainerHigh, primary, 0.42);
                p.OnPrimaryContainer = ColorUtil.ContrastText(p.PrimaryContainer);
                p.SecondaryContainer = ColorUtil.Blend(p.SurfaceContainerHigh, ColorUtil.ShiftHue(primary, 28), 0.34);
                p.OnSecondaryContainer = ColorUtil.ContrastText(p.SecondaryContainer);
                p.Tertiary = ColorUtil.NormaliseAccent(ColorUtil.ShiftHue(primary, 140), true);
                p.Error = Color.FromRgb(0xFF, 0xB4, 0xAB);
                p.OnError = Color.FromRgb(0x6B, 0x11, 0x0C);
                p.Warning = Color.FromRgb(0xFB, 0xC0, 0x4A);
                p.Success = Color.FromRgb(0x6F, 0xDC, 0x9B);
                p.InverseSurface = Color.FromRgb(0xE3, 0xE8, 0xEF);
                p.InverseOnSurface = Color.FromRgb(0x16, 0x1C, 0x26);
                p.NavigationSurface = Color.FromRgb(0x0A, 0x0F, 0x16);
                p.OnNavigationSurface = Color.FromRgb(0xE7, 0xEC, 0xF4);
                p.Hover = Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF);
                p.Selection = Color.FromArgb(0x38, primary.R, primary.G, primary.B);
            }
            else
            {
                p.Surface = Color.FromRgb(0xFB, 0xFC, 0xFE);
                p.SurfaceContainerLow = Color.FromRgb(0xF6, 0xF8, 0xFB);
                p.SurfaceContainer = Color.FromRgb(0xF1, 0xF4, 0xF9);
                p.SurfaceContainerHigh = Color.FromRgb(0xEA, 0xEF, 0xF6);
                p.SurfaceContainerHighest = Color.FromRgb(0xE2, 0xE8, 0xF1);
                p.SurfaceDim = Color.FromRgb(0xDC, 0xE2, 0xEA);
                p.OnSurface = Color.FromRgb(0x14, 0x19, 0x21);
                p.OnSurfaceVariant = Color.FromRgb(0x51, 0x5C, 0x6B);
                p.OnSurfaceFaint = Color.FromRgb(0x86, 0x90, 0xA0);
                p.Outline = Color.FromRgb(0xB9, 0xC2, 0xD0);
                p.OutlineVariant = Color.FromRgb(0xDD, 0xE3, 0xEC);
                p.PrimaryContainer = ColorUtil.Blend(p.Surface, primary, 0.16);
                p.OnPrimaryContainer = ColorUtil.Shade(primary, -0.55);
                p.SecondaryContainer = ColorUtil.Blend(p.Surface, ColorUtil.ShiftHue(primary, 28), 0.14);
                p.OnSecondaryContainer = ColorUtil.Shade(ColorUtil.ShiftHue(primary, 28), -0.5);
                p.Tertiary = ColorUtil.NormaliseAccent(ColorUtil.ShiftHue(primary, 140), false);
                p.Error = Color.FromRgb(0xC0, 0x2A, 0x1F);
                p.OnError = Colors.White;
                p.Warning = Color.FromRgb(0xC2, 0x77, 0x03);
                p.Success = Color.FromRgb(0x1B, 0x7F, 0x4B);
                p.InverseSurface = Color.FromRgb(0x2A, 0x31, 0x3D);
                p.InverseOnSurface = Color.FromRgb(0xF3, 0xF5, 0xF9);
                p.NavigationSurface = Color.FromRgb(0xF3, 0xF6, 0xFB);
                p.OnNavigationSurface = Color.FromRgb(0x1B, 0x22, 0x2C);
                p.Hover = Color.FromArgb(0x14, 0x10, 0x18, 0x24);
                p.Selection = Color.FromArgb(0x2A, primary.R, primary.G, primary.B);
            }

            p.Scrim = Color.FromArgb(0x99, 0, 0, 0);
            p.Shadow = ColorUtil.ShadowFor(dark);
            return p;
        }

        /// <summary>Colour used for urgency badges and timeline markers.</summary>
        public Color UrgencyColor(string urgencyKey)
        {
            switch (urgencyKey)
            {
                case "overdue": return Error;
                case "critical": return Orange;
                case "today": return Warning;
                case "soon": return Primary;
                case "done": return Success;
                default: return OnSurfaceFaint;
            }
        }

        public Color Orange
        {
            get { return Dark ? Color.FromRgb(0xFF, 0xA0, 0x5A) : Color.FromRgb(0xD9, 0x5B, 0x0B); }
        }
    }

    /// <summary>
    /// Owns the live theme: applies brush resources to the application, swaps light/dark, tracks
    /// the system preference, holds the accent colour and prepares the window background.
    /// </summary>
    public static class ThemeManager
    {
        public const string DefaultLightAccent = "#2563EB";
        public const string DefaultDarkAccent = "#7AB0FF";

        public static ThemePalette Palette { get; private set; }
        public static Color Accent { get; private set; }
        /// <summary>Explicit preference: system | light | dark.</summary>
        public static string Mode { get; private set; }
        /// <summary>Whether the dark palette is currently applied (system mode resolved).</summary>
        public static bool IsDark { get; private set; }

        public static event EventHandler PaletteChanged;

        static ThemeManager()
        {
            Mode = ThemeMode.System;
            Accent = ColorUtil.Parse(DefaultLightAccent, Colors.RoyalBlue);
        }

        /// <summary>
        /// Apply a theme.  <paramref name="mode"/> is the stored preference, <paramref name="accentHex"/>
        /// the stored accent; when <paramref name="autoExtract"/> was used the caller passes the
        /// extracted value here.
        /// </summary>
        public static void Apply(string mode, string accentHex, bool notify)
        {
            Mode = ThemeMode.IsValid(mode) ? mode : ThemeMode.System;
            IsDark = ResolveDark(Mode);

            Color accent = ColorUtil.Parse(
                string.IsNullOrEmpty(accentHex) ? (IsDark ? DefaultDarkAccent : DefaultLightAccent) : accentHex,
                ColorUtil.Parse(IsDark ? DefaultDarkAccent : DefaultLightAccent, Colors.RoyalBlue));
            Accent = accent;
            Palette = ThemePalette.Create(IsDark, accent);

            if (Application.Current != null)
            {
                ResourceDictionary resources = Application.Current.Resources;
                WriteBrushes(resources, Palette);
            }

            if (notify)
            {
                EventHandler handler = PaletteChanged;
                if (handler != null) { handler(null, EventArgs.Empty); }
            }
        }

        public static void ApplyFromSettings()
        {
            Program.Trace("  ApplyFromSettings: reading settings");
            AppSettings settings = AppState.SettingsOrNull;
            Program.Trace("  ApplyFromSettings: settings=" + (settings != null));
            string mode = settings != null ? settings.Theme : AppConfigFallbackTheme();
            Program.Trace("  ApplyFromSettings: mode=" + (mode ?? "(null)"));
            string accent = settings != null ? settings.AccentColor : "";
            if (string.IsNullOrEmpty(accent))
            {
                accent = AppState.Config.AccentFallback;
                Program.Trace("  ApplyFromSettings: accent from config=" + (accent ?? "(null)"));
            }
            Apply(mode, accent, false);
            Program.Trace("  ApplyFromSettings: applied");
        }

        private static string AppConfigFallbackTheme()
        {
            try
            {
                Program.Trace("  AppConfigFallbackTheme: loading machine config");
                string theme = AppState.Config.ThemeOverride;
                Program.Trace("  AppConfigFallbackTheme: themeOverride=" + (theme ?? "(null)"));
                return theme;
            }
            catch (Exception ex)
            {
                Program.Trace("  AppConfigFallbackTheme failed: " + Program.Describe(ex));
                return ThemeMode.System;
            }
        }

        private static bool ResolveDark(string mode)
        {
            if (mode == ThemeMode.Light) { return false; }
            if (mode == ThemeMode.Dark) { return true; }
            return SystemPrefersDark();
        }

        /// <summary>Read the Windows app theme (registry), defaulting to light.</summary>
        public static bool SystemPrefersDark()
        {
            try
            {
                object value = Microsoft.Win32.Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme", 1);
                if (value is int) { return ((int)value) == 0; }
            }
            catch
            {
            }
            return false;
        }

        // ------------------------------------------------------------- brush resources
        private static void WriteBrushes(ResourceDictionary resources, ThemePalette p)
        {
            Set(resources, "Brush.Primary", p.Primary);
            Set(resources, "Brush.OnPrimary", p.OnPrimary);
            Set(resources, "Brush.PrimaryContainer", p.PrimaryContainer);
            Set(resources, "Brush.OnPrimaryContainer", p.OnPrimaryContainer);
            Set(resources, "Brush.SecondaryContainer", p.SecondaryContainer);
            Set(resources, "Brush.OnSecondaryContainer", p.OnSecondaryContainer);
            Set(resources, "Brush.Tertiary", p.Tertiary);
            Set(resources, "Brush.Error", p.Error);
            Set(resources, "Brush.OnError", p.OnError);
            Set(resources, "Brush.Warning", p.Warning);
            Set(resources, "Brush.Success", p.Success);

            Set(resources, "Brush.Surface", p.Surface);
            Set(resources, "Brush.SurfaceContainerLow", p.SurfaceContainerLow);
            Set(resources, "Brush.SurfaceContainer", p.SurfaceContainer);
            Set(resources, "Brush.SurfaceContainerHigh", p.SurfaceContainerHigh);
            Set(resources, "Brush.SurfaceContainerHighest", p.SurfaceContainerHighest);
            Set(resources, "Brush.SurfaceDim", p.SurfaceDim);
            Set(resources, "Brush.OnSurface", p.OnSurface);
            Set(resources, "Brush.OnSurfaceVariant", p.OnSurfaceVariant);
            Set(resources, "Brush.OnSurfaceFaint", p.OnSurfaceFaint);
            Set(resources, "Brush.Outline", p.Outline);
            Set(resources, "Brush.OutlineVariant", p.OutlineVariant);
            Set(resources, "Brush.Scrim", p.Scrim);
            Set(resources, "Brush.InverseSurface", p.InverseSurface);
            Set(resources, "Brush.InverseOnSurface", p.InverseOnSurface);
            Set(resources, "Brush.NavigationSurface", p.NavigationSurface);
            Set(resources, "Brush.OnNavigationSurface", p.OnNavigationSurface);
            Set(resources, "Brush.Hover", p.Hover);
            Set(resources, "Brush.Selection", p.Selection);

            resources["Brush.Shadow"] = new SolidColorBrush(p.Shadow);
            resources["Double.CornerSmall"] = 8.0;
            resources["Double.CornerMedium"] = 14.0;
            resources["Double.CornerLarge"] = 22.0;
            resources["String.ThemeName"] = p.Dark ? "Dark" : "Light";
            resources["ThemeAccentColor"] = p.Primary;
        }

        private static void Set(ResourceDictionary resources, string key, Color color)
        {
            SolidColorBrush brush;
            if (resources.Contains(key))
            {
                brush = resources[key] as SolidColorBrush;
                if (brush != null && !brush.IsFrozen)
                {
                    brush.Color = color;
                    return;
                }
            }
            brush = new SolidColorBrush(color);
            brush.Freeze();
            resources[key] = brush;
        }

        // ------------------------------------------------------------- system watching
        private static bool _watching;

        /// <summary>Follow Windows light/dark changes while the preference is "system".</summary>
        public static void StartSystemWatcher()
        {
            if (_watching) { return; }
            _watching = true;
            try
            {
                Microsoft.Win32.SystemEvents.UserPreferenceChanged += delegate(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
                {
                    if (e.Category != Microsoft.Win32.UserPreferenceCategory.General &&
                        e.Category != Microsoft.Win32.UserPreferenceCategory.VisualStyle)
                    {
                        return;
                    }
                    if (Mode != ThemeMode.System) { return; }
                    bool dark = SystemPrefersDark();
                    if (dark == IsDark) { return; }
                    Apply(Mode, ColorUtil.ToHex(Accent), true);
                };
            }
            catch
            {
                // SystemEvents needs a message pump; failure is not fatal.
            }
        }

        // ------------------------------------------------------------- accent helpers
        /// <summary>Load an image from inside the container and pull an accent colour out of it.</summary>
        public static Color? ExtractAccentFromImage(string containerRelativePath)
        {
            try
            {
                ContainerSession session = AppState.SessionOrNull;
                if (session == null || string.IsNullOrEmpty(containerRelativePath)) { return null; }
                string path = session.ResolveRelative(containerRelativePath);
                if (!File.Exists(path)) { return null; }
                BitmapSource source = LoadBitmap(path, 1200);
                Color? dominant = ColorUtil.DominantAccent(source);
                if (dominant.HasValue) { return ColorUtil.NormaliseAccent(dominant.Value, IsDark); }
            }
            catch
            {
            }
            return null;
        }

        /// <summary>Load a bitmap with a decode cap so huge photos do not stall the UI.</summary>
        public static BitmapSource LoadBitmap(string path, int maxPixelWidth)
        {
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            if (maxPixelWidth > 0) { image.DecodePixelWidth = maxPixelWidth; }
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}
