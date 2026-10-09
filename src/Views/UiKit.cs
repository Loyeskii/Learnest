using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Learnest.Data;
using Learnest.Services;

namespace Learnest.Views
{
    /// <summary>
    /// Small helper library so the views can build Material-ish UI in C# without repeating
    /// style lookups everywhere.  Every helper pulls its look from the shared theme resources,
    /// which keeps light/dark and accent switching automatic.
    /// </summary>
    internal static class UiKit
    {
        // ---------------------------------------------------------------- resources
        public static object Res(string key)
        {
            if (Application.Current == null) { return null; }
            return Application.Current.Resources[key];
        }

        public static Brush Brush(string key)
        {
            Brush brush = Res(key) as Brush;
            return brush ?? Brushes.Transparent;
        }

        public static Color UiColor(string key)
        {
            SolidColorBrush brush = Res(key) as SolidColorBrush;
            return brush != null ? brush.Color : Colors.Gray;
        }

        public static Style Style(string key)
        {
            return Res(key) as Style;
        }

        public static FontFamily UiFont
        {
            get
            {
                FontFamily family = Res("Font.Ui") as FontFamily;
                return family ?? new FontFamily("Microsoft YaHei UI, Segoe UI");
            }
        }

        public static FontFamily MonoFont
        {
            get
            {
                FontFamily family = Res("Font.Mono") as FontFamily;
                return family ?? new FontFamily("Consolas");
            }
        }

        private static System.Windows.Media.Imaging.BitmapFrame _appIcon;

        /// <summary>
        /// The application icon, loaded once from the embedded resource
        /// (assets\learnest.ico, linked into the assembly by the project file).  Used by the title bar
        /// and the setup wizard so the in-app mark matches the exe icon.
        /// Returns null when the resource is unavailable; callers fall back to a simple shape.
        /// </summary>
        public static System.Windows.Media.Imaging.BitmapSource AppIcon(double decodeSize)
        {
            try
            {
                if (_appIcon == null)
                {
                    System.Windows.Media.Imaging.BitmapImage image = new System.Windows.Media.Imaging.BitmapImage();
                    image.BeginInit();
                    image.UriSource = new Uri("pack://application:,,,/assets/learnest.ico", UriKind.Absolute);
                    image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    image.EndInit();
                    image.Freeze();
                    _appIcon = System.Windows.Media.Imaging.BitmapFrame.Create(image);
                    _appIcon.Freeze();
                }
                return _appIcon;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// A square application mark of the requested size: the real icon when it is available,
        /// otherwise a rounded accent tile with an "L", so the UI never shows an empty box.
        /// </summary>
        public static UIElement AppMark(double size)
        {
            System.Windows.Media.Imaging.BitmapSource icon = AppIcon(size);
            if (icon != null)
            {
                Image image = new Image();
                image.Source = icon;
                image.Width = size;
                image.Height = size;
                image.Stretch = Stretch.Uniform;
                image.SnapsToDevicePixels = true;
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                return image;
            }

            Border tile = new Border();
            tile.Width = size;
            tile.Height = size;
            tile.CornerRadius = new CornerRadius(size * 0.28);
            tile.Background = Brush("Brush.Primary");
            TextBlock letter = new TextBlock();
            letter.Text = "L";
            letter.FontFamily = UiFont;
            letter.FontWeight = FontWeights.Bold;
            letter.FontSize = size * 0.55;
            letter.Foreground = Brush("Brush.OnPrimary");
            letter.HorizontalAlignment = HorizontalAlignment.Center;
            letter.VerticalAlignment = VerticalAlignment.Center;
            tile.Child = letter;
            return tile;
        }

        // ---------------------------------------------------------------- text
        public static TextBlock Text(string value, string styleKey)
        {
            TextBlock block = new TextBlock();
            block.Text = value ?? "";
            Style style = Style(styleKey);
            if (style != null) { block.Style = style; }
            else { block.FontFamily = UiFont; block.FontSize = 13; }
            block.TextTrimming = TextTrimming.CharacterEllipsis;
            return block;
        }

        public static TextBlock Body(string value) { return Text(value, "Text.Body"); }
        public static TextBlock Caption(string value) { return Text(value, "Text.Caption"); }
        public static TextBlock Label(string value) { return Text(value, "Text.Label"); }
        public static TextBlock Title(string value) { return Text(value, "Text.Title"); }
        public static TextBlock Subtitle(string value) { return Text(value, "Text.Subtitle"); }
        public static TextBlock Muted(string value) { return Text(value, "Text.Muted"); }

        /// <summary>Section heading with an optional trailing element (usually a button).</summary>
        public static Grid SectionHeader(string title, UIElement trailing)
        {
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock heading = Text(title, "Text.Subtitle");
            heading.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(heading);
            if (trailing != null)
            {
                Grid.SetColumn(trailing, 1);
                grid.Children.Add(trailing);
            }
            grid.Margin = new Thickness(0, 0, 0, 10);
            return grid;
        }

        // ---------------------------------------------------------------- buttons
        public static Button Button(string content, string styleKey, RoutedEventHandler click)
        {
            Button button = new Button();
            button.Content = content;
            Style style = Style(styleKey);
            if (style != null) { button.Style = style; }
            if (click != null) { button.Click += click; }
            return button;
        }

        public static Button Primary(string content, RoutedEventHandler click)
        {
            return Button(content, "Button.Primary", click);
        }

        public static Button Tonal(string content, RoutedEventHandler click)
        {
            return Button(content, "Button.Tonal", click);
        }

        public static Button Outlined(string content, RoutedEventHandler click)
        {
            return Button(content, "Button.Outlined", click);
        }

        public static Button TextButton(string content, RoutedEventHandler click)
        {
            return Button(content, "Button.Text", click);
        }

        /// <summary>Non-toggle chip: a compact pill button used for suggestions and quick picks.</summary>
        public static Button Chip(string content, RoutedEventHandler click)
        {
            return Button(content, "Button.Chip.Static", click);
        }

        /// <summary>Icon button drawn with vector geometry (no icon font required).</summary>
        public static Button IconButton(string glyph, string tooltip, RoutedEventHandler click)
        {
            Button button = new Button();
            button.Style = Style("Button.Icon");
            button.Content = Icon(glyph, 18, Brush("Brush.OnSurfaceVariant"));
            if (!string.IsNullOrEmpty(tooltip))
            {
                ToolTipService.SetToolTip(button, tooltip);
                AutomationProperties.SetName(button, tooltip);
            }
            if (click != null) { button.Click += click; }
            return button;
        }

        public static System.Windows.Controls.Primitives.ToggleButton ToggleChip(string content, bool isChecked, RoutedEventHandler changed)
        {
            System.Windows.Controls.Primitives.ToggleButton toggle = new System.Windows.Controls.Primitives.ToggleButton();
            toggle.Content = content;
            toggle.Style = Style("Button.Chip");
            toggle.IsChecked = isChecked;
            if (changed != null)
            {
                toggle.Checked += changed;
                toggle.Unchecked += changed;
            }
            return toggle;
        }

        // ---------------------------------------------------------------- icons
        /// <summary>
        /// A small set of vector glyphs so the UI does not depend on an icon font.  Each entry is
        /// a path geometry in a 24x24 viewport.
        /// </summary>
        public static Path Icon(string name, double size, Brush fill)
        {
            Path path = new Path();
            path.Data = Geometry.Parse(IconGeometry(name));
            path.Stroke = fill;
            path.StrokeThickness = 1.9;
            path.StrokeStartLineCap = PenLineCap.Round;
            path.StrokeEndLineCap = PenLineCap.Round;
            path.StrokeLineJoin = PenLineJoin.Round;
            path.Fill = null;
            path.Stretch = Stretch.Uniform;
            path.Width = size;
            path.Height = size;
            path.SnapsToDevicePixels = true;
            return path;
        }

        public static string IconGeometry(string name)
        {
            switch (name)
            {
                case "dashboard": return "M4,13 L10,13 L10,4 L4,4 Z M14,20 L20,20 L20,11 L14,11 Z M4,20 L10,20 L10,17 L4,17 Z M14,7 L20,7 L20,4 L14,4 Z";
                case "tasks": return "M4,6 L7,9 L12,3 M4,18 L7,21 L12,15 M15,7 L21,7 M15,19 L21,19";
                case "timeline": return "M3,6 L21,6 M3,12 L21,12 M3,18 L21,18 M6,4 L6,8 M12,10 L12,14 M17,16 L17,20";
                case "exam": return "M6,3 L18,3 L18,21 L6,21 Z M9,8 L15,8 M9,12 L15,12 M9,16 L13,16";
                case "trophy": return "M7,4 L17,4 L17,9 A5,5 0 0 1 7,9 Z M12,14 L12,18 M8,21 L16,21 M9,18 L15,18";
                case "calendar": return "M4,6 L20,6 L20,21 L4,21 Z M4,10 L20,10 M8,3 L8,7 M16,3 L16,7";
                case "settings": return "M12,15.5 A3.5,3.5 0 1 0 12,8.5 A3.5,3.5 0 1 0 12,15.5 M19.4,15 A1.7,1.7 0 0 0 19.7,16.9 L19.8,17 A2,2 0 1 1 17,19.8 L16.9,19.7 A1.7,1.7 0 0 0 15,19.4 A1.7,1.7 0 0 0 14,21 L14,21.2 A2,2 0 1 1 10,21.2 L10,21 A1.7,1.7 0 0 0 9,19.4 A1.7,1.7 0 0 0 7.1,19.7 L7,19.8 A2,2 0 1 1 4.2,17 L4.3,16.9 A1.7,1.7 0 0 0 4.6,15 A1.7,1.7 0 0 0 3,14 L2.8,14 A2,2 0 1 1 2.8,10 L3,10 A1.7,1.7 0 0 0 4.6,9 A1.7,1.7 0 0 0 4.3,7.1 L4.2,7 A2,2 0 1 1 7,4.2 L7.1,4.3 A1.7,1.7 0 0 0 9,4.6 A1.7,1.7 0 0 0 10,3 L10,2.8 A2,2 0 1 1 14,2.8 L14,3 A1.7,1.7 0 0 0 15,4.6 A1.7,1.7 0 0 0 16.9,4.3 L17,4.2 A2,2 0 1 1 19.8,7 L19.7,7.1 A1.7,1.7 0 0 0 19.4,9 A1.7,1.7 0 0 0 21,10 L21.2,10 A2,2 0 1 1 21.2,14 L21,14 A1.7,1.7 0 0 0 19.4,15 Z";
                case "pin": return "M12,17 L12,22 M8,3 L16,3 L15,9 L18,12 L6,12 L9,9 Z";
                case "bell": return "M6,17 L6,11 A6,6 0 0 1 18,11 L18,17 L20,19 L4,19 Z M10,19 A2,2 0 0 0 14,19";
                case "plus": return "M12,5 L12,19 M5,12 L19,12";
                case "minus": return "M5,12 L19,12";
                case "close": return "M6,6 L18,18 M18,6 L6,18";
                case "check": return "M5,12 L10,17 L19,7";
                case "chevron_left": return "M14,6 L8,12 L14,18";
                case "chevron_right": return "M10,6 L16,12 L10,18";
                case "chevron_down": return "M6,9 L12,15 L18,9";
                case "chevron_up": return "M6,15 L12,9 L18,15";
                case "search": return "M11,4 A7,7 0 1 0 11,18 A7,7 0 1 0 11,4 M16,16 L21,21";
                case "edit": return "M4,20 L4,16 L16,4 L20,8 L8,20 Z M14,6 L18,10";
                case "trash": return "M5,7 L19,7 M9,7 L9,4 L15,4 L15,7 M7,7 L8,21 L16,21 L17,7 M11,11 L11,17 M13,11 L13,17";
                case "folder": return "M3,6 L10,6 L12,9 L21,9 L21,19 L3,19 Z";
                case "file": return "M6,3 L14,3 L18,7 L18,21 L6,21 Z M14,3 L14,7 L18,7";
                case "paperclip": return "M9,12 L15,6 A3,3 0 0 1 19,10 L11,18 A4,4 0 0 1 5,12 L13,4";
                case "note": return "M5,4 L19,4 L19,20 L5,20 Z M8,9 L16,9 M8,13 L16,13 M8,17 L12,17";
                case "markdown": return "M3,7 L21,7 L21,17 L3,17 Z M6,14 L6,10 L8,12 L10,10 L10,14 M15,10 L15,14 M13,12 L17,12";
                case "sort": return "M6,4 L6,20 M6,20 L3,17 M6,20 L9,17 M18,20 L18,4 M18,4 L15,7 M18,4 L21,7";
                case "filter": return "M3,5 L21,5 L14,13 L14,20 L10,18 L10,13 Z";
                case "backup": return "M12,4 L12,15 M8,11 L12,15 L16,11 M5,19 L19,19";
                case "import": return "M12,20 L12,9 M8,13 L12,9 L16,13 M5,4 L19,4";
                case "export": return "M12,4 L12,15 M8,8 L12,4 L16,8 M5,20 L19,20";
                case "image": return "M4,5 L20,5 L20,19 L4,19 Z M8,11 A1.6,1.6 0 1 0 8,8 A1.6,1.6 0 1 0 8,11 M5,17 L11,12 L15,16 L18,14 L20,15";
                case "palette": return "M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 M8,9 A1.2,1.2 0 1 0 8,6.6 A1.2,1.2 0 1 0 8,9 M15,8 A1.2,1.2 0 1 0 15,5.6 A1.2,1.2 0 1 0 15,8 M17,14 A1.2,1.2 0 1 0 17,11.6 A1.2,1.2 0 1 0 17,14";
                case "language": return "M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 M3,12 L21,12 M12,3 A14,14 0 0 1 12,21 A14,14 0 0 1 12,3";
                case "database": return "M4,6 C4,4.3 7.6,3 12,3 C16.4,3 20,4.3 20,6 C20,7.7 16.4,9 12,9 C7.6,9 4,7.7 4,6 Z M4,6 L4,18 C4,19.7 7.6,21 12,21 C16.4,21 20,19.7 20,18 L20,6 M4,12 C4,13.7 7.6,15 12,15 C16.4,15 20,13.7 20,12";
                case "folder_open": return "M3,6 L10,6 L12,9 L21,9 L21,19 L3,19 Z M3,12 L21,12";
                case "clock": return "M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 M12,7 L12,12 L16,14";
                case "flag": return "M6,3 L6,21 M6,4 L18,4 L15,9 L18,14 L6,14";
                case "star": return "M12,3 L15,9 L21,9.6 L16.5,14 L18,20 L12,17 L6,20 L7.5,14 L3,9.6 L9,9 Z";
                case "star_filled": return "M12,3 L15,9 L21,9.6 L16.5,14 L18,20 L12,17 L6,20 L7.5,14 L3,9.6 L9,9 Z";
                case "link": return "M9,15 L15,9 M10,6 L13,3 A4,4 0 0 1 19,9 L16,12 M14,18 L11,21 A4,4 0 0 1 5,15 L8,12";
                case "target": return "M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 M12,8 A4,4 0 1 0 12,16 A4,4 0 1 0 12,8 M12,11.4 A0.6,0.6 0 1 0 12,12.6 A0.6,0.6 0 1 0 12,11.4";
                case "team": return "M8,11 A3.2,3.2 0 1 0 8,4.6 A3.2,3.2 0 1 0 8,11 M16,11 A2.6,2.6 0 1 0 16,5.8 A2.6,2.6 0 1 0 16,11 M3,20 C3,16.5 5.2,14.4 8,14.4 C10.8,14.4 13,16.5 13,20 M15,20 C15,17 16.5,15.4 18.4,15.4 C20.3,15.4 21.5,17 21.5,20";
                case "sun": return "M12,7.5 A4.5,4.5 0 1 0 12,16.5 A4.5,4.5 0 1 0 12,7.5 M12,2 L12,4.5 M12,19.5 L12,22 M2,12 L4.5,12 M19.5,12 L22,12 M5,5 L6.8,6.8 M17.2,17.2 L19,19 M19,5 L17.2,6.8 M6.8,17.2 L5,19";
                case "moon": return "M20,14.5 A8.5,8.5 0 0 1 9.5,4 A8.5,8.5 0 1 0 20,14.5 Z";
                case "monitor": return "M3,5 L21,5 L21,17 L3,17 Z M9,21 L15,21 M12,17 L12,21";
                case "grid": return "M4,4 L10,4 L10,10 L4,10 Z M14,4 L20,4 L20,10 L14,10 Z M4,14 L10,14 L10,20 L4,20 Z M14,14 L20,14 L20,20 L14,20 Z";
                case "maximise": return "M4,4 L20,4 L20,20 L4,20 Z";
                case "restore": return "M8,3 L21,3 L21,16 M3,8 L16,8 L16,21 L3,21 Z";
                case "list": return "M8,6 L21,6 M8,12 L21,12 M8,18 L21,18 M4,6 L4.01,6 M4,12 L4.01,12 M4,18 L4.01,18";
                case "drag": return "M4,9 L20,9 M4,15 L20,15";
                case "alert": return "M12,3 L22,20 L2,20 Z M12,9 L12,14 M12,17 L12,17.5";
                case "info": return "M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 M12,8 L12,8.5 M12,11 L12,16";
                case "refresh": return "M20,12 A8,8 0 1 1 16,5.2 M20,4 L20,9 L15,9";
                case "save": return "M5,3 L16,3 L20,7 L20,21 L5,21 Z M8,3 L8,9 L16,9 L16,3 M8,21 L8,14 L16,14 L16,21";
                case "eye": return "M2,12 C5,6 19,6 22,12 C19,18 5,18 2,12 M12,9.5 A2.5,2.5 0 1 0 12,14.5 A2.5,2.5 0 1 0 12,9.5";
                case "play": return "M7,4 L19,12 L7,20 Z";
                case "trophy_outline": return "M8,4 L16,4 L16,10 A4,4 0 0 1 8,10 Z M8,6 L5,6 L5,9 A3,3 0 0 0 8,12 M16,6 L19,6 L19,9 A3,3 0 0 1 16,12 M12,14 L12,18 M9,20 L15,20";
                default: return "M4,4 L20,4 L20,20 L4,20 Z";
            }
        }

        // ---------------------------------------------------------------- containers
        public static Border Card(UIElement child, bool outlined)
        {
            Border border = new Border();
            border.Style = Style(outlined ? "Card.Outlined" : "Card");
            border.Child = child;
            return border;
        }

        public static Border Panel(UIElement child)
        {
            Border border = new Border();
            border.Style = Style("Panel");
            border.Child = child;
            return border;
        }

        public static Border Badge(string text, Brush background, Brush foreground)
        {
            Border border = new Border();
            border.Style = Style("Badge");
            if (background != null) { border.Background = background; }
            TextBlock block = Text(text, "Badge.Text");
            if (foreground != null) { block.Foreground = foreground; }
            border.Child = block;
            return border;
        }

        public static Border Dot(string hexColor, double size)
        {
            Border dot = new Border();
            dot.Width = size;
            dot.Height = size;
            dot.CornerRadius = new CornerRadius(size / 2);
            dot.Background = new SolidColorBrush(Theming.ColorUtil.Parse(hexColor, Colors.Gray));
            dot.VerticalAlignment = VerticalAlignment.Center;
            return dot;
        }

        public static Separator Divider()
        {
            return new Separator();
        }

        /// <summary>Empty state block with an icon, title and hint.</summary>
        public static StackPanel Empty(string icon, string title, string hint)
        {
            StackPanel panel = new StackPanel();
            panel.HorizontalAlignment = HorizontalAlignment.Center;
            panel.VerticalAlignment = VerticalAlignment.Center;
            panel.Margin = new Thickness(0, 40, 0, 40);
            Path glyph = Icon(icon, 42, Brush("Brush.OnSurfaceFaint"));
            glyph.HorizontalAlignment = HorizontalAlignment.Center;
            glyph.Opacity = 0.7;
            panel.Children.Add(glyph);
            TextBlock heading = Text(title, "Text.Subtitle");
            heading.HorizontalAlignment = HorizontalAlignment.Center;
            heading.Margin = new Thickness(0, 14, 0, 4);
            heading.Foreground = Brush("Brush.OnSurfaceVariant");
            panel.Children.Add(heading);
            if (!string.IsNullOrEmpty(hint))
            {
                TextBlock note = Text(hint, "Text.Caption");
                note.HorizontalAlignment = HorizontalAlignment.Center;
                note.TextWrapping = TextWrapping.Wrap;
                note.MaxWidth = 420;
                note.TextAlignment = TextAlignment.Center;
                panel.Children.Add(note);
            }
            return panel;
        }

        /// <summary>A titled field group used by every editor dialog.</summary>
        public static StackPanel Field(string label, UIElement editor, string hint)
        {
            StackPanel stack = new StackPanel();
            stack.Margin = new Thickness(0, 0, 0, 12);
            if (!string.IsNullOrEmpty(label))
            {
                TextBlock caption = Text(label, "Text.Label");
                caption.Margin = new Thickness(2, 0, 0, 5);
                stack.Children.Add(caption);
            }
            stack.Children.Add(editor);
            if (!string.IsNullOrEmpty(hint))
            {
                TextBlock note = Text(hint, "Text.Caption");
                note.Margin = new Thickness(2, 4, 0, 0);
                note.TextWrapping = TextWrapping.Wrap;
                stack.Children.Add(note);
            }
            return stack;
        }

        public static TextBox Input(string value, string watermark)
        {
            TextBox box = new TextBox();
            box.Style = Style("TextBox.Base");
            box.Text = value ?? "";
            if (watermark != null) { WatermarkService.Set(box, watermark); }
            return box;
        }

        /// <summary>Multi line input used for short notes such as a list of links.</summary>
        public static TextBox MultiLineInput(string value, double height)
        {
            TextBox box = new TextBox();
            box.Style = Style("TextBox.Base");
            box.Text = value ?? "";
            box.AcceptsReturn = true;
            box.TextWrapping = TextWrapping.Wrap;
            box.VerticalContentAlignment = VerticalAlignment.Top;
            box.MinHeight = height;
            box.Height = height;
            box.FontFamily = UiFont;
            return box;
        }

        /// <summary>Segmented control: a row of mutually exclusive chips.</summary>
        public static StackPanel Segmented(string[] labels, string[] values, string selected, Action<string> onSelect)
        {
            StackPanel row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            for (int i = 0; i < labels.Length; i++)
            {
                string value = values[i];
                System.Windows.Controls.Primitives.ToggleButton chip = new System.Windows.Controls.Primitives.ToggleButton();
                chip.Content = labels[i];
                chip.Style = Style("Button.Chip");
                chip.IsChecked = value == selected;
                chip.Margin = new Thickness(0, 0, 6, 0);
                chip.Tag = value;
                chip.Checked += delegate(object sender, RoutedEventArgs e)
                {
                    System.Windows.Controls.Primitives.ToggleButton source = (System.Windows.Controls.Primitives.ToggleButton)sender;
                    // Radio behaviour inside the panel.
                    Panel parent = source.Parent as Panel;
                    if (parent != null)
                    {
                        for (int j = 0; j < parent.Children.Count; j++)
                        {
                            System.Windows.Controls.Primitives.ToggleButton other = parent.Children[j] as System.Windows.Controls.Primitives.ToggleButton;
                            if (other != null && !ReferenceEquals(other, source)) { other.IsChecked = false; }
                        }
                    }
                    if (onSelect != null) { onSelect((string)source.Tag); }
                };
                row.Children.Add(chip);
            }
            return row;
        }

        // ---------------------------------------------------------------- lists
        public static ComboBox Combo(IEnumerable<KeyValuePair<string, string>> items, string selectedKey)
        {
            ComboBox combo = new ComboBox();
            int selectedIndex = 0;
            int index = 0;
            foreach (KeyValuePair<string, string> item in items)
            {
                ComboBoxItem entry = new ComboBoxItem();
                entry.Content = item.Value;
                entry.Tag = item.Key;
                combo.Items.Add(entry);
                if (string.Equals(item.Key, selectedKey, StringComparison.Ordinal))
                {
                    selectedIndex = index;
                }
                index++;
            }
            if (combo.Items.Count > 0) { combo.SelectedIndex = selectedIndex; }
            return combo;
        }

        public static string SelectedKey(ComboBox combo)
        {
            ComboBoxItem item = combo.SelectedItem as ComboBoxItem;
            if (item == null) { return null; }
            return item.Tag as string;
        }

        public static void SelectKey(ComboBox combo, string key)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                ComboBoxItem item = combo.Items[i] as ComboBoxItem;
                if (item != null && string.Equals(item.Tag as string, key, StringComparison.Ordinal))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        // ---------------------------------------------------------------- dialogs
        /// <summary>
        /// When set, ShowDialog builds the dialog, records it and returns immediately instead of
        /// showing it.  This lets the headless checks construct every editor - which is where
        /// style/target type mismatches and other construction time failures live.
        /// </summary>
        internal static bool CaptureDialogs;

        /// <summary>Dialogs captured while <see cref="CaptureDialogs"/> is set.</summary>
        internal static readonly List<Window> CapturedDialogs = new List<Window>();

        /// <summary>Shelved dialog window with a title bar, content and button row.</summary>
        public static bool ShowDialog(Window owner, string title, UIElement content, string primaryText,
            string secondaryText, Func<bool> onPrimary, bool danger)
        {
            Window dialog = new Window();
            dialog.Title = title;
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialog.SizeToContent = SizeToContent.Height;
            dialog.Width = 520;
            dialog.MinWidth = 420;
            dialog.MaxWidth = 900;
            dialog.ResizeMode = ResizeMode.CanResizeWithGrip;
            dialog.ShowInTaskbar = false;
            dialog.Background = Brush("Brush.Surface");
            dialog.FontFamily = UiFont;
            if (Application.Current != null)
            {
                Style windowStyle = Application.Current.TryFindResource("LearnestWindow") as Style;
                if (windowStyle != null) { dialog.Style = windowStyle; }
            }
            Grid root = new Grid();
            root.Margin = new Thickness(22, 18, 22, 18);
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock heading = Text(title, "Text.Title");
            heading.Margin = new Thickness(0, 0, 0, 14);
            root.Children.Add(heading);

            ScrollViewer scroller = new ScrollViewer();
            scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroller.MaxHeight = 560;
            scroller.Content = content;
            Grid.SetRow(scroller, 1);
            root.Children.Add(scroller);

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Margin = new Thickness(0, 18, 0, 0);

            bool result = false;
            if (!string.IsNullOrEmpty(secondaryText))
            {
                Button cancel = Outlined(secondaryText, delegate { dialog.DialogResult = false; });
                buttons.Children.Add(cancel);
            }
            Button primary = Button(primaryText, danger ? "Button.Danger" : "Button.Primary", delegate
            {
                if (onPrimary == null || onPrimary())
                {
                    result = true;
                    dialog.DialogResult = true;
                }
            });
            primary.Margin = new Thickness(8, 0, 0, 0);
            primary.MinWidth = 92;
            buttons.Children.Add(primary);
            Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);

            dialog.Content = root;
            dialog.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) { dialog.DialogResult = false; }
            };

            // Headless mode: build the dialog and measure it (which applies every style), record it,
            // and return without showing it.  This is how the checks cover the editors - the place
            // where target type mismatches and other construction failures actually live.
            if (CaptureDialogs)
            {
                try
                {
                    dialog.Measure(new Size(560, 900));
                    dialog.Arrange(new Rect(0, 0, 560, 900));
                    dialog.UpdateLayout();
                }
                catch (Exception ex)
                {
                    CapturedDialogs.Add(dialog);
                    throw new InvalidOperationException("dialog '" + title + "' failed to build: " + ex.Message, ex);
                }
                CapturedDialogs.Add(dialog);
                return false;
            }

            bool? shown = dialog.ShowDialog();
            return shown == true && result;
        }

        public static void Message(Window owner, string title, string text)
        {
            MessageBox.Show(owner, text, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static bool Confirm(Window owner, string title, string text)
        {
            return MessageBox.Show(owner, text, title, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;
        }

        // ---------------------------------------------------------------- misc
        public static string RelativeTime(DateTime when)
        {
            TimeSpan span = DateTime.Now - when;
            if (span.TotalSeconds < 60) { return Loc.T("common.today") + " · " + when.ToString("HH:mm"); }
            if (span.TotalMinutes < 60) { return ((int)span.TotalMinutes) + " min"; }
            if (span.TotalHours < 24) { return ((int)span.TotalHours) + " h"; }
            if (span.TotalDays < 30) { return ((int)span.TotalDays) + " d"; }
            return when.ToString("yyyy-MM-dd");
        }

        /// <summary>Colour with alpha applied, for tints and hover layers.</summary>
        public static SolidColorBrush Alpha(Color color, double alpha)
        {
            byte a = (byte)Math.Round(Math.Max(0, Math.Min(1, alpha)) * 255);
            SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(a, color.R, color.G, color.B));
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Brush for the border of an item that has entered its reminder window.
        ///
        /// This is the reminder signal now: a static red gradient border instead of a flashing
        /// animation.  It is derived from the theme's error colour so it stays in step with the
        /// palette, and the gradient runs from the error colour to a softened tint of it so the
        /// border reads as "urgent" without becoming a solid red alarm.
        /// </summary>
        public static System.Windows.Media.LinearGradientBrush AlertBorderBrush()
        {
            Color error = UiColor("Brush.Error");
            Color soft = Theming.ColorUtil.Blend(error, Colors.White, 0.45);

            System.Windows.Media.LinearGradientBrush brush = new System.Windows.Media.LinearGradientBrush();
            brush.StartPoint = new Point(0, 0);
            brush.EndPoint = new Point(1, 1);
            brush.GradientStops.Add(new GradientStop(error, 0.0));
            brush.GradientStops.Add(new GradientStop(soft, 0.5));
            brush.GradientStops.Add(new GradientStop(error, 1.0));
            brush.Freeze();
            return brush;
        }

        /// <summary>Soft tint used as the background of a reminding item.</summary>
        public static SolidColorBrush AlertTintBrush()
        {
            return Alpha(UiColor("Brush.Error"), 0.10);
        }

        /// <summary>Uniform margin helper.</summary>
        public static Thickness Insets(double all)
        {
            return new Thickness(all);
        }
    }
}
