using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Learnest.Data;
using Learnest.Services;
using Learnest.Theming;

namespace Learnest.Views
{
    /// <summary>
    /// The application shell: custom title bar, navigation rail, category filter, global search and
    /// the content host that switches between the seven top level views.
    /// </summary>
    public partial class MainWindow : Window, IShell
    {
        private readonly Dictionary<string, FrameworkElement> _viewCache = new Dictionary<string, FrameworkElement>(StringComparer.Ordinal);
        private string _activeView = "dashboard";
        private string _activeCategoryId;
        private ReminderService _reminders;
        private bool _navReady;

        public MainWindow()
        {
            Program.Trace("MainWindow: ctor begin");
            InitializeComponent();
            Program.Trace("MainWindow: XAML loaded");

            // WindowChrome (see Themes/Window.xaml) suppresses the OS title bar so only our own is
            // visible.  Two details have to be handled in code:
            //   * WM_GETMINMAXINFO, otherwise a maximised chrome window is clipped by the resize
            //     border and its top edge sits off screen;
            //   * a margin correction when restoring, so the 8px transparent resize border does not
            //     make the window grow by 16px on every maximise/restore cycle.
            SourceInitialized += OnSourceInitialized;
            StateChanged += OnStateChanged;

            ApplyLocalisation();
            Program.Trace("MainWindow: localisation applied");
            BuildNavigation();
            Program.Trace("MainWindow: navigation built");
            BuildFooter();
            Program.Trace("MainWindow: footer built");
            RestoreGeometry();
            Program.Trace("MainWindow: geometry restored");

            Loaded += OnLoaded;
            Closing += OnClosing;
            Closed += OnClosed;

            ThemeManager.PaletteChanged += OnPaletteChanged;
            Loc.Changed += OnLanguageChanged;
            AppState.Changed += OnDataChanged;
            AppState.ContainerChanged += OnContainerChanged;
            Program.Trace("MainWindow: ctor done");
        }

        // ================================================================ IShell
        public Window Window { get { return this; } }

        public string ActiveView { get { return _activeView; } }

        public void Navigate(string viewId)
        {
            Navigate(viewId, null);
        }

        public void Navigate(string viewId, string argument)
        {
            if (string.IsNullOrEmpty(viewId)) { return; }
            _activeView = viewId;
            if (!string.IsNullOrEmpty(argument)) { _pendingArgument = argument; }
            ShowActiveView();
            UpdateNavSelection();
            try
            {
                if (AppState.SessionOrNull != null)
                {
                    AppState.SessionOrNull.Settings.LastView = viewId;
                    AppState.SessionOrNull.SaveSettings();
                }
            }
            catch
            {
            }
        }

        private string _pendingArgument;

        public string TakeArgument()
        {
            string value = _pendingArgument;
            _pendingArgument = null;
            return value;
        }

        public void SetStatus(string text)
        {
            StatusText.Text = text ?? "";
        }

        public void RefreshContent()
        {
            IRefreshable refreshable = ContentHost.Content as IRefreshable;
            if (refreshable != null) { refreshable.Refresh(); }
            RefreshCategoryPanel();
            UpdateStatusBar();
        }

        /// <summary>Drop every cached view so the next navigation rebuilds it (container switch).</summary>
        public void InvalidateViews()
        {
            _viewCache.Clear();
            ContentHost.Content = null;
        }

        // ================================================================ startup
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Program.Trace("MainWindow.OnLoaded begin");
            try
            {
                WindowBackground.Apply(BackgroundLayer, AppState.SessionOrNull != null ? AppState.SessionOrNull.Settings : null);
                Program.Trace("  background applied");
                _navReady = true;
                RefreshCategoryPanel();
                Program.Trace("  category rail built");
                string start = AppState.SessionOrNull != null ? AppState.SessionOrNull.Settings.LastView : "dashboard";
                if (string.IsNullOrEmpty(start)) { start = "dashboard"; }
                Navigate(start);
                Program.Trace("  navigated to '" + start + "'");
                UpdateStatusBar();
                Program.Trace("  status bar updated");

                _reminders = new ReminderService(this);
                _reminders.Start();
                Program.Trace("  reminder service started");

                if (AppState.SessionOrNull != null && AppState.SessionOrNull.Settings.WidgetVisible)
                {
                    ShowWidget(true);
                    Program.Trace("  pin widget restored");
                }
                Program.Trace("MainWindow.OnLoaded done");
            }
            catch (Exception ex)
            {
                Program.Trace("FATAL in MainWindow.OnLoaded: " + Program.Describe(ex));
                App.LogCrash(ex, "MainWindow.OnLoaded");
                MessageBox.Show(
                    Loc.T("error.generic") + "\r\n\r\n" + Program.Describe(ex) + "\r\n\r\n" +
                    Program.StartupTracePath,
                    AppPaths.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveGeometry();
            if (AppState.SessionOrNull != null)
            {
                AppSettings settings = AppState.SessionOrNull.Settings;
                if (settings.AutoBackupOnExit)
                {
                    try
                    {
                        string fileName;
                        BackupService.ExportContainer(AppState.SessionOrNull, null, out fileName);
                    }
                    catch
                    {
                    }
                }
                AppState.SessionOrNull.Flush();
            }
            if (WidgetWindow.Current != null)
            {
                WidgetWindow.Current.Close();
            }
            if (_reminders != null) { _reminders.Dispose(); }
        }

        private void OnClosed(object sender, EventArgs e)
        {
            ThemeManager.PaletteChanged -= OnPaletteChanged;
            Loc.Changed -= OnLanguageChanged;
            AppState.Changed -= OnDataChanged;
            AppState.ContainerChanged -= OnContainerChanged;
            Application.Current.Shutdown();
        }

        // ================================================================ geometry
        private void RestoreGeometry()
        {
            AppSettings settings = AppState.SessionOrNull != null ? AppState.SessionOrNull.Settings : null;
            if (settings == null) { return; }
            if (!double.IsNaN(settings.MainWindowWidth) && settings.MainWindowWidth > 800) { Width = settings.MainWindowWidth; }
            if (!double.IsNaN(settings.MainWindowHeight) && settings.MainWindowHeight > 600) { Height = settings.MainWindowHeight; }
            if (!double.IsNaN(settings.MainWindowLeft) && !double.IsNaN(settings.MainWindowTop))
            {
                double left = settings.MainWindowLeft;
                double top = settings.MainWindowTop;
                // Only restore when the saved position is still on a visible screen.
                if (left > SystemParameters.VirtualScreenLeft - 50 && top > SystemParameters.VirtualScreenTop - 50 &&
                    left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100 &&
                    top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 100)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    Left = left;
                    Top = top;
                }
            }
            if (settings.MainWindowState == "maximized") { WindowState = WindowState.Maximized; }
        }

        private void SaveGeometry()
        {
            if (AppState.SessionOrNull == null) { return; }
            AppSettings settings = AppState.SessionOrNull.Settings;
            settings.MainWindowState = WindowState == WindowState.Maximized ? "maximized" : "normal";
            if (WindowState == WindowState.Normal)
            {
                settings.MainWindowLeft = Left;
                settings.MainWindowTop = Top;
                settings.MainWindowWidth = Width;
                settings.MainWindowHeight = Height;
            }
        }

        // ================================================================ title bar
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximise();
                return;
            }
            try { DragMove(); }
            catch { }
        }

        private void ToggleMaximise()
        {
            // SystemCommands keeps the OS in charge of snapping, restore animation and the taskbar
            // behaviour of the minimise / restore button, which a bare WindowState assignment does
            // not with a chrome window.
            if (WindowState == WindowState.Maximized)
            {
                SystemCommands.RestoreWindow(this);
            }
            else
            {
                SystemCommands.MaximizeWindow(this);
            }
            UpdateMaximiseGlyph();
        }

        /// <summary>Swap the maximise button between "maximise" and "restore" glyphs.</summary>
        private void UpdateMaximiseGlyph()
        {
            if (MaximiseGlyph == null) { return; }
            try
            {
                MaximiseGlyph.Data = Geometry.Parse(WindowState == WindowState.Maximized
                    ? UiKit.IconGeometry("restore")
                    : UiKit.IconGeometry("maximise"));
            }
            catch
            {
            }
        }

        private void MinimiseButton_Click(object sender, RoutedEventArgs e)
        {
            SystemCommands.MinimizeWindow(this);
        }

        private void MaximiseButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximise();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) { Close(); }

        // ================================================================ chrome plumbing
        private void OnSourceInitialized(object sender, EventArgs e)
        {
            try
            {
                IntPtr handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                System.Windows.Interop.HwndSource source = System.Windows.Interop.HwndSource.FromHwnd(handle);
                if (source != null) { source.AddHook(WindowProc); }
            }
            catch (Exception ex)
            {
                Program.Trace("source init hook failed: " + ex.Message);
            }
            UpdateMaximiseGlyph();
        }

        private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_GETMINMAXINFO = 0x0024;
            if (msg == WM_GETMINMAXINFO)
            {
                AdjustMaximisedBounds(hwnd, lParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// A WindowChrome window is larger than the work area by the size of its (invisible) resize
        /// border, so the maximised client area would be clipped.  Shrink the maximised size and move
        /// the position by the same amount.
        /// </summary>
        private static void AdjustMaximisedBounds(IntPtr hwnd, IntPtr lParam)
        {
            try
            {
                System.Windows.Interop.HwndSource source =
                    System.Windows.Interop.HwndSource.FromHwnd(hwnd);
                if (source == null || source.CompositionTarget == null) { return; }
                System.Windows.Media.Matrix matrix = source.CompositionTarget.TransformToDevice;
                double dpiX = matrix.M11;
                double dpiY = matrix.M22;
                if (dpiX <= 0) { dpiX = 1; }
                if (dpiY <= 0) { dpiY = 1; }

                int borderX = (int)Math.Round(SystemParameters.WindowResizeBorderThickness.Left * dpiX);
                int borderY = (int)Math.Round(SystemParameters.WindowResizeBorderThickness.Top * dpiY);

                MINMAXINFO info = (MINMAXINFO)System.Runtime.InteropServices.Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
                info.ptMaxPosition.x += 2 * borderX;
                info.ptMaxPosition.y += 2 * borderY;
                info.ptMaxSize.x -= 4 * borderX;
                info.ptMaxSize.y -= 4 * borderY;
                System.Runtime.InteropServices.Marshal.StructureToPtr(info, lParam, true);
            }
            catch
            {
            }
        }

        private void OnStateChanged(object sender, EventArgs e)
        {
            UpdateMaximiseGlyph();
            // Correct the window rectangle once the state has settled.
            if (WindowState == WindowState.Normal)
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    try
                    {
                        Rect workArea = SystemParameters.WorkArea;
                        if (Left < workArea.Left || Top < workArea.Top)
                        {
                            Left = Math.Max(Left, workArea.Left);
                            Top = Math.Max(Top, workArea.Top);
                        }
                    }
                    catch
                    {
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            if (AppState.SessionOrNull == null) { return; }
            AppSettings settings = AppState.SessionOrNull.Settings;
            string next = settings.Theme == ThemeMode.Light ? ThemeMode.Dark
                        : settings.Theme == ThemeMode.Dark ? ThemeMode.System : ThemeMode.Light;
            settings.Theme = next;
            AppState.SessionOrNull.SaveSettings();
            ThemeManager.Apply(settings.Theme, settings.AccentColor, true);
            LoadThemeIcon();
            SetStatus(ThemeLabel(next));
        }

        private static string ThemeLabel(string mode)
        {
            if (mode == ThemeMode.Light) { return Loc.T("settings.theme_light"); }
            if (mode == ThemeMode.Dark) { return Loc.T("settings.theme_dark"); }
            return Loc.T("settings.theme_system");
        }

        private void LoadThemeIcon()
        {
            string mode = AppState.SessionOrNull != null ? AppState.SessionOrNull.Settings.Theme : ThemeMode.System;
            string glyph = mode == ThemeMode.Dark ? "moon" : mode == ThemeMode.Light ? "sun" : "monitor";
            ThemeIcon.Data = Geometry.Parse(UiKit.IconGeometry(glyph));
        }

        private void WidgetButton_Click(object sender, RoutedEventArgs e)
        {
            ShowWidget(WidgetWindow.Current == null);
        }

        public void ShowWidget(bool visible)
        {
            if (visible)
            {
                if (WidgetWindow.Current == null)
                {
                    WidgetWindow widget = new WidgetWindow(this);
                    widget.Show();
                }
                else
                {
                    WidgetWindow.Current.Activate();
                }
            }
            else if (WidgetWindow.Current != null)
            {
                WidgetWindow.Current.Close();
            }
            if (AppState.SessionOrNull != null)
            {
                AppState.SessionOrNull.Settings.WidgetVisible = visible;
                AppState.SessionOrNull.SaveSettings();
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e) { Navigate("settings"); }

        private void GlobalSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = GlobalSearchBox.Text;
            _searchText = text;
            if (string.IsNullOrEmpty(text))
            {
                RefreshContent();
                return;
            }
            // Searching jumps to the task list, which owns the result rendering.
            if (_activeView != "tasks")
            {
                Navigate("tasks");
            }
            else
            {
                RefreshContent();
            }
        }

        private string _searchText;

        public string SearchText { get { return _searchText; } }

        // ================================================================ navigation
        private void BuildNavigation()
        {
            NavPanel.Children.Clear();
            NavPanel.Children.Add(NavItem("dashboard", "nav.dashboard", "dashboard"));
            NavPanel.Children.Add(NavItem("tasks", "nav.tasks", "tasks"));
            NavPanel.Children.Add(NavItem("timeline", "nav.timeline", "timeline"));
            NavPanel.Children.Add(NavItem("exams", "nav.exams", "exam"));
            NavPanel.Children.Add(NavItem("competitions", "nav.competitions", "trophy"));
            NavPanel.Children.Add(NavItem("calendar", "nav.calendar", "calendar"));
        }

        private RadioButton NavItem(string viewId, string locKey, string icon)
        {
            RadioButton item = new RadioButton();
            item.Style = UiKit.Style("NavItem");
            item.GroupName = "learnest-nav";
            item.Tag = UiKit.Icon(icon, 18, UiKit.Brush("Brush.NavItemForeground"));
            item.Content = Loc.T(locKey);
            item.Name = "nav_" + viewId;
            item.Checked += delegate
            {
                if (!_navReady) { return; }
                Navigate(viewId);
            };
            return item;
        }

        private void BuildFooter()
        {
            NavFooterPanel.Children.Clear();
            Button backup = UiKit.Button(Loc.T("settings.backup_now"), "Button.Text", delegate { BackupToFolder(true); });
            backup.HorizontalAlignment = HorizontalAlignment.Left;
            backup.Padding = new Thickness(10, 6, 10, 6);
            NavFooterPanel.Children.Add(backup);

            TextBlock hint = UiKit.Caption(Loc.T("container.portable_hint"));
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(10, 2, 4, 4);
            NavFooterPanel.Children.Add(hint);
        }

        private void UpdateNavSelection()
        {
            _navReady = false;
            for (int i = 0; i < NavPanel.Children.Count; i++)
            {
                RadioButton item = NavPanel.Children[i] as RadioButton;
                if (item == null) { continue; }
                item.IsChecked = item.Name == "nav_" + _activeView;
            }
            _navReady = true;
        }

        private void ShowActiveView()
        {
            FrameworkElement view;
            if (!_viewCache.TryGetValue(_activeView, out view))
            {
                Program.Trace("  creating view '" + _activeView + "'");
                view = CreateView(_activeView);
                if (view == null) { return; }
                _viewCache[_activeView] = view;
                Program.Trace("  view '" + _activeView + "' created");
            }
            ContentHost.Content = view;
            IRefreshable refreshable = view as IRefreshable;
            if (refreshable != null)
            {
                Program.Trace("  refreshing view '" + _activeView + "'");
                refreshable.Refresh();
                Program.Trace("  view '" + _activeView + "' refreshed");
            }
        }

        private FrameworkElement CreateView(string viewId)
        {
            switch (viewId)
            {
                case "tasks": return new TasksView(this);
                case "timeline": return new TimelineView(this);
                case "exams": return new ExamsView(this);
                case "competitions": return new CompetitionsView(this);
                case "calendar": return new CalendarView(this);
                case "settings": return new SettingsView(this);
                default: return new DashboardView(this);
            }
        }

        // ================================================================ categories
        private void RefreshCategoryPanel()
        {
            CategoryPanel.Children.Clear();
            if (AppState.SessionOrNull == null) { return; }

            CategoryPanel.Children.Add(CategoryRow(null, Loc.T("common.all"), CountTodos(null), null));
            List<Category> categories = AppState.Data.Categories;
            for (int i = 0; i < categories.Count; i++)
            {
                Category category = categories[i];
                if (category.IsUncategorized) { continue; }
                CategoryPanel.Children.Add(CategoryRow(category.Id, category.Name, CountTodos(category.Id), category.Color));
            }
            Category uncategorized = FindUncategorized();
            if (uncategorized != null)
            {
                CategoryPanel.Children.Add(CategoryRow(uncategorized.Id, uncategorized.Name, CountTodos(uncategorized.Id), uncategorized.Color));
            }
        }

        private static Category FindUncategorized()
        {
            List<Category> categories = AppState.Data.Categories;
            for (int i = 0; i < categories.Count; i++)
            {
                if (categories[i].IsUncategorized) { return categories[i]; }
            }
            return null;
        }

        private int CountTodos(string categoryId)
        {
            int count = 0;
            List<Todo> todos = AppState.Data.Todos;
            for (int i = 0; i < todos.Count; i++)
            {
                if (!todos[i].IsOpen) { continue; }
                if (categoryId == null || todos[i].CategoryId == categoryId) { count++; }
            }
            return count;
        }

        private FrameworkElement CategoryRow(string categoryId, string name, int count)
        {
            return CategoryRow(categoryId, name, count, null);
        }

        private FrameworkElement CategoryRow(string categoryId, string name, int count, string colorHex)
        {
            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border dot = UiKit.Dot(colorHex ?? "#94A3B8", 9);
            dot.Margin = new Thickness(10, 0, 0, 0);
            row.Children.Add(dot);

            TextBlock label = UiKit.Body(name);
            label.Margin = new Thickness(9, 0, 6, 0);
            label.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(label, 1);
            row.Children.Add(label);

            TextBlock counter = UiKit.Caption(count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            counter.Margin = new Thickness(0, 0, 10, 0);
            counter.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(counter, 2);
            row.Children.Add(counter);

            Border container = new Border();
            container.CornerRadius = new CornerRadius(18);
            container.Padding = new Thickness(0, 7, 0, 7);
            container.Margin = new Thickness(8, 1, 8, 1);
            container.Cursor = Cursors.Hand;
            container.Background = categoryId == _activeCategoryId
                ? UiKit.Brush("Brush.NavSelected")
                : Brushes.Transparent;
            container.Child = row;
            container.MouseLeftButtonUp += delegate
            {
                _activeCategoryId = categoryId;
                RefreshCategoryPanel();
                if (_activeView != "tasks") { Navigate("tasks"); }
                else { RefreshContent(); }
            };
            return container;
        }

        public string ActiveCategoryId { get { return _activeCategoryId; } }

        public void ClearCategoryFilter()
        {
            _activeCategoryId = null;
            RefreshCategoryPanel();
        }

        private void AddCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            CategoryEditor.ShowCreate(this, delegate { AppState.Commit(); RefreshCategoryPanel(); });
        }

        // ================================================================ backup
        public void BackupToFolder(bool interactive)
        {
            if (AppState.SessionOrNull == null) { return; }
            try
            {
                string folder = null;
                if (interactive)
                {
                    folder = Dialogs.PickFolder(this, Loc.T("container.export_folder"), AppState.SessionOrNull.Root);
                    if (string.IsNullOrEmpty(folder)) { return; }
                }
                string fileName;
                string path = BackupService.ExportContainer(AppState.SessionOrNull, folder, out fileName);
                SetStatus(Loc.T("container.export_success") + " · " + fileName);
                if (interactive)
                {
                    UiKit.Message(this, Loc.T("backup.export_title"),
                        Loc.T("backup.export_done", fileName) + "\r\n\r\n" + path);
                }
            }
            catch (Exception ex)
            {
                SetStatus(Loc.T("container.export_failed"));
                if (interactive)
                {
                    MessageBox.Show(this, Loc.T("container.export_failed") + "\r\n" + ex.Message,
                        Loc.T("error.generic"), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        // ================================================================ theme/language/data events
        private void OnPaletteChanged(object sender, EventArgs e)
        {
            WindowBackground.Apply(BackgroundLayer, AppState.SessionOrNull != null ? AppState.SessionOrNull.Settings : null);
            LoadThemeIcon();
            // Rebuild the cached views: colours are resolved when controls are created.
            string keep = _activeView;
            InvalidateViews();
            Navigate(keep);
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            ApplyLocalisation();
            BuildNavigation();
            BuildFooter();
            string keep = _activeView;
            InvalidateViews();
            Navigate(keep);
            RefreshCategoryPanel();
        }

        private void OnDataChanged(object sender, EventArgs e)
        {
            RefreshContent();
        }

        private void OnContainerChanged(object sender, EventArgs e)
        {
            InvalidateViews();
            ApplyLocalisation();
            RefreshCategoryPanel();
            WindowBackground.Apply(BackgroundLayer, AppState.SessionOrNull != null ? AppState.SessionOrNull.Settings : null);
            UpdateStatusBar();
            Navigate(AppState.SessionOrNull != null ? AppState.SessionOrNull.Settings.LastView : "dashboard");
        }

        private void ApplyLocalisation()
        {
            AppTitleText.Text = Loc.T("app.name");
            Title = AppPaths.AppName + " · " + AppPaths.Tagline;
            CategoriesHeader.Text = Loc.T("common.categories").ToUpperInvariant();
            AddCategoryButton.Content = "+ " + Loc.T("tasks.category_new");
            ContainerNameText.Text = AppState.SessionOrNull != null ? AppState.SessionOrNull.Name : Loc.T("container.none_selected");
            ToolTipService.SetToolTip(ThemeButton, Loc.T("settings.theme"));
            ToolTipService.SetToolTip(WidgetButton, Loc.T("widget.pin"));
            ToolTipService.SetToolTip(SettingsButton, Loc.T("settings.title"));
            LoadThemeIcon();
            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            if (AppState.SessionOrNull == null)
            {
                StatusText.Text = Loc.T("container.welcome_body");
                StatusRightText.Text = "";
                return;
            }
            TaskQuery.Summary summary = TaskQuery.Summarise(DateTime.Now);
            StatusText.Text = Loc.T("tasks.overdue_warning", summary.Overdue) + " " + summary.Overdue + " · " +
                              Loc.T("tasks.due_today") + " " + summary.DueToday + " · " +
                              Loc.T("tasks.due_this_week") + " " + summary.DueThisWeek;
            StatusRightText.Text = Loc.T("container.current") + ": " + AppState.SessionOrNull.Root;
        }

        // ================================================================ reminder tick
        /// <summary>
        /// Periodic deadline check.  Reminders are static: the widget frame turns red while anything
        /// is inside the alert window and the affected rows carry a red gradient border in the list.
        /// Nothing flashes or animates.
        /// </summary>
        internal void OnReminderTick(List<TaskQuery.AlertHit> hits, int newAlerts)
        {
            if (hits != null && hits.Count > 0)
            {
                SetStatus(Loc.T("alert.deadline_approaching", hits.Count));
            }
            else
            {
                SetStatus("");
            }

            if (WidgetWindow.Current == null) { return; }
            WidgetWindow.Current.SyncReminderState(hits);
            if (newAlerts > 0) { WidgetWindow.Current.Reload(); }
        }
    }

    /// <summary>Implemented by any view that can reload itself from the current snapshot.</summary>
    public interface IRefreshable
    {
        void Refresh();
    }

    /// <summary>The services a view may use without knowing about the shell internals.</summary>
    public interface IShell
    {
        Window Window { get; }
        string ActiveView { get; }
        string ActiveCategoryId { get; }
        string SearchText { get; }
        void Navigate(string viewId);
        void Navigate(string viewId, string argument);
        string TakeArgument();
        void SetStatus(string text);
        void RefreshContent();
        void ClearCategoryFilter();
        void BackupToFolder(bool interactive);
        void ShowWidget(bool visible);
    }
}
