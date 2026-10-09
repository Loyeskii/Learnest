using System;
using System.Collections.Generic;
using System.IO;
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
    /// First run (and "switch container") wizard.  Learnest never stores business data next to the
    /// executable, so the very first thing it asks is where the container folder should live:
    /// create a new one, open an existing one, or restore one from an exported archive.  The choice
    /// is remembered in %LocalAppData%\Learnest\appsettings.json so the next launch goes straight
    /// to the app.
    /// </summary>
    public sealed class ContainerWizard : Window
    {
        public string ResultPath { get; private set; }
        public bool CreatedNew { get; private set; }

        private readonly bool _allowCancel;
        private readonly TextBlock _title;
        private readonly TextBlock _subtitle;
        private readonly StackPanel _body;
        private readonly StackPanel _modeRow;

        private string _mode = "new";
        private TextBox _newFolderBox;
        private TextBox _newNameBox;
        private TextBox _openFolderBox;
        private ComboBox _languageCombo;
        private readonly List<string> _recent = new List<string>();

        public ContainerWizard(bool allowCancel)
        {
            _allowCancel = allowCancel;

            Title = AppPaths.AppName + " · " + AppPaths.Tagline;
            Width = 720;
            Height = 560;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanResize;
            MinWidth = 560;
            MinHeight = 480;
            ShowInTaskbar = true;
            Background = UiKit.Brush("Brush.Surface");
            FontFamily = UiKit.UiFont;
            Style = UiKit.Style("LearnestWindow");

            Grid root = new Grid();
            root.Margin = new Thickness(28, 24, 28, 20);
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel brand = new StackPanel();
            brand.Orientation = Orientation.Horizontal;
            // Same mark as the executable icon (UiKit falls back to an accent tile if unavailable).
            ContentControl mark = new ContentControl();
            mark.Width = 40;
            mark.Height = 40;
            mark.Content = UiKit.AppMark(40);
            brand.Children.Add(mark);

            StackPanel brandText = new StackPanel();
            brandText.Margin = new Thickness(12, 0, 0, 0);
            TextBlock appName = UiKit.Text(AppPaths.AppName, "Text.Display");
            appName.FontSize = 24;
            brandText.Children.Add(appName);
            TextBlock tagline = UiKit.Muted(AppPaths.Tagline);
            brandText.Children.Add(tagline);
            brand.Children.Add(brandText);
            Grid.SetRow(brand, 0);
            root.Children.Add(brand);

            _title = UiKit.Text(Loc.T("container.welcome_title"), "Text.Subtitle");
            _title.Margin = new Thickness(0, 18, 0, 4);
            Grid.SetRow(_title, 1);
            root.Children.Add(_title);

            _subtitle = UiKit.Muted(Loc.T("container.welcome_body"));
            _subtitle.TextWrapping = TextWrapping.Wrap;
            _subtitle.Margin = new Thickness(0, 0, 0, 14);
            Grid.SetRow(_subtitle, 1);

            // ---- mode selector + body
            StackPanel centre = new StackPanel();
            centre.Margin = new Thickness(0, 12, 0, 0);

            _modeRow = new StackPanel();
            _modeRow.Orientation = Orientation.Horizontal;
            _modeRow.Margin = new Thickness(0, 0, 0, 14);
            _modeRow.Children.Add(ModeChip(Loc.T("container.new"), "new"));
            _modeRow.Children.Add(ModeChip(Loc.T("container.open"), "open"));
            _modeRow.Children.Add(ModeChip(Loc.T("container.import"), "import"));
            centre.Children.Add(_modeRow);

            _body = new StackPanel();
            ScrollViewer bodyScroller = new ScrollViewer();
            bodyScroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            bodyScroller.Content = _body;
            bodyScroller.MaxHeight = 300;
            centre.Children.Add(bodyScroller);

            Grid.SetRow(centre, 2);
            root.Children.Add(centre);

            // ---- buttons
            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Margin = new Thickness(0, 16, 0, 0);

            if (_allowCancel)
            {
                buttons.Children.Add(UiKit.Outlined(Loc.T("common.cancel"), delegate { DialogResult = false; }));
            }
            Button confirm = UiKit.Primary(Loc.T("common.next"), delegate { Confirm(); });
            confirm.Margin = new Thickness(8, 0, 0, 0);
            confirm.MinWidth = 110;
            buttons.Children.Add(confirm);
            Grid.SetRow(buttons, 3);
            root.Children.Add(buttons);

            Content = root;
            LoadRecents();
            BuildBody();
        }

        private System.Windows.Controls.Primitives.ToggleButton ModeChip(string label, string mode)
        {
            System.Windows.Controls.Primitives.ToggleButton chip =
                new System.Windows.Controls.Primitives.ToggleButton();
            chip.Content = label;
            chip.Style = UiKit.Style("Button.Chip");
            chip.Tag = mode;
            chip.Margin = new Thickness(0, 0, 8, 0);
            chip.MinHeight = 34;
            chip.Padding = new Thickness(16, 6, 16, 6);
            chip.IsChecked = _mode == mode;
            chip.Checked += delegate(object sender, RoutedEventArgs e)
            {
                _mode = (string)((System.Windows.Controls.Primitives.ToggleButton)sender).Tag;
                for (int i = 0; i < _modeRow.Children.Count; i++)
                {
                    System.Windows.Controls.Primitives.ToggleButton other =
                        _modeRow.Children[i] as System.Windows.Controls.Primitives.ToggleButton;
                    if (other != null && !ReferenceEquals(other, sender)) { other.IsChecked = false; }
                }
                BuildBody();
            };
            return chip;
        }

        private void LoadRecents()
        {
            _recent.Clear();
            try
            {
                List<string> stored = AppState.Config.RecentContainers;
                for (int i = 0; i < stored.Count; i++)
                {
                    if (Directory.Exists(stored[i]) && ContainerLayout.IsContainer(stored[i])) { _recent.Add(stored[i]); }
                }
                if (!string.IsNullOrEmpty(AppState.Config.LastContainer) &&
                    ContainerLayout.IsContainer(AppState.Config.LastContainer) &&
                    !_recent.Contains(AppState.Config.LastContainer))
                {
                    _recent.Insert(0, AppState.Config.LastContainer);
                }
            }
            catch
            {
            }
        }

        private void BuildBody()
        {
            _body.Children.Clear();

            if (_mode == "new")
            {
                _title.Text = Loc.T("container.welcome_title");
                _subtitle.Text = Loc.T("container.new_desc");

                // Deliberately left empty: Learnest must not pre-fill a container location.  A wrong
                // suggestion either fails on Next (read-only Documents on redirected profiles) or
                // silently creates the container somewhere the user did not intend.  Suggestions are
                // offered as one click chips instead.
                _newFolderBox = UiKit.Input("", Loc.T("container.browse_folder"));
                _newNameBox = UiKit.Input("", Loc.T("container.display_name"));

                StackPanel folderRow = new StackPanel();
                folderRow.Orientation = Orientation.Horizontal;
                _newFolderBox.Width = 400;
                folderRow.Children.Add(_newFolderBox);
                Button browse = UiKit.Outlined(Loc.T("common.browse"), delegate
                {
                    string start = _newFolderBox.Text;
                    if (string.IsNullOrEmpty(start) || !Directory.Exists(start)) { start = AppPaths.UserProfileOrTemp(); }
                    string chosen = Dialogs.PickFolder(this, Loc.T("container.browse_folder"), start);
                    if (!string.IsNullOrEmpty(chosen))
                    {
                        _newFolderBox.Text = chosen;
                        if (string.IsNullOrWhiteSpace(_newNameBox.Text))
                        {
                            _newNameBox.Text = AppPaths.ContainerNameFromPath(chosen);
                        }
                    }
                });
                browse.Margin = new Thickness(8, 0, 0, 0);
                folderRow.Children.Add(browse);

                StackPanel folderField = UiKit.Field(Loc.T("container.path"), folderRow, null);
                folderField.Children.Add(SuggestionChips());
                _body.Children.Add(folderField);
                _body.Children.Add(UiKit.Field(Loc.T("container.display_name"), _newNameBox, null));

                List<KeyValuePair<string, string>> languages = new List<KeyValuePair<string, string>>();
                languages.Add(new KeyValuePair<string, string>("system", Loc.T("settings.language_system")));
                List<LanguagePack> packs = Loc.Available;
                for (int i = 0; i < packs.Count; i++)
                {
                    languages.Add(new KeyValuePair<string, string>(packs[i].Code, packs[i].DisplayName));
                }
                _languageCombo = UiKit.Combo(languages, string.IsNullOrEmpty(AppState.Config.LanguageOverride)
                    ? "system" : AppState.Config.LanguageOverride);
                _body.Children.Add(UiKit.Field(Loc.T("settings.language"), _languageCombo, null));

                TextBlock hint = UiKit.Caption(Loc.T("container.portable_hint"));
                hint.TextWrapping = TextWrapping.Wrap;
                _body.Children.Add(hint);
                return;
            }

            if (_mode == "open")
            {
                _title.Text = Loc.T("container.open");
                _subtitle.Text = Loc.T("container.open_desc");

                _openFolderBox = UiKit.Input("", Loc.T("container.path"));
                StackPanel folderRow = new StackPanel();
                folderRow.Orientation = Orientation.Horizontal;
                _openFolderBox.Width = 400;
                folderRow.Children.Add(_openFolderBox);
                Button browse = UiKit.Outlined(Loc.T("common.browse"), delegate
                {
                    string chosen = Dialogs.PickFolder(this, Loc.T("container.open"), _openFolderBox.Text);
                    if (!string.IsNullOrEmpty(chosen)) { _openFolderBox.Text = chosen; }
                });
                browse.Margin = new Thickness(8, 0, 0, 0);
                folderRow.Children.Add(browse);
                _body.Children.Add(UiKit.Field(Loc.T("container.selected_folder", _openFolderBox.Text ?? ""), folderRow, null));

                _body.Children.Add(UiKit.SectionHeader(Loc.T("container.recent"), null));
                if (_recent.Count == 0)
                {
                    _body.Children.Add(UiKit.Caption(Loc.T("container.no_recent")));
                }
                for (int i = 0; i < _recent.Count; i++)
                {
                    string path = _recent[i];
                    Border row = new Border();
                    row.CornerRadius = new CornerRadius(10);
                    row.Padding = new Thickness(10, 8, 10, 8);
                    row.Margin = new Thickness(0, 2, 0, 2);
                    row.Background = UiKit.Brush("Brush.SurfaceContainer");
                    row.Cursor = Cursors.Hand;
                    Grid grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.Children.Add(UiKit.Icon("folder", 16, UiKit.Brush("Brush.Primary")));
                    StackPanel text = new StackPanel();
                    text.Margin = new Thickness(9, 0, 0, 0);
                    text.Children.Add(UiKit.Body(AppPaths.ContainerNameFromPath(path)));
                    text.Children.Add(UiKit.Caption(path));
                    Grid.SetColumn(text, 1);
                    grid.Children.Add(text);
                    row.Child = grid;
                    row.MouseLeftButtonUp += delegate
                    {
                        _openFolderBox.Text = path;
                    };
                    _body.Children.Add(row);
                }
                return;
            }

            // import
            _title.Text = Loc.T("container.import");
            _subtitle.Text = Loc.T("container.import_desc");

            TextBox archiveBox = UiKit.Input("", Loc.T("container.import_file"));
            StackPanel archiveRow = new StackPanel();
            archiveRow.Orientation = Orientation.Horizontal;
            archiveBox.Width = 400;
            archiveRow.Children.Add(archiveBox);
            Button browseArchive = UiKit.Outlined(Loc.T("common.browse"), delegate
            {
                string chosen = Dialogs.PickArchive(this);
                if (!string.IsNullOrEmpty(chosen)) { archiveBox.Text = chosen; }
            });
            browseArchive.Margin = new Thickness(8, 0, 0, 0);
            archiveRow.Children.Add(browseArchive);
            _body.Children.Add(UiKit.Field(Loc.T("container.import_file"), archiveRow, null));

            TextBox targetBox = UiKit.Input(Path.Combine(PickWritableFolder(), "Restored"), null);
            StackPanel targetRow = new StackPanel();
            targetRow.Orientation = Orientation.Horizontal;
            targetBox.Width = 400;
            targetRow.Children.Add(targetBox);
            Button browseTarget = UiKit.Outlined(Loc.T("common.browse"), delegate
            {
                string chosen = Dialogs.PickFolder(this, Loc.T("container.import_target"), targetBox.Text);
                if (!string.IsNullOrEmpty(chosen)) { targetBox.Text = chosen; }
            });
            browseTarget.Margin = new Thickness(8, 0, 0, 0);
            targetRow.Children.Add(browseTarget);
            _body.Children.Add(UiKit.Field(Loc.T("container.import_target"), targetRow, Loc.T("container.location_hint")));

            _body.Tag = new object[] { archiveBox, targetBox };
        }

        private void Confirm()
        {
            try
            {
                if (_mode == "new") { ConfirmNew(); return; }
                if (_mode == "open") { ConfirmOpen(); return; }
                ConfirmImport();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Loc.T("error.generic"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// One click chips for the locations Learnest verified it can write to.  These are offered
        /// rather than pre-filled so the user always makes the final decision.
        /// </summary>
        private UIElement SuggestionChips()
        {
            WrapPanel chips = new WrapPanel();
            chips.Margin = new Thickness(0, 8, 0, 0);

            TextBlock caption = UiKit.Caption(Loc.T("container.location_hint"));
            caption.Margin = new Thickness(0, 0, 0, 4);
            chips.Children.Add(caption);

            List<string> writable = new List<string>();
            List<string> candidates = AppPaths.SuggestedContainerRoots();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (AppPaths.IsUnsafeContainerTarget(candidates[i])) { continue; }
                if (Data.ContainerLayout.IsContainer(candidates[i])) { continue; }
                if (!Data.ContainerLayout.IsAvailable(candidates[i])) { continue; }
                if (!AppPaths.CanWriteTo(candidates[i])) { continue; }
                writable.Add(candidates[i]);
                if (writable.Count >= 4) { break; }
            }

            if (writable.Count == 0)
            {
                chips.Children.Add(UiKit.Caption(Loc.T("container.invalid_folder")));
                return chips;
            }

            for (int i = 0; i < writable.Count; i++)
            {
                string path = writable[i];
                Button chip = UiKit.Chip(path, null);
                chip.Margin = new Thickness(0, 4, 6, 0);
                chip.MinHeight = 28;
                chip.FontSize = 11;
                ToolTipService.SetToolTip(chip, path);
                chip.Click += delegate
                {
                    _newFolderBox.Text = path;
                    if (string.IsNullOrWhiteSpace(_newNameBox.Text))
                    {
                        _newNameBox.Text = AppPaths.ContainerNameFromPath(path);
                    }
                };
                chips.Children.Add(chip);
            }
            return chips;
        }

        private void ConfirmNew()
        {
            string folder = (_newFolderBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(folder))
            {
                Warn(Loc.T("container.invalid_folder"));
                return;
            }
            if (AppPaths.IsUnsafeContainerTarget(folder))
            {
                Warn("请不要把容器放在系统盘根目录或 Windows / Program Files 等受保护位置。\r\n" +
                     "建议使用「文档」或用户目录下的一个空文件夹。");
                return;
            }
            if (Data.ContainerLayout.IsContainer(folder))
            {
                Warn(Loc.T("container.container_exists"));
                return;
            }
            if (!Data.ContainerLayout.IsAvailable(folder))
            {
                Warn(Loc.T("container.folder_not_empty"));
                return;
            }
            // A folder can exist yet be read-only, so probe it before creating anything.
            string reason;
            if (!AppPaths.CanWriteTo(folder, out reason))
            {
                Warn(reason + "\r\n\r\n" + Loc.T("container.location_hint"));
                return;
            }
            string name = (_newNameBox.Text ?? "").Trim();
            if (name.Length == 0) { name = AppPaths.ContainerNameFromPath(folder); }

            ContainerSession session = ContainerSession.Create(folder, name);
            session.WriteMeta("containerName", name);
            string language = UiKit.SelectedKey(_languageCombo);
            session.Settings.Language = language == null ? "" : language;
            session.SaveSettings();
            session.Repository.Log("container", "container", "", "创建容器：" + name);
            session.WriteManifest();

            ResultPath = session.Root;
            CreatedNew = true;
            AppState.AttachContainer(session, true);
            DialogResult = true;
        }

        /// <summary>
        /// First suggestion that Learnest can actually write to.  Falls back to the last candidate
        /// (a temp folder) so the wizard can always show something usable.
        /// </summary>
        private static string PickWritableFolder()
        {
            List<string> candidates = AppPaths.SuggestedContainerRoots();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (AppPaths.IsUnsafeContainerTarget(candidates[i])) { continue; }
                if (Data.ContainerLayout.IsContainer(candidates[i])) { continue; }
                if (!Data.ContainerLayout.IsAvailable(candidates[i])) { continue; }
                if (AppPaths.CanWriteTo(candidates[i])) { return candidates[i]; }
            }
            // Nothing worked: hand back the best guess and let the wizard explain on Next.
            return candidates.Count > 0 ? candidates[candidates.Count - 1] : Path.GetTempPath();
        }

        private void ConfirmOpen()
        {
            string folder = (_openFolderBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                Warn(Loc.T("container.invalid_folder"));
                return;
            }
            string error = ContainerSession.ValidateContainer(folder);
            if (error != null && ContainerLayout.IsContainer(folder)) { Warn(error); return; }
            if (!ContainerLayout.IsContainer(folder))
            {
                Warn(Loc.T("container.not_a_container"));
                return;
            }
            ContainerSession session = ContainerSession.Open(folder);
            ResultPath = session.Root;
            AppState.AttachContainer(session, true);
            DialogResult = true;
        }

        private void ConfirmImport()
        {
            object[] boxes = _body.Tag as object[];
            if (boxes == null) { return; }
            string archive = ((TextBox)boxes[0]).Text;
            string target = ((TextBox)boxes[1]).Text;
            if (string.IsNullOrEmpty(archive) || !File.Exists(archive))
            {
                Warn(Loc.T("container.import_file"));
                return;
            }
            if (string.IsNullOrEmpty(target))
            {
                Warn(Loc.T("container.import_target"));
                return;
            }
            string reason;
            if (!AppPaths.CanWriteTo(target, out reason))
            {
                Warn(reason);
                return;
            }
            string error;
            if (!BackupService.ImportContainer(archive, target, out error))
            {
                Warn(error ?? Loc.T("container.import_failed"));
                return;
            }
            ContainerSession session = ContainerSession.Open(target);
            ResultPath = session.Root;
            AppState.AttachContainer(session, true);
            DialogResult = true;
        }

        private void Warn(string message)
        {
            MessageBox.Show(this, message, AppPaths.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
