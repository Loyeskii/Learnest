using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;
using Learnest.Data;
using Learnest.Services;

using System.Windows.Shapes;
namespace Learnest.Views
{
    /// <summary>
    /// The desktop pin list widget: a small always-on-top window that can be left on the desktop
    /// and shows the closest deadlines.  Clicking the round check box completes a to-do without
    /// opening the app, the sort order and filter are user selectable, and whenever the widget
        /// gains mouse focus it re-checks the profile and re-renders its reminder state.
        ///
        /// Reminders are static: an item inside its reminder window gets a red gradient border,
        /// and the widget frame turns red while anything is in the alert window.  Nothing flashes.
    /// </summary>
    internal sealed class WidgetWindow : Window
    {
        public static WidgetWindow Current { get; private set; }

        private readonly MainWindow _shell;
        private readonly StackPanel _list;
        private readonly TextBlock _header;
        private readonly StackPanel _toolbar;
        private readonly Border _root;
        private readonly DispatcherTimer _refreshTimer;
        private int _alertCount;
        private bool _isReminding;
        private DateTime _lastFocusPrompt = DateTime.MinValue;

        public WidgetWindow(MainWindow shell)
        {
            _shell = shell;
            AppSettings settings = AppState.SessionOrNull != null ? AppState.SessionOrNull.Settings : new AppSettings();

            Title = AppPaths.AppName;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            ShowInTaskbar = false;
            Topmost = settings.WidgetTopmost;

            // Rounding the window itself needs a transparent surface: with AllowsTransparency = false
            // WPF cannot clip the window, so the rounded Border inside it sat on a rectangular frame.
            // WindowChrome keeps the ability to resize (ResizeBorderThickness) and the shell keeps
            // handling drag and snap, while the window edge follows the rounded Border.
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(6),
                CornerRadius = new CornerRadius(14),
                GlassFrameThickness = new Thickness(0),
                UseAeroCaptionButtons = false
            });

            Width = settings.WidgetWidth > 200 ? settings.WidgetWidth : 350;
            Height = settings.WidgetHeight > 200 ? settings.WidgetHeight : 500;
            MinWidth = 260;
            MinHeight = 220;
            FontFamily = UiKit.UiFont;

            if (!double.IsNaN(settings.WidgetLeft) && !double.IsNaN(settings.WidgetTop) &&
                settings.WidgetLeft > SystemParameters.VirtualScreenLeft - 40 &&
                settings.WidgetTop > SystemParameters.VirtualScreenTop - 40 &&
                settings.WidgetLeft < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 80 &&
                settings.WidgetTop < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 80)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = settings.WidgetLeft;
                Top = settings.WidgetTop;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = SystemParameters.WorkArea.Right - Width - 24;
                Top = SystemParameters.WorkArea.Top + 24;
            }

            // The frame: rounded, and the only place the widget draws a border.  It is never tinted by
            // the reminder state - see SyncReminderState.
            Padding = new Thickness(14, 10, 14, 14);
            _root = new Border();
            _root.Name = "WidgetFrame";
            _root.CornerRadius = new CornerRadius(14);
            _root.BorderThickness = new Thickness(1);
            _root.BorderBrush = UiKit.Brush("WidgetBorder");
            _root.Background = UiKit.Brush("WidgetBackground");
            _root.Padding = new Thickness(0);
            // The padding above leaves room for this shadow; without it the shadow would be clipped by
            // the window bounds, because the window itself is transparent (AllowsTransparency).
            _root.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 16,
                ShadowDepth = 1.5,
                Opacity = 0.30,
                Color = Colors.Black
            };

            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ---------------- title strip (drag handle + actions)
            Border titleBar = new Border();
            titleBar.Background = UiKit.Brush("Brush.SurfaceContainerHigh");
            titleBar.CornerRadius = new CornerRadius(13, 13, 0, 0);
            titleBar.Padding = new Thickness(10, 7, 6, 7);
            titleBar.Cursor = Cursors.SizeAll;
            titleBar.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ClickCount == 2) { Shell.Navigate("tasks"); return; }
                try { DragMove(); } catch { }
            };

            Grid titleGrid = new Grid();
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel brand = new StackPanel();
            brand.Orientation = Orientation.Horizontal;
            Path pin = UiKit.Icon("pin", 14, UiKit.Brush("Brush.Primary"));
            pin.VerticalAlignment = VerticalAlignment.Center;
            brand.Children.Add(pin);
            _header = UiKit.Text(Loc.T("widget.title"), "Text.Subtitle");
            _header.FontSize = 13;
            _header.Margin = new Thickness(6, 0, 0, 0);
            _header.VerticalAlignment = VerticalAlignment.Center;
            brand.Children.Add(_header);
            titleGrid.Children.Add(brand);

            StackPanel windowActions = new StackPanel();
            windowActions.Orientation = Orientation.Horizontal;
            windowActions.Children.Add(UiKit.IconButton("refresh", Loc.T("common.refresh"), delegate { Reload(); }));
            windowActions.Children.Add(UiKit.IconButton("dashboard", Loc.T("widget.open_app"), delegate { Shell.Navigate("dashboard"); }));
            windowActions.Children.Add(UiKit.IconButton("close", Loc.T("widget.hide"), delegate { HideWidget(); }));
            Grid.SetColumn(windowActions, 2);
            titleGrid.Children.Add(windowActions);

            titleBar.Child = titleGrid;
            Grid.SetRow(titleBar, 0);
            layout.Children.Add(titleBar);

            // ---------------- toolbar
            _toolbar = new StackPanel();
            _toolbar.Margin = new Thickness(8, 6, 8, 2);
            Grid.SetRow(_toolbar, 1);
            layout.Children.Add(_toolbar);

            // ---------------- list
            ScrollViewer scroller = new ScrollViewer();
            scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroller.Padding = new Thickness(8, 4, 8, 8);
            _list = new StackPanel();
            scroller.Content = _list;
            Grid.SetRow(scroller, 2);
            layout.Children.Add(scroller);

            // ---------------- footer
            Border footer = new Border();
            footer.Padding = new Thickness(10, 4, 10, 8);
            TextBlock hint = UiKit.Caption(Loc.T("widget.check_hint"));
            hint.TextWrapping = TextWrapping.Wrap;
            footer.Child = hint;
            Grid.SetRow(footer, 3);
            layout.Children.Add(footer);

            _root.Child = layout;

            Content = _root;

            _refreshTimer = new DispatcherTimer(DispatcherPriority.Background);
            _refreshTimer.Interval = TimeSpan.FromSeconds(30);
            _refreshTimer.Tick += delegate { Reload(); };
            _refreshTimer.Start();

            Current = this;
            Activated += delegate(object sender, EventArgs e) { OnGainedFocus(); };
            MouseEnter += delegate(object sender, MouseEventArgs e) { OnGainedFocus(); };
            LocationChanged += delegate { SaveGeometry(); };
            SizeChanged += delegate { SaveGeometry(); };
            Closed += delegate
            {
                if (ReferenceEquals(Current, this)) { Current = null; }
                SaveGeometry();
                if (AppState.SessionOrNull != null)
                {
                    AppState.SessionOrNull.Settings.WidgetVisible = false;
                    AppState.SessionOrNull.SaveSettings();
                }
            };

            Reload();
            ApplyOpacity();
        }

        private MainWindow Shell { get { return _shell; } }

        // ================================================================ content
        public void Reload()
        {
            if (AppState.SessionOrNull == null) { return; }
            AppSettings settings = AppState.SessionOrNull.Settings;

            _toolbar.Children.Clear();
            if (settings.WidgetShowToolbar)
            {
                WrapPanel row = new WrapPanel();

                ComboBox sortCombo = UiKit.Combo(new KeyValuePair<string, string>[]
                {
                    new KeyValuePair<string, string>("dueAsc", Loc.T("tasks.sort_due_asc")),
                    new KeyValuePair<string, string>("dueDesc", Loc.T("tasks.sort_due_desc")),
                    new KeyValuePair<string, string>("modifiedDesc", Loc.T("tasks.sort_modified_desc")),
                    new KeyValuePair<string, string>("modifiedAsc", Loc.T("tasks.sort_modified_asc")),
                    new KeyValuePair<string, string>("createdDesc", Loc.T("tasks.sort_created_desc")),
                    new KeyValuePair<string, string>("createdAsc", Loc.T("tasks.sort_created_asc"))
                }, settings.WidgetSort);
                sortCombo.FontSize = 11;
                sortCombo.MinHeight = 28;
                sortCombo.Width = 150;
                sortCombo.Margin = new Thickness(0, 0, 6, 4);
                ToolTipService.SetToolTip(sortCombo, Loc.T("widget.sort"));
                sortCombo.SelectionChanged += delegate
                {
                    settings.WidgetSort = UiKit.SelectedKey(sortCombo) ?? "dueAsc";
                    AppState.SessionOrNull.SaveSettings();
                    Reload();
                };
                row.Children.Add(sortCombo);

                ComboBox filterCombo = UiKit.Combo(new KeyValuePair<string, string>[]
                {
                    new KeyValuePair<string, string>("upcoming", Loc.T("widget.filter_upcoming")),
                    new KeyValuePair<string, string>("today", Loc.T("widget.filter_today")),
                    new KeyValuePair<string, string>("week", Loc.T("widget.filter_week")),
                    new KeyValuePair<string, string>("all", Loc.T("widget.filter_all"))
                }, settings.WidgetFilter);
                filterCombo.FontSize = 11;
                filterCombo.MinHeight = 28;
                filterCombo.Width = 116;
                filterCombo.Margin = new Thickness(0, 0, 6, 4);
                ToolTipService.SetToolTip(filterCombo, Loc.T("widget.filter"));
                filterCombo.SelectionChanged += delegate
                {
                    settings.WidgetFilter = UiKit.SelectedKey(filterCombo) ?? "upcoming";
                    AppState.SessionOrNull.SaveSettings();
                    Reload();
                };
                row.Children.Add(filterCombo);

                System.Windows.Controls.Primitives.ToggleButton topmostToggle =
                    new System.Windows.Controls.Primitives.ToggleButton();
                topmostToggle.Content = Loc.T("widget.always_on_top");
                topmostToggle.Style = UiKit.Style("Button.Chip");
                topmostToggle.IsChecked = settings.WidgetTopmost;
                topmostToggle.FontSize = 11;
                topmostToggle.MinHeight = 28;
                topmostToggle.Margin = new Thickness(0, 0, 0, 4);
                topmostToggle.Checked += delegate
                {
                    settings.WidgetTopmost = true;
                    AppState.SessionOrNull.SaveSettings();
                    ApplyTopmost();
                };
                topmostToggle.Unchecked += delegate
                {
                    settings.WidgetTopmost = false;
                    AppState.SessionOrNull.SaveSettings();
                    ApplyTopmost();
                };
                row.Children.Add(topmostToggle);
                _toolbar.Children.Add(row);
            }

            List<Todo> items = TaskQuery.WidgetItems(settings.WidgetFilter, settings.WidgetSort, DateTime.Now);
            _list.Children.Clear();

            DateTime now = DateTime.Now;
            int reminding = 0;
            for (int i = 0; i < items.Count; i++)
            {
                Todo todo = items[i];
                Urgency urgency = TaskQuery.UrgencyOf(todo, now);
                bool isReminding = TaskQuery.IsReminding(todo, now);
                if (isReminding) { reminding++; }
                _list.Children.Add(Row(todo, now, urgency, isReminding));
            }

            if (items.Count == 0)
            {
                _list.Children.Add(UiKit.Empty("check", Loc.T("widget.no_items"), Loc.T("widget.filter")));
            }

            // Frame and header reflect the reminder state; nothing animates.
            SyncReminderState(TaskQuery.Alerts(now));
            ToolTipService.SetToolTip(_header, Loc.T("widget.alerts_count", _alertCount));
            if (reminding == 0 && _alertCount == 0) { _header.Text = AppPaths.AppName; }
        }

        private UIElement Row(Todo todo, DateTime now, Urgency urgency, bool isReminding)
        {
            Border card = new Border();
            card.CornerRadius = new CornerRadius(10);
            card.Padding = new Thickness(8, 6, 8, 6);
            card.Margin = new Thickness(0, 1, 0, 3);
            card.Background = isReminding ? UiKit.AlertTintBrush() : UiKit.Brush("Brush.SurfaceContainerLow");

            // Items inside their reminder window carry the red gradient border; everything else keeps
            // the neutral outline.  This replaced the flashing reminder.
            card.BorderThickness = new Thickness(isReminding ? 2 : 1);
            card.BorderBrush = isReminding
                ? UiKit.AlertBorderBrush()
                : UiKit.Brush("Brush.OutlineVariant");

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            CheckBox check = new CheckBox();
            check.Style = UiKit.Style("CheckBox.Round");
            check.IsChecked = todo.IsDone;
            check.VerticalAlignment = VerticalAlignment.Center;
            ToolTipService.SetToolTip(check, Loc.T("tasks.mark_complete"));
            string id = todo.Id;
            check.Click += delegate(object sender, RoutedEventArgs e)
            {
                CheckBox box = (CheckBox)sender;
                AppState.Repo.SetTodoStatus(id, box.IsChecked == true ? TodoStatus.Done : TodoStatus.Open);
                AppState.Commit();
                Reload();
            };
            grid.Children.Add(check);

            StackPanel text = new StackPanel();
            text.Margin = new Thickness(9, 0, 6, 0);
            TextBlock title = UiKit.Text(todo.Title, "Text.Body");
            title.FontSize = 12.5;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.ToolTip = todo.Title;
            if (todo.IsDone) { title.TextDecorations = TextDecorations.Strikethrough; title.Opacity = 0.6; }
            text.Children.Add(title);

            StackPanel meta = new StackPanel();
            meta.Orientation = Orientation.Horizontal;
            Border dot = UiKit.Dot(AppState.ColorFor(todo), 7);
            dot.Margin = new Thickness(0, 0, 5, 0);
            meta.Children.Add(dot);
            string metaText = todo.DueDate.HasValue
                ? Services.CalendarService.FormatDateShort(todo.DueDate.Value) + " · " +
                  Services.CalendarService.DescribeDistance(todo.DueDate.Value, now)
                : AppState.Data.NameOf(todo.CategoryId);
            TextBlock metaBlock = UiKit.Caption(metaText);
            metaBlock.FontSize = 10.5;
            if (isReminding) { metaBlock.Foreground = UiKit.Brush("Brush.Error"); }
            meta.Children.Add(metaBlock);
            text.Children.Add(meta);
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            Button advanced = UiKit.IconButton("markdown", Loc.T("tasks.advanced_record"), delegate
            {
                Todo fresh = AppState.Repo.FindTodo(id);
                if (fresh == null) { return; }
                Show();
                Activate();
                TaskEditor.ShowAdvanced(_shell, fresh);
            });
            advanced.Width = 26;
            advanced.Height = 26;
            Grid.SetColumn(advanced, 2);
            grid.Children.Add(advanced);

            card.Child = grid;
            return card;
        }

        // ================================================================ reminder state
        //
        // The reminder is a static visual state, not an animation: a to-do that has entered its
        // reminder window gets a red gradient border in the list (see Row) and the widget shows a
        // matching border plus an alert count.  An earlier version flashed the widget; it was
        // distracting while working, so all flashing was removed.

        /// <summary>
        /// Re-checks the profile and refreshes the widget's reminder state.  Called whenever the
        /// widget gains mouse focus, and by the periodic deadline check.
        /// </summary>
        private void OnGainedFocus()
        {
            if (AppState.SessionOrNull == null) { return; }
            if (DateTime.Now - _lastFocusPrompt < TimeSpan.FromSeconds(2)) { return; }
            _lastFocusPrompt = DateTime.Now;
            Reload();
        }

        /// <summary>
        /// Reflects the current reminder state on the window frame: a red border while anything is
        /// inside the alert window, the normal frame otherwise.  No animation is involved.
        /// </summary>
        /// <summary>
        /// Refreshes the widget header count.
        ///
        /// The window frame is deliberately NOT tinted or bordered by the reminder state any more:
        /// a red gradient frame around the whole widget read as visual residue and could sit there
        /// for days.  The reminder signal lives on the individual to-do rows (see Row), which is what
        /// the user asked for.
        /// </summary>
        public void SyncReminderState(List<TaskQuery.AlertHit> hits)
        {
            bool reminding = hits != null && hits.Count > 0;
            _alertCount = reminding ? hits.Count : 0;
            _isReminding = reminding;
            _header.Text = AppPaths.AppName + (reminding ? " · " + _alertCount : "");
        }

        /// <summary>Clears the reminder state (used when nothing is in the alert window any more).</summary>
        public void ClearReminderState()
        {
            SyncReminderState(null);
        }

        /// <summary>Number of items currently inside the alert window (shown in the header).</summary>
        internal int ReminderCount { get { return _alertCount; } }

        /// <summary>True when at least one item is inside the alert window.</summary>
        internal bool IsReminding { get { return _isReminding; } }

        public void ApplyTopmost()
        {
            if (AppState.SessionOrNull == null) { return; }
            Topmost = false;
            Topmost = AppState.SessionOrNull.Settings.WidgetTopmost;
        }

        public void ApplyOpacity()
        {
            double opacity = AppState.SessionOrNull != null ? AppState.SessionOrNull.Settings.WidgetOpacity : 0.96;
            if (opacity < 0.3) { opacity = 0.3; }
            if (opacity > 1) { opacity = 1; }
            // Applied to the frame, never to the window: the window is transparent (AllowsTransparency)
            // and carries the shadow margin around the frame.
            _root.Opacity = opacity;
            _root.Background = UiKit.Brush("WidgetBackground");
        }

        private void HideWidget()
        {
            Hide();
            if (AppState.SessionOrNull != null)
            {
                AppState.SessionOrNull.Settings.WidgetVisible = false;
                AppState.SessionOrNull.SaveSettings();
            }
        }

        private void SaveGeometry()
        {
            if (AppState.SessionOrNull == null) { return; }
            if (WindowState != WindowState.Normal) { return; }
            AppSettings settings = AppState.SessionOrNull.Settings;
            settings.WidgetLeft = Left;
            settings.WidgetTop = Top;
            settings.WidgetWidth = Width;
            settings.WidgetHeight = Height;
            AppState.SessionOrNull.Settings.WidgetVisible = IsVisible;
            AppState.SessionOrNull.SaveSettings();
        }
    }
}
