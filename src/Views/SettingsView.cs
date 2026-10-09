using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Learnest.Data;
using Learnest.Services;
using Learnest.Theming;

namespace Learnest.Views
{
    /// <summary>
    /// Settings.  Everything here is stored inside the container (theme, accent, language,
    /// background image, widget and alert preferences, window layout), so a container keeps its
    /// look when it is copied to another machine.  The only exception is the language fallback in
    /// %LocalAppData%\Learnest\appsettings.json, which exists so the setup wizard can be localised
    /// before a container is chosen.
    /// </summary>
    internal sealed class SettingsView : UserControl, IRefreshable
    {
        private readonly IShell _shell;
        private readonly StackPanel _root;
        private readonly TextBlock _hint;

        public SettingsView(IShell shell)
        {
            _shell = shell;
            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            StackPanel header = new StackPanel();
            header.Margin = new Thickness(22, 16, 22, 6);
            header.Children.Add(UiKit.Text(Loc.T("settings.title"), "Text.Display"));
            _hint = UiKit.Muted("");
            _hint.Margin = new Thickness(0, 2, 0, 0);
            header.Children.Add(_hint);
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            ScrollViewer scroller = new ScrollViewer();
            scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroller.Padding = new Thickness(22, 8, 22, 20);
            _root = new StackPanel();
            scroller.Content = _root;
            Grid.SetRow(scroller, 1);
            root.Children.Add(scroller);

            Content = root;
            Refresh();
        }

        public void Refresh()
        {
            _root.Children.Clear();
            AppSettings settings = AppState.Settings;
            if (settings == null) { return; }
            _hint.Text = AppPaths.AppName + " " + AppPaths.AppVersionString() + " · " + AppPaths.Tagline;

            _root.Children.Add(AppearanceCard(settings));
            _root.Children.Add(BackgroundCard(settings));
            _root.Children.Add(LanguageCard(settings));
            _root.Children.Add(WidgetCard(settings));
            _root.Children.Add(AlertCard(settings));
            _root.Children.Add(DataCard(settings));
            _root.Children.Add(BehaviourCard(settings));
            _root.Children.Add(AboutCard(settings));
        }

        private Border Section(string title, params UIElement[] children)
        {
            StackPanel stack = new StackPanel();
            stack.Children.Add(UiKit.SectionHeader(title, null));
            for (int i = 0; i < children.Length; i++) { stack.Children.Add(children[i]); }
            Border card = UiKit.Card(stack, false);
            card.Margin = new Thickness(0, 0, 0, 12);
            return card;
        }

        private static string ThemeLabel(string mode)
        {
            if (mode == ThemeMode.Light) { return Loc.T("settings.theme_light"); }
            if (mode == ThemeMode.Dark) { return Loc.T("settings.theme_dark"); }
            return Loc.T("settings.theme_system");
        }

        private UIElement AppearanceCard(AppSettings settings)
        {
            StackPanel themeRow = new StackPanel();
            themeRow.Orientation = Orientation.Horizontal;
            string[] themeLabels = new string[] { Loc.T("settings.theme_system"), Loc.T("settings.theme_light"), Loc.T("settings.theme_dark") };
            string[] themeValues = new string[] { ThemeMode.System, ThemeMode.Light, ThemeMode.Dark };
            themeRow.Children.Add(UiKit.Segmented(themeLabels, themeValues, settings.Theme, delegate(string value)
            {
                settings.Theme = value;
                AppState.Session.SaveSettings();
                ThemeManager.Apply(settings.Theme, settings.AccentColor, true);
                Refresh();
            }));

            // Accent swatches + auto extraction from the background image.
            WrapPanel accents = new WrapPanel();
            string[] presets = new string[]
            {
                "#2563EB", "#7C3AED", "#DB2777", "#DC2626", "#EA580C", "#CA8A04",
                "#16A34A", "#0D9488", "#0891B2", "#4F46E5", "#64748B", "#B45309"
            };
            for (int i = 0; i < presets.Length; i++)
            {
                string hex = presets[i];
                Border chip = new Border();
                chip.Width = 30;
                chip.Height = 30;
                chip.CornerRadius = new CornerRadius(9);
                chip.Margin = new Thickness(0, 0, 8, 8);
                chip.Cursor = System.Windows.Input.Cursors.Hand;
                chip.Background = new SolidColorBrush(ColorUtil.Parse(hex, Colors.RoyalBlue));
                chip.BorderThickness = new Thickness(2);
                bool selected = string.Equals(settings.AccentColor, hex, StringComparison.OrdinalIgnoreCase);
                chip.BorderBrush = selected ? UiKit.Brush("Brush.OnSurface") : Brushes.Transparent;
                chip.Tag = hex;
                chip.MouseLeftButtonUp += delegate(object sender, System.Windows.Input.MouseButtonEventArgs e)
                {
                    settings.AccentColor = (string)((Border)sender).Tag;
                    settings.AutoExtractAccent = false;
                    AppState.Session.SaveSettings();
                    ThemeManager.Apply(settings.Theme, settings.AccentColor, true);
                    Refresh();
                };
                ToolTipService.SetToolTip(chip, hex);
                accents.Children.Add(chip);
            }

            Button extract = UiKit.Outlined(Loc.T("settings.accent_extract"), delegate
            {
                if (string.IsNullOrEmpty(settings.BackgroundImage))
                {
                    MessageBox.Show(_shell.Window, Loc.T("settings.background_choose"), Loc.T("settings.accent"),
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                Color? accent = ThemeManager.ExtractAccentFromImage(settings.BackgroundImage);
                if (accent.HasValue)
                {
                    settings.AccentColor = ColorUtil.ToHex(accent.Value);
                    settings.AutoExtractAccent = true;
                    AppState.Session.SaveSettings();
                    ThemeManager.Apply(settings.Theme, settings.AccentColor, true);
                    _shell.SetStatus(Loc.T("settings.accent_extracted") + " " + settings.AccentColor);
                    Refresh();
                }
                else
                {
                    MessageBox.Show(_shell.Window, Loc.T("settings.accent_failed"), Loc.T("settings.accent"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            });
            extract.Margin = new Thickness(0, 2, 0, 0);

            CheckBox autoAccent = new CheckBox();
            autoAccent.Content = Loc.T("settings.accent_auto");
            autoAccent.IsChecked = settings.AutoExtractAccent;
            autoAccent.Margin = new Thickness(0, 8, 0, 0);
            autoAccent.Click += delegate(object sender, RoutedEventArgs e)
            {
                settings.AutoExtractAccent = ((CheckBox)sender).IsChecked == true;
                AppState.Session.SaveSettings();
            };

            CheckBox compact = new CheckBox();
            compact.Content = Loc.T("settings.compact");
            compact.IsChecked = settings.CompactDensity;
            compact.Click += delegate(object sender, RoutedEventArgs e)
            {
                settings.CompactDensity = ((CheckBox)sender).IsChecked == true;
                AppState.Session.SaveSettings();
            };

            return Section(Loc.T("settings.appearance"),
                UiKit.Field(Loc.T("settings.theme"), themeRow, null),
                UiKit.Field(Loc.T("settings.accent"), accents, null),
                extract,
                autoAccent,
                compact);
        }

        private UIElement BackgroundCard(AppSettings settings)
        {
            StackPanel current = new StackPanel();
            current.Orientation = Orientation.Horizontal;
            if (string.IsNullOrEmpty(settings.BackgroundImage))
            {
                current.Children.Add(UiKit.Caption(Loc.T("settings.background_clear")));
            }
            else
            {
                current.Children.Add(UiKit.Icon("image", 14, UiKit.Brush("Brush.OnSurfaceVariant")));
                TextBlock path = UiKit.Caption(settings.BackgroundImage);
                path.Margin = new Thickness(6, 0, 0, 0);
                path.TextTrimming = TextTrimming.CharacterEllipsis;
                current.Children.Add(path);
            }

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.Children.Add(UiKit.Tonal(Loc.T("settings.background_choose"), delegate
            {
                string file = Dialogs.PickImage(_shell.Window);
                if (string.IsNullOrEmpty(file)) { return; }
                string error;
                string relative = WindowBackground.ImportBackgroundImage(file, out error);
                if (relative == null)
                {
                    MessageBox.Show(_shell.Window, error ?? Loc.T("error.attachment_copy", file), Loc.T("settings.background"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                settings.BackgroundImage = relative;
                if (settings.BackgroundMode == BackgroundMode.None) { settings.BackgroundMode = BackgroundMode.Fill; }
                if (settings.BackgroundScrim <= 0) { settings.BackgroundScrim = 0.35; }
                AppState.Session.SaveSettings();

                if (settings.AutoExtractAccent)
                {
                    Color? accent = ThemeManager.ExtractAccentFromImage(relative);
                    if (accent.HasValue)
                    {
                        settings.AccentColor = ColorUtil.ToHex(accent.Value);
                        _shell.SetStatus(Loc.T("settings.accent_extracted") + " " + settings.AccentColor);
                    }
                }
                AppState.Session.SaveSettings();
                ThemeManager.Apply(settings.Theme, settings.AccentColor, true);
                Refresh();
            }));
            Button clear = UiKit.Outlined(Loc.T("settings.background_clear"), delegate
            {
                WindowBackground.Clear(settings);
                AppState.Session.SaveSettings();
                ThemeManager.Apply(settings.Theme, settings.AccentColor, true);
                Refresh();
            });
            clear.Margin = new Thickness(8, 0, 0, 0);
            buttons.Children.Add(clear);

            ComboBox modeCombo = UiKit.Combo(new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>(BackgroundMode.Fill, Loc.T("settings.mode_fill")),
                new KeyValuePair<string, string>(BackgroundMode.Fit, Loc.T("settings.mode_fit")),
                new KeyValuePair<string, string>(BackgroundMode.Stretch, Loc.T("settings.mode_stretch")),
                new KeyValuePair<string, string>(BackgroundMode.Tile, Loc.T("settings.mode_tile")),
                new KeyValuePair<string, string>(BackgroundMode.Center, Loc.T("settings.mode_center")),
                new KeyValuePair<string, string>(BackgroundMode.None, Loc.T("settings.mode_none"))
            }, settings.BackgroundMode);
            modeCombo.SelectionChanged += delegate
            {
                settings.BackgroundMode = UiKit.SelectedKey(modeCombo) ?? BackgroundMode.Fill;
                AppState.Session.SaveSettings();
                ThemeManager.Apply(settings.Theme, settings.AccentColor, true);
            };

            Slider blur = new Slider();
            blur.Minimum = 0;
            blur.Maximum = 40;
            blur.Value = settings.BackgroundBlur;
            TextBlock blurLabel = UiKit.Caption(settings.BackgroundBlur.ToString("0") + " px");
            blur.ValueChanged += delegate
            {
                settings.BackgroundBlur = blur.Value;
                blurLabel.Text = blur.Value.ToString("0") + " px";
            };
            blur.LostMouseCapture += delegate
            {
                AppState.Session.SaveSettings();
                ThemeManager.Apply(settings.Theme, settings.AccentColor, true);
            };
            blur.PreviewMouseUp += delegate
            {
                AppState.Session.SaveSettings();
                ThemeManager.Apply(settings.Theme, settings.AccentColor, true);
            };
            StackPanel blurField = new StackPanel();
            blurField.Children.Add(blur);
            blurField.Children.Add(blurLabel);

            Slider scrim = new Slider();
            scrim.Minimum = 0;
            scrim.Maximum = 1;
            scrim.Value = settings.BackgroundScrim;
            TextBlock scrimLabel = UiKit.Caption((settings.BackgroundScrim * 100).ToString("0") + " %");
            scrim.ValueChanged += delegate
            {
                settings.BackgroundScrim = scrim.Value;
                scrimLabel.Text = (scrim.Value * 100).ToString("0") + " %";
            };
            scrim.PreviewMouseUp += delegate
            {
                AppState.Session.SaveSettings();
                ThemeManager.Apply(settings.Theme, settings.AccentColor, true);
            };
            StackPanel scrimField = new StackPanel();
            scrimField.Children.Add(scrim);
            scrimField.Children.Add(scrimLabel);

            CheckBox lightScrim = new CheckBox();
            lightScrim.Content = Loc.T("settings.scrim_light");
            lightScrim.IsChecked = settings.BackgroundScrimLight;
            lightScrim.Click += delegate(object sender, RoutedEventArgs e)
            {
                settings.BackgroundScrimLight = ((CheckBox)sender).IsChecked == true;
                AppState.Session.SaveSettings();
                ThemeManager.Apply(settings.Theme, settings.AccentColor, true);
            };

            return Section(Loc.T("settings.background"),
                UiKit.Field(Loc.T("settings.background"), current, null),
                buttons,
                UiKit.Field(Loc.T("settings.background_mode"), modeCombo, null),
                UiKit.Field(Loc.T("settings.blur"), blurField, null),
                UiKit.Field(Loc.T("settings.scrim"), scrimField, null),
                lightScrim);
        }

        private UIElement LanguageCard(AppSettings settings)
        {
            List<KeyValuePair<string, string>> choices = new List<KeyValuePair<string, string>>();
            choices.Add(new KeyValuePair<string, string>("system", Loc.T("settings.language_system")));
            List<LanguagePack> packs = Loc.Available;
            for (int i = 0; i < packs.Count; i++)
            {
                choices.Add(new KeyValuePair<string, string>(packs[i].Code, packs[i].DisplayName));
            }
            ComboBox combo = UiKit.Combo(choices, string.IsNullOrEmpty(settings.Language) ? "system" : settings.Language);
            combo.SelectionChanged += delegate
            {
                settings.Language = UiKit.SelectedKey(combo) ?? "system";
                AppState.Session.SaveSettings();
                Loc.Apply(settings.Language);
                _shell.RefreshContent();
            };

            Button import = UiKit.Tonal(Loc.T("settings.language_import"), delegate
            {
                string file = Dialogs.PickLanguagePack(_shell.Window);
                if (string.IsNullOrEmpty(file)) { return; }
                string error;
                LanguagePack pack = Loc.Import(file, out error);
                if (pack == null)
                {
                    MessageBox.Show(_shell.Window, error ?? Loc.T("settings.language_import_failed"),
                        Loc.T("settings.language"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                settings.Language = pack.Code;
                AppState.Session.SaveSettings();
                Loc.Apply(pack.Code);
                _shell.SetStatus(Loc.T("settings.language_imported", pack.DisplayName));
                Refresh();
            });

            Button reload = UiKit.Outlined(Loc.T("settings.language_reload"), delegate
            {
                Loc.Rescan();
                Loc.Apply(settings.Language);
                Refresh();
            });
            reload.Margin = new Thickness(8, 0, 0, 0);

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.Children.Add(import);
            buttons.Children.Add(reload);

            StackPanel stack = new StackPanel();
            stack.Children.Add(UiKit.Field(Loc.T("settings.language"), combo, null));
            stack.Children.Add(buttons);
            stack.Children.Add(UiKit.Caption(Loc.LanguageFolder()));
            return Section(Loc.T("settings.language"), stack);
        }

        private UIElement WidgetCard(AppSettings settings)
        {
            CheckBox topmost = new CheckBox();
            topmost.Content = Loc.T("widget.always_on_top");
            topmost.IsChecked = settings.WidgetTopmost;
            topmost.Click += delegate(object sender, RoutedEventArgs e)
            {
                settings.WidgetTopmost = ((CheckBox)sender).IsChecked == true;
                AppState.Session.SaveSettings();
                if (WidgetWindow.Current != null) { WidgetWindow.Current.ApplyTopmost(); }
            };

            CheckBox showCompleted = new CheckBox();
            showCompleted.Content = Loc.T("widget.show_completed");
            showCompleted.IsChecked = settings.WidgetShowCompleted;
            showCompleted.Click += delegate(object sender, RoutedEventArgs e)
            {
                settings.WidgetShowCompleted = ((CheckBox)sender).IsChecked == true;
                AppState.Session.SaveSettings();
                if (WidgetWindow.Current != null) { WidgetWindow.Current.Reload(); }
            };

            CheckBox toolbar = new CheckBox();
            toolbar.Content = Loc.T("widget.show_toolbar");
            toolbar.IsChecked = settings.WidgetShowToolbar;
            toolbar.Click += delegate(object sender, RoutedEventArgs e)
            {
                settings.WidgetShowToolbar = ((CheckBox)sender).IsChecked == true;
                AppState.Session.SaveSettings();
                if (WidgetWindow.Current != null) { WidgetWindow.Current.Reload(); }
            };

            ComboBox filterCombo = UiKit.Combo(new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("upcoming", Loc.T("widget.filter_upcoming")),
                new KeyValuePair<string, string>("today", Loc.T("widget.filter_today")),
                new KeyValuePair<string, string>("week", Loc.T("widget.filter_week")),
                new KeyValuePair<string, string>("all", Loc.T("widget.filter_all"))
            }, settings.WidgetFilter);
            filterCombo.SelectionChanged += delegate
            {
                settings.WidgetFilter = UiKit.SelectedKey(filterCombo) ?? "upcoming";
                AppState.Session.SaveSettings();
                if (WidgetWindow.Current != null) { WidgetWindow.Current.Reload(); }
            };

            ComboBox sortCombo = UiKit.Combo(new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("dueAsc", Loc.T("tasks.sort_due_asc")),
                new KeyValuePair<string, string>("dueDesc", Loc.T("tasks.sort_due_desc")),
                new KeyValuePair<string, string>("modifiedDesc", Loc.T("tasks.sort_modified_desc")),
                new KeyValuePair<string, string>("modifiedAsc", Loc.T("tasks.sort_modified_asc")),
                new KeyValuePair<string, string>("createdDesc", Loc.T("tasks.sort_created_desc")),
                new KeyValuePair<string, string>("createdAsc", Loc.T("tasks.sort_created_asc"))
            }, settings.WidgetSort);
            sortCombo.SelectionChanged += delegate
            {
                settings.WidgetSort = UiKit.SelectedKey(sortCombo) ?? "dueAsc";
                AppState.Session.SaveSettings();
                if (WidgetWindow.Current != null) { WidgetWindow.Current.Reload(); }
            };

            Slider opacity = new Slider();
            opacity.Minimum = 0.4;
            opacity.Maximum = 1;
            opacity.Value = settings.WidgetOpacity;
            TextBlock opacityLabel = UiKit.Caption((settings.WidgetOpacity * 100).ToString("0") + " %");
            opacity.ValueChanged += delegate
            {
                settings.WidgetOpacity = opacity.Value;
                opacityLabel.Text = (opacity.Value * 100).ToString("0") + " %";
                if (WidgetWindow.Current != null) { WidgetWindow.Current.ApplyOpacity(); }
            };
            opacity.PreviewMouseUp += delegate { AppState.Session.SaveSettings(); };
            StackPanel opacityField = new StackPanel();
            opacityField.Children.Add(opacity);
            opacityField.Children.Add(opacityLabel);

            Button toggle = UiKit.Primary(WidgetWindow.Current == null ? Loc.T("widget.pin") : Loc.T("widget.unpin"),
                delegate
                {
                    _shell.ShowWidget(WidgetWindow.Current == null);
                    Refresh();
                });

            StackPanel stack = new StackPanel();
            stack.Children.Add(toggle);
            stack.Children.Add(UiKit.Field(Loc.T("widget.filter"), filterCombo, null));
            stack.Children.Add(UiKit.Field(Loc.T("widget.sort"), sortCombo, null));
            stack.Children.Add(UiKit.Field(Loc.T("widget.opacity"), opacityField, null));
            stack.Children.Add(topmost);
            stack.Children.Add(showCompleted);
            stack.Children.Add(toolbar);
            return Section(Loc.T("nav.widget"), stack);
        }

        private UIElement AlertCard(AppSettings settings)
        {
            ComboBox leadCombo = UiKit.Combo(new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("12", "12 " + Loc.T("calendar.hour")),
                new KeyValuePair<string, string>("24", "24 " + Loc.T("calendar.hour")),
                new KeyValuePair<string, string>("48", "48 " + Loc.T("calendar.hour")),
                new KeyValuePair<string, string>("72", "72 " + Loc.T("calendar.hour")),
                new KeyValuePair<string, string>("168", "7 " + Loc.T("timeline.day"))
            }, settings.AlertLeadHours.ToString());
            leadCombo.SelectionChanged += delegate
            {
                int hours;
                if (int.TryParse(UiKit.SelectedKey(leadCombo), out hours)) { settings.AlertLeadHours = hours; }
                AppState.Session.SaveSettings();
            };

            ComboBox intervalCombo = UiKit.Combo(new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("300", "0.3 s"),
                new KeyValuePair<string, string>("500", "0.5 s"),
                new KeyValuePair<string, string>("700", "0.7 s"),
                new KeyValuePair<string, string>("1000", "1 s")
            }, settings.AlertFlashIntervalMs.ToString());
            intervalCombo.SelectionChanged += delegate
            {
                int milliseconds;
                if (int.TryParse(UiKit.SelectedKey(intervalCombo), out milliseconds))
                {
                    settings.AlertFlashIntervalMs = milliseconds;
                }
                AppState.Session.SaveSettings();
            };

            // The burst length is deliberately a small number: a reminder should catch the eye and
            // then get out of the way while the user is working.
            ComboBox flashCountCombo = UiKit.Combo(new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("1", "1"),
                new KeyValuePair<string, string>("2", "2"),
                new KeyValuePair<string, string>("3", "3"),
                new KeyValuePair<string, string>("5", "5"),
                new KeyValuePair<string, string>("10", "10")
            }, settings.FlashCount.ToString());
            flashCountCombo.SelectionChanged += delegate
            {
                int count;
                if (int.TryParse(UiKit.SelectedKey(flashCountCombo), out count)) { settings.FlashCount = count; }
                AppState.Session.SaveSettings();
            };

            ComboBox checkCombo = UiKit.Combo(new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("15", "15 s"),
                new KeyValuePair<string, string>("30", "30 s"),
                new KeyValuePair<string, string>("60", "60 s"),
                new KeyValuePair<string, string>("300", "5 min"),
                new KeyValuePair<string, string>("900", "15 min")
            }, settings.AlertCheckSeconds.ToString());
            checkCombo.SelectionChanged += delegate
            {
                int seconds;
                if (int.TryParse(UiKit.SelectedKey(checkCombo), out seconds)) { settings.AlertCheckSeconds = seconds; }
                AppState.Session.SaveSettings();
            };

            CheckBox flash = new CheckBox();
            flash.Content = Loc.T("settings.alert_flash");
            flash.IsChecked = settings.AlertFlash;
            flash.Click += delegate(object sender, RoutedEventArgs e)
            {
                settings.AlertFlash = ((CheckBox)sender).IsChecked == true;
                AppState.Session.SaveSettings();
            };

            CheckBox onFocus = new CheckBox();
            onFocus.Content = Loc.T("settings.alert_on_focus");
            onFocus.IsChecked = settings.AlertOnFocus;
            onFocus.Click += delegate(object sender, RoutedEventArgs e)
            {
                settings.AlertOnFocus = ((CheckBox)sender).IsChecked == true;
                AppState.Session.SaveSettings();
            };

            StackPanel stack = new StackPanel();
            stack.Children.Add(UiKit.Field(Loc.T("settings.alert_lead"), leadCombo, Loc.T("widget.focus_alert_hint")));
            stack.Children.Add(UiKit.Field(Loc.T("settings.alert_flash_count"), flashCountCombo,
                Loc.T("settings.alert_flash_hint")));
            stack.Children.Add(UiKit.Field(Loc.T("settings.alert_interval"), intervalCombo, null));
            stack.Children.Add(UiKit.Field(Loc.T("settings.alert_check"), checkCombo, null));
            stack.Children.Add(flash);
            stack.Children.Add(onFocus);
            return Section(Loc.T("settings.alerts"), stack);
        }

        private UIElement DataCard(AppSettings settings)
        {
            TextBlock folder = UiKit.Body(AppState.Session.Root);
            folder.TextWrapping = TextWrapping.Wrap;

            long size = 0;
            string database = AppState.Session.Root;
            try
            {
                string dbPath = Path.Combine(AppState.Session.Root, ContainerLayout.DatabaseFile);
                if (File.Exists(dbPath)) { size = new FileInfo(dbPath).Length; }
            }
            catch
            {
            }

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.Children.Add(UiKit.Tonal(Loc.T("settings.backup_now"), delegate { _shell.BackupToFolder(true); }));
            Button openFolder = UiKit.Outlined(Loc.T("settings.open_data_folder"), delegate
            {
                try { Process.Start(new ProcessStartInfo(AppState.Session.Root) { UseShellExecute = true }); }
                catch (Exception ex)
                {
                    MessageBox.Show(_shell.Window, ex.Message, Loc.T("error.generic"), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            });
            openFolder.Margin = new Thickness(8, 0, 0, 0);
            buttons.Children.Add(openFolder);
            Button importContainer = UiKit.Outlined(Loc.T("settings.import_data"), delegate
            {
                string archive = Dialogs.PickArchive(_shell.Window);
                if (string.IsNullOrEmpty(archive)) { return; }
                string target = Dialogs.PickFolder(_shell.Window, Loc.T("container.import_target"), null);
                if (string.IsNullOrEmpty(target)) { return; }
                string error;
                if (!BackupService.ImportContainer(archive, target, out error))
                {
                    MessageBox.Show(_shell.Window, error ?? Loc.T("container.import_failed"), Loc.T("container.import"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                BackupService.BackupSummary summary = null;
                try { summary = BackupService.InspectArchive(archive, out error); } catch { }
                if (UiKit.Confirm(_shell.Window, Loc.T("container.import"),
                        Loc.T("container.import_success") + "\r\n" + target + "\r\n\r\n" + Loc.T("container.switch") + "?"))
                {
                    OpenContainer(target);
                }
            });
            importContainer.Margin = new Thickness(8, 0, 0, 0);
            buttons.Children.Add(importContainer);

            CheckBox autoBackup = new CheckBox();
            autoBackup.Content = Loc.T("settings.auto_backup");
            autoBackup.IsChecked = settings.AutoBackupOnExit;
            autoBackup.Margin = new Thickness(0, 10, 0, 0);
            autoBackup.Click += delegate(object sender, RoutedEventArgs e)
            {
                settings.AutoBackupOnExit = ((CheckBox)sender).IsChecked == true;
                AppState.Session.SaveSettings();
            };

            StackPanel stack = new StackPanel();
            stack.Children.Add(UiKit.Field(Loc.T("settings.data_folder"), folder, Loc.T("container.location_hint")));
            stack.Children.Add(UiKit.Caption(Loc.T("container.database") + ": " + ContainerLayout.DatabaseFile + " · " +
                (size / 1024.0).ToString("0.#") + " KB"));
            if (!string.IsNullOrEmpty(settings.LastBackupAt))
            {
                stack.Children.Add(UiKit.Caption(Loc.T("settings.last_backup", settings.LastBackupAt)));
            }
            stack.Children.Add(buttons);
            stack.Children.Add(autoBackup);
            return Section(Loc.T("settings.data"), stack);
        }

        private UIElement BehaviourCard(AppSettings settings)
        {
            CheckBox confirm = new CheckBox();
            confirm.Content = Loc.T("settings.confirm_delete");
            confirm.IsChecked = settings.ConfirmDelete;
            confirm.Click += delegate(object sender, RoutedEventArgs e)
            {
                settings.ConfirmDelete = ((CheckBox)sender).IsChecked == true;
                AppState.Session.SaveSettings();
            };

            CheckBox minimise = new CheckBox();
            minimise.Content = Loc.T("settings.minimise_to_tray");
            minimise.IsChecked = settings.MinimiseToTray;
            minimise.Click += delegate(object sender, RoutedEventArgs e)
            {
                settings.MinimiseToTray = ((CheckBox)sender).IsChecked == true;
                AppState.Session.SaveSettings();
            };

            Button reset = UiKit.Button(Loc.T("settings.reset_settings"), "Button.Danger", delegate
            {
                if (!UiKit.Confirm(_shell.Window, Loc.T("settings.reset_settings"), Loc.T("settings.reset_confirm"))) { return; }
                AppSettings fresh = new AppSettings();
                AppState.Session.Settings = fresh;
                AppState.Session.SaveSettings();
                ThemeManager.Apply(fresh.Theme, fresh.AccentColor, true);
                Loc.Apply("system");
                _shell.RefreshContent();
                Refresh();
            });
            reset.HorizontalAlignment = HorizontalAlignment.Left;
            reset.Margin = new Thickness(0, 10, 0, 0);

            StackPanel stack = new StackPanel();
            stack.Children.Add(confirm);
            stack.Children.Add(minimise);
            stack.Children.Add(reset);
            return Section(Loc.T("settings.behaviour"), stack);
        }

        private UIElement AboutCard(AppSettings settings)
        {
            StackPanel stack = new StackPanel();
            stack.Children.Add(UiKit.Body(AppPaths.AppName + "  " + AppPaths.AppVersionString()));
            stack.Children.Add(UiKit.Muted(AppPaths.Tagline));
            stack.Children.Add(UiKit.Caption(Loc.T("settings.engine", SqliteInfo.Describe())));
            stack.Children.Add(UiKit.Caption(Loc.T("settings.about_body")));

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.Margin = new Thickness(0, 10, 0, 0);
            buttons.Children.Add(UiKit.Outlined(Loc.T("container.switch"), delegate
            {
                ChooseContainer();
            }));
            buttons.Children.Add(UiKit.Outlined(Loc.T("container.new"), delegate
            {
                ContainerWizard wizard = new ContainerWizard(false);
                wizard.Owner = _shell.Window;
                bool? result = wizard.ShowDialog();
                if (result == true && !string.IsNullOrEmpty(wizard.ResultPath))
                {
                    OpenContainer(wizard.ResultPath);
                }
            }));
            stack.Children.Add(buttons);
            return Section(Loc.T("settings.about"), stack);
        }

        private void ChooseContainer()
        {
            string folder = Dialogs.PickFolder(_shell.Window, Loc.T("container.open"), AppState.Session.Root);
            if (string.IsNullOrEmpty(folder)) { return; }
            string error = ContainerSession.ValidateContainer(folder);
            if (error != null)
            {
                MessageBox.Show(_shell.Window, error, Loc.T("container.open"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            OpenContainer(folder);
        }

        private void OpenContainer(string folder)
        {
            try
            {
                ContainerSession session = ContainerSession.Open(folder);
                AppState.AttachContainer(session, true);
                Loc.Apply(session.Settings.Language);
                ThemeManager.Apply(session.Settings.Theme, session.Settings.AccentColor, true);
                Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show(_shell.Window, ex.Message, Loc.T("error.container_missing"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
