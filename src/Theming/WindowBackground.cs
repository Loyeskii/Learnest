using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Learnest.Data;

namespace Learnest.Theming
{
    /// <summary>
    /// Paints the optional user background image behind a view: image mode (fill / fit / stretch /
    /// tile / center), gaussian blur amount and a scrim layer, all driven by the container's
    /// settings so the look travels with the container folder.
    /// </summary>
    public static class WindowBackground
    {
        /// <summary>Rebuild the background visual tree for <paramref name="host"/>.</summary>
        public static void Apply(Grid host, AppSettings settings)
        {
            if (host == null) { return; }
            host.Children.Clear();
            host.Background = null;
            foreach (UIElement child in host.Children) { }
            if (settings == null) { return; }

            host.Background = Application.Current != null
                ? (Brush)Application.Current.Resources["Brush.Surface"]
                : Brushes.White;

            string relative = settings.BackgroundImage;
            if (string.IsNullOrEmpty(relative) || settings.BackgroundMode == BackgroundMode.None) { return; }

            string path = null;
            try
            {
                ContainerSession session = AppState.SessionOrNull;
                if (session != null) { path = session.ResolveRelative(relative); }
                else if (Path.IsPathRooted(relative)) { path = relative; }
            }
            catch
            {
                path = null;
            }
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) { return; }

            BitmapSource bitmap;
            try
            {
                bitmap = ThemeManager.LoadBitmap(path, 2560);
            }
            catch
            {
                return;
            }

            string mode = string.IsNullOrEmpty(settings.BackgroundMode) ? BackgroundMode.Fill : settings.BackgroundMode;

            if (mode == BackgroundMode.Tile)
            {
                ImageBrush brush = new ImageBrush(bitmap);
                brush.TileMode = TileMode.Tile;
                brush.Stretch = Stretch.None;
                brush.ViewportUnits = BrushMappingMode.Absolute;
                brush.Viewport = new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight);
                host.Background = brush;
            }
            else
            {
                Image image = new Image();
                image.Source = bitmap;
                image.Stretch = StretchFor(mode);
                if (mode == BackgroundMode.Center)
                {
                    image.Stretch = Stretch.None;
                    image.HorizontalAlignment = HorizontalAlignment.Center;
                    image.VerticalAlignment = VerticalAlignment.Center;
                }
                else
                {
                    image.HorizontalAlignment = HorizontalAlignment.Stretch;
                    image.VerticalAlignment = VerticalAlignment.Stretch;
                }
                if (settings.BackgroundBlur > 0.1)
                {
                    image.Effect = new BlurEffect
                    {
                        Radius = settings.BackgroundBlur,
                        KernelType = KernelType.Gaussian,
                        RenderingBias = RenderingBias.Performance
                    };
                    // A blur bleeds transparency at the edges; slightly overdraw to hide it.
                    image.Margin = new Thickness(-settings.BackgroundBlur * 2);
                }
                host.Children.Add(image);
            }

            double scrim = settings.BackgroundScrim;
            if (scrim > 0.001)
            {
                Color tint = settings.BackgroundScrimLight ? Colors.White : Colors.Black;
                Border overlay = new Border();
                overlay.Background = new SolidColorBrush(Color.FromArgb(
                    (byte)Math.Round(Math.Min(1.0, scrim) * 255), tint.R, tint.G, tint.B));
                overlay.IsHitTestVisible = false;
                host.Children.Add(overlay);
            }
        }

        public static Stretch StretchFor(string mode)
        {
            switch (mode)
            {
                case BackgroundMode.Fit: return Stretch.Uniform;
                case BackgroundMode.Stretch: return Stretch.Fill;
                case BackgroundMode.Center: return Stretch.None;
                case BackgroundMode.Tile: return Stretch.None;
                default: return Stretch.UniformToFill;   // fill
            }
        }

        /// <summary>Copy a chosen image into the container so the profile stays portable.</summary>
        public static string ImportBackgroundImage(string sourcePath, out string error)
        {
            error = null;
            try
            {
                ContainerSession session = AppState.SessionOrNull;
                if (session == null)
                {
                    error = "请先打开或创建容器。";
                    return null;
                }
                string folder = Path.Combine(session.Root, ContainerLayout.BackgroundsFolder);
                Directory.CreateDirectory(folder);
                string name = AppPaths.SafeFileName(Path.GetFileName(sourcePath), "background");
                string target = Path.Combine(folder, name);
                int suffix = 2;
                while (File.Exists(target) && !SameFile(target, sourcePath))
                {
                    target = Path.Combine(folder,
                        Path.GetFileNameWithoutExtension(name) + "-" + suffix + Path.GetExtension(name));
                    suffix++;
                }
                if (!SameFile(target, sourcePath)) { File.Copy(sourcePath, target, true); }
                return session.ToRelative(target);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        private static bool SameFile(string a, string b)
        {
            try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        /// <summary>Remove the background image reference (the file itself is kept).</summary>
        public static void Clear(AppSettings settings)
        {
            if (settings == null) { return; }
            settings.BackgroundImage = "";
            settings.BackgroundMode = BackgroundMode.None;
            settings.BackgroundBlur = 0;
            settings.BackgroundScrim = 0;
        }
    }
}
