using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Learnest.Data;
using Learnest.Services;
using Learnest.Theming;

using Learnest.Core;
namespace Learnest.Views
{
    /// <summary>
    /// Competitions Planner.  A competition is a long running project that other to-dos can
    /// reference as a tag (a single to-do may reference several).  Each competition owns
    /// milestones, important dated events, a markdown work log and its own attachment folder
    /// ("pack and go"): everything stays inside the container so the folder is copyable.
    /// </summary>
    internal sealed class CompetitionsView : UserControl, IRefreshable
    {
        private readonly IShell _shell;
        private readonly StackPanel _master;
        private readonly StackPanel _detail;
        private readonly TextBox _search;
        private string _selectedId;
        private bool _showArchived;

        public CompetitionsView(IShell shell)
        {
            _shell = shell;

            Grid root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // ---------------- master list
            Border masterHost = new Border();
            masterHost.Background = UiKit.Brush("Brush.SurfaceContainerLow");
            masterHost.BorderBrush = UiKit.Brush("Brush.OutlineVariant");
            masterHost.BorderThickness = new Thickness(0, 0, 1, 0);
            Grid masterGrid = new Grid();
            masterGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            masterGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            StackPanel masterHeader = new StackPanel();
            masterHeader.Margin = new Thickness(16, 14, 16, 6);
            masterHeader.Children.Add(UiKit.Text(Loc.T("comp.title"), "Text.Title"));

            _search = UiKit.Input("", Loc.T("common.search"));
            _search.Margin = new Thickness(0, 8, 0, 6);
            _search.TextChanged += delegate { Refresh(); };
            masterHeader.Children.Add(_search);

            StackPanel masterActions = new StackPanel();
            masterActions.Orientation = Orientation.Horizontal;
            masterActions.Children.Add(UiKit.Primary("+ " + Loc.T("comp.new"), delegate
            {
                CompetitionEditor.ShowCreate(_shell, delegate(string id)
                {
                    _selectedId = id;
                    Refresh();
                });
            }));
            System.Windows.Controls.Primitives.ToggleButton archivedToggle = new System.Windows.Controls.Primitives.ToggleButton();
            archivedToggle.Content = Loc.T("comp.stage_archived");
            archivedToggle.Style = UiKit.Style("Button.Chip");
            archivedToggle.Margin = new Thickness(8, 0, 0, 0);
            archivedToggle.Checked += delegate { _showArchived = true; Refresh(); };
            archivedToggle.Unchecked += delegate { _showArchived = false; Refresh(); };
            masterActions.Children.Add(archivedToggle);
            masterHeader.Children.Add(masterActions);
            Grid.SetRow(masterHeader, 0);
            masterGrid.Children.Add(masterHeader);

            ScrollViewer masterScroller = new ScrollViewer();
            masterScroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            masterScroller.Padding = new Thickness(10, 0, 10, 12);
            _master = new StackPanel();
            masterScroller.Content = _master;
            Grid.SetRow(masterScroller, 1);
            masterGrid.Children.Add(masterScroller);
            masterHost.Child = masterGrid;
            root.Children.Add(masterHost);

            // ---------------- detail
            ScrollViewer detailScroller = new ScrollViewer();
            detailScroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            detailScroller.Padding = new Thickness(20, 16, 20, 20);
            _detail = new StackPanel();
            detailScroller.Content = _detail;
            Grid.SetColumn(detailScroller, 1);
            root.Children.Add(detailScroller);

            Content = root;
            Refresh();
        }

        public void Refresh()
        {
            RenderMaster();
            RenderDetail();
        }

        // ================================================================ master
        private void RenderMaster()
        {
            _master.Children.Clear();
            if (AppState.SessionOrNull == null) { return; }

            string argument = _shell.TakeArgument();
            if (!string.IsNullOrEmpty(argument)) { _selectedId = argument; }

            string needle = (_search.Text ?? "").Trim();
            List<Competition> competitions = AppState.Data.Competitions;
            int shown = 0;
            for (int i = 0; i < competitions.Count; i++)
            {
                Competition competition = competitions[i];
                bool archived = competition.Stage == "archived" || competition.Stage == "finished";
                if (archived && !_showArchived) { continue; }
                if (needle.Length > 0 &&
                    competition.Name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0 &&
                    (competition.Tag ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                _master.Children.Add(MasterRow(competition));
                shown++;
            }

            if (shown == 0)
            {
                _master.Children.Add(UiKit.Empty("trophy", Loc.T("comp.empty"), Loc.T("comp.empty_hint")));
            }
            if (string.IsNullOrEmpty(_selectedId) && competitions.Count > 0)
            {
                _selectedId = competitions[0].Id;
            }
        }

        private UIElement MasterRow(Competition competition)
        {
            bool selected = competition.Id == _selectedId;
            Border card = new Border();
            card.CornerRadius = new CornerRadius(12);
            card.Padding = new Thickness(12, 10, 12, 10);
            card.Margin = new Thickness(0, 3, 0, 3);
            card.Cursor = Cursors.Hand;
            card.Background = selected ? UiKit.Brush("Brush.Selection") : Brushes.Transparent;
            if (selected)
            {
                card.BorderBrush = UiKit.Brush("Brush.Primary");
                card.BorderThickness = new Thickness(1);
            }

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border dot = UiKit.Dot(competition.Color, 10);
            dot.VerticalAlignment = VerticalAlignment.Top;
            dot.Margin = new Thickness(0, 4, 0, 0);
            grid.Children.Add(dot);

            StackPanel text = new StackPanel();
            text.Margin = new Thickness(10, 0, 6, 0);
            StackPanel titleRow = new StackPanel();
            titleRow.Orientation = Orientation.Horizontal;
            TextBlock title = UiKit.Body(competition.Name);
            title.FontWeight = FontWeights.SemiBold;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            titleRow.Children.Add(title);
            if (competition.Starred)
            {
                System.Windows.Shapes.Path star = UiKit.Icon("star_filled", 12, UiKit.Brush("Brush.Warning"));
                star.Margin = new Thickness(5, 0, 0, 0);
                titleRow.Children.Add(star);
            }
            text.Children.Add(titleRow);

            string tagText = string.IsNullOrEmpty(competition.Tag) ? "" : "#" + competition.Tag + " · ";
            text.Children.Add(UiKit.Caption(tagText + Loc.T("comp.stage_" + competition.Stage) + " · " +
                competition.Milestones.Count + " " + Loc.T("comp.milestones")));
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            TextBlock progress = UiKit.Caption((competition.MilestoneProgress() * 100).ToString("0") + "%");
            progress.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(progress, 2);
            grid.Children.Add(progress);

            card.Child = grid;
            string id = competition.Id;
            card.MouseLeftButtonUp += delegate
            {
                _selectedId = id;
                Refresh();
            };
            return card;
        }

        // ================================================================ detail
        private void RenderDetail()
        {
            _detail.Children.Clear();
            Competition competition = string.IsNullOrEmpty(_selectedId) ? null : AppState.Data.FindCompetition(_selectedId);
            if (competition == null)
            {
                _detail.Children.Add(UiKit.Empty("trophy", Loc.T("comp.empty"), Loc.T("comp.empty_hint")));
                return;
            }

            // Header -------------------------------------------------------
            Grid header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titles = new StackPanel();
            StackPanel nameRow = new StackPanel();
            nameRow.Orientation = Orientation.Horizontal;
            TextBlock name = UiKit.Text(competition.Name, "Text.Display");
            name.FontSize = 24;
            nameRow.Children.Add(name);
            Border stageBadge = UiKit.Badge(Loc.T("comp.stage_" + competition.Stage),
                UiKit.Alpha(ColorUtil.Parse(competition.Color, Colors.Gray), 0.18),
                new SolidColorBrush(ColorUtil.Parse(competition.Color, Colors.Gray)));
            stageBadge.Margin = new Thickness(10, 4, 0, 0);
            nameRow.Children.Add(stageBadge);
            titles.Children.Add(nameRow);

            StackPanel meta = new StackPanel();
            meta.Orientation = Orientation.Horizontal;
            meta.Margin = new Thickness(0, 4, 0, 0);
            if (!string.IsNullOrEmpty(competition.Tag))
            {
                meta.Children.Add(UiKit.Caption("#" + competition.Tag + "  "));
            }
            if (!string.IsNullOrEmpty(competition.Organizer))
            {
                meta.Children.Add(UiKit.Caption(Loc.T("comp.organizer") + ": " + competition.Organizer + "  "));
            }
            if (!string.IsNullOrEmpty(competition.StartAt) || !string.IsNullOrEmpty(competition.EndAt))
            {
                meta.Children.Add(UiKit.Caption(competition.StartAt.Replace('T', ' ').Substring(0, Math.Min(16, competition.StartAt.Length)) +
                    " → " + competition.EndAt.Replace('T', ' ').Substring(0, Math.Min(16, competition.EndAt.Length))));
            }
            titles.Children.Add(meta);
            header.Children.Add(titles);

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.VerticalAlignment = VerticalAlignment.Top;
            actions.Children.Add(UiKit.IconButton(competition.Starred ? "star_filled" : "star", Loc.T("comp.starred"), delegate
            {
                competition.Starred = !competition.Starred;
                AppState.Repo.SaveCompetition(competition);
                AppState.Commit();
            }));
            actions.Children.Add(UiKit.IconButton("edit", Loc.T("common.edit"), delegate
            {
                CompetitionEditor.ShowEdit(_shell, competition);
            }));
            actions.Children.Add(UiKit.IconButton("folder_open", Loc.T("comp.pack_and_go"), delegate
            {
                OpenAttachmentFolder(competition);
            }));
            actions.Children.Add(UiKit.IconButton("trash", Loc.T("common.delete"), delegate
            {
                DeleteCompetition(competition);
            }));
            Grid.SetColumn(actions, 1);
            header.Children.Add(actions);
            _detail.Children.Add(header);

            // Progress -----------------------------------------------------
            StackPanel progressCard = new StackPanel();
            Grid progressHeader = new Grid();
            progressHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            progressHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            progressHeader.Children.Add(UiKit.Subtitle(Loc.T("common.progress")));
            double value = competition.Milestones.Count > 0 ? competition.MilestoneProgress() : competition.Progress;
            TextBlock percent = UiKit.Label((value * 100).ToString("0") + "% · " + Loc.T("comp.progress_from_milestones"));
            Grid.SetColumn(percent, 1);
            progressHeader.Children.Add(percent);
            progressCard.Children.Add(progressHeader);
            ProgressBar bar = new ProgressBar();
            bar.Minimum = 0;
            bar.Maximum = 1;
            bar.Value = value;
            bar.Margin = new Thickness(0, 8, 0, 0);
            progressCard.Children.Add(bar);
            Border progressBorder = UiKit.Card(progressCard, false);
            progressBorder.Margin = new Thickness(0, 14, 0, 14);
            _detail.Children.Add(progressBorder);

            // Tabs: milestones / events / work log / files / linked todos
            TabControl tabs = new TabControl();
            tabs.MinHeight = 340;

            tabs.Items.Add(DetailTab(Loc.T("comp.milestones"), MilestonesPanel(competition)));
            tabs.Items.Add(DetailTab(Loc.T("comp.events"), EventsPanel(competition)));
            tabs.Items.Add(DetailTab(Loc.T("comp.worklog"), WorkLogPanel(competition)));
            tabs.Items.Add(DetailTab(Loc.T("comp.files"), FilesPanel(competition)));
            tabs.Items.Add(DetailTab(Loc.T("comp.linked_todos"), LinkedTodosPanel(competition)));
            _detail.Children.Add(tabs);
        }

        private static TabItem DetailTab(string header, UIElement content)
        {
            TabItem item = new TabItem();
            item.Header = header;
            item.Content = content;
            return item;
        }

        // ---------------------------------------------------------------- milestones
        private UIElement MilestonesPanel(Competition competition)
        {
            StackPanel panel = new StackPanel();
            panel.Margin = new Thickness(2, 12, 2, 2);

            Button add = UiKit.Tonal("+ " + Loc.T("comp.milestone_new"), delegate
            {
                ShowMilestoneDialog(competition, null);
            });
            add.HorizontalAlignment = HorizontalAlignment.Left;
            panel.Children.Add(add);

            if (competition.Milestones.Count == 0)
            {
                panel.Children.Add(UiKit.Empty("flag", Loc.T("comp.no_milestones"), Loc.T("comp.milestone_detail")));
                return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            }

            List<Milestone> milestones = new List<Milestone>(competition.Milestones);
            milestones.Sort(delegate(Milestone a, Milestone b)
            {
                DateTime da = a.DueDate.HasValue ? a.DueDate.Value : DateTime.MaxValue;
                DateTime db = b.DueDate.HasValue ? b.DueDate.Value : DateTime.MaxValue;
                return da.CompareTo(db);
            });

            DateTime now = DateTime.Now;
            for (int i = 0; i < milestones.Count; i++)
            {
                Milestone milestone = milestones[i];
                Border card = new Border();
                card.Style = UiKit.Style("RowCard");

                Grid grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                CheckBox done = new CheckBox();
                done.Style = UiKit.Style("CheckBox.Round");
                done.IsChecked = milestone.IsDone;
                done.VerticalAlignment = VerticalAlignment.Center;
                done.Click += delegate(object sender, RoutedEventArgs e)
                {
                    AppState.Repo.SetMilestoneDone(milestone.Id, ((CheckBox)sender).IsChecked == true);
                    AppState.Commit();
                };
                grid.Children.Add(done);

                StackPanel text = new StackPanel();
                text.Margin = new Thickness(11, 0, 10, 0);
                TextBlock title = UiKit.Body(milestone.Title);
                title.FontWeight = FontWeights.SemiBold;
                if (milestone.IsDone) { title.TextDecorations = TextDecorations.Strikethrough; title.Opacity = 0.65; }
                text.Children.Add(title);
                StackPanel meta = new StackPanel();
                meta.Orientation = Orientation.Horizontal;
                if (milestone.DueDate.HasValue)
                {
                    TextBlock due = UiKit.Caption(Services.CalendarService.FormatDateTime(milestone.DueDate.Value) + " · " +
                        Services.CalendarService.DescribeDistance(milestone.DueDate.Value, now));
                    if (!milestone.IsDone && milestone.DueDate.Value < now)
                    {
                        due.Foreground = UiKit.Brush("Brush.Error");
                    }
                    meta.Children.Add(due);
                }
                if (!string.IsNullOrEmpty(milestone.Detail))
                {
                    meta.Children.Add(UiKit.Caption(" · " + milestone.Detail));
                }
                text.Children.Add(meta);

                if (!string.IsNullOrEmpty(milestone.NoteMarkdown))
                {
                    string preview = Markdown.ToPlainText(milestone.NoteMarkdown);
                    string[] lines = preview.Replace("\r\n", "\n").Split('\n');
                    for (int l = 0; l < lines.Length; l++)
                    {
                        if (lines[l].Trim().Length == 0) { continue; }
                        TextBlock note = UiKit.Caption(lines[l].Trim());
                        note.TextTrimming = TextTrimming.CharacterEllipsis;
                        note.Margin = new Thickness(0, 2, 0, 0);
                        text.Children.Add(note);
                        break;
                    }
                }
                Grid.SetColumn(text, 1);
                grid.Children.Add(text);

                TextBlock weight = UiKit.Caption(milestone.Weight.ToString("0.##"));
                weight.VerticalAlignment = VerticalAlignment.Center;
                weight.Margin = new Thickness(0, 0, 8, 0);
                weight.ToolTip = Loc.T("comp.milestone_weight");
                Grid.SetColumn(weight, 2);
                grid.Children.Add(weight);

                StackPanel rowActions = new StackPanel();
                rowActions.Orientation = Orientation.Horizontal;
                rowActions.Children.Add(UiKit.IconButton("markdown", Loc.T("tasks.advanced_record"), delegate
                {
                    ShowMilestoneDialog(competition, milestone);
                }));
                rowActions.Children.Add(UiKit.IconButton("trash", Loc.T("common.delete"), delegate
                {
                    if (!UiKit.Confirm(_shell.Window, Loc.T("common.delete"), milestone.Title)) { return; }
                    AppState.Repo.DeleteMilestone(milestone.Id);
                    AppState.Commit();
                }));
                Grid.SetColumn(rowActions, 3);
                grid.Children.Add(rowActions);

                card.Child = grid;
                panel.Children.Add(card);
            }
            return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private void ShowMilestoneDialog(Competition competition, Milestone existing)
        {
            bool isNew = existing == null;
            Milestone working = isNew ? new Milestone() : existing;
            if (isNew) { working.CompetitionId = competition.Id; }

            TextBox titleBox = UiKit.Input(working.Title, Loc.T("comp.milestone_title"));
            TextBox detailBox = UiKit.Input(working.Detail, Loc.T("common.optional"));

            DateTime seed = working.DueDate.HasValue ? working.DueDate.Value : DateTime.Now.Date.AddDays(14).AddHours(23).AddMinutes(59);
            System.Windows.Controls.DatePicker dueDate = new System.Windows.Controls.DatePicker();
            dueDate.SelectedDate = working.DueDate.HasValue ? working.DueDate.Value.Date : (DateTime?)null;
            dueDate.Style = UiKit.Style("DatePicker");
            TimeField dueTime = new TimeField(seed, 5);

            CheckBox hasDue = new CheckBox();
            hasDue.Content = Loc.T("comp.milestone_due");
            hasDue.IsChecked = working.DueDate.HasValue || isNew;
            hasDue.Margin = new Thickness(0, 0, 0, 6);

            Grid dueRow = new Grid();
            dueRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            dueRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            dueRow.Children.Add(dueDate);
            Grid.SetColumn(dueTime, 1);
            dueTime.Margin = new Thickness(8, 0, 0, 0);
            dueRow.Children.Add(dueTime);
            dueRow.IsEnabled = hasDue.IsChecked == true;
            hasDue.Checked += delegate { dueRow.IsEnabled = true; };
            hasDue.Unchecked += delegate { dueRow.IsEnabled = false; };

            TextBox weightBox = UiKit.Input(working.Weight.ToString("0.##"), "1");

            Border previewHost = new Border();
            previewHost.Padding = new Thickness(12);
            previewHost.CornerRadius = new CornerRadius(11);
            previewHost.Background = UiKit.Brush("Brush.SurfaceContainer");
            previewHost.BorderThickness = new Thickness(1);
            previewHost.BorderBrush = UiKit.Brush("Brush.OutlineVariant");
            MarkdownEditor markdown = new MarkdownEditor(working.NoteMarkdown, previewHost, true);

            StackPanel content = new StackPanel();
            content.Children.Add(UiKit.Field(Loc.T("comp.milestone_title"), titleBox, null));
            content.Children.Add(UiKit.Field(Loc.T("common.description"), detailBox, null));
            StackPanel dueField = new StackPanel();
            dueField.Children.Add(hasDue);
            dueField.Children.Add(dueRow);
            content.Children.Add(UiKit.Field(Loc.T("comp.milestone_due"), dueField, null));
            content.Children.Add(UiKit.Field(Loc.T("comp.milestone_weight"), weightBox, Loc.T("comp.progress_from_milestones")));
            content.Children.Add(UiKit.SectionHeader(Loc.T("tasks.advanced_record"), null));
            content.Children.Add(markdown);

            UiKit.ShowDialog(_shell.Window, isNew ? Loc.T("comp.milestone_new") : Loc.T("common.edit"), content,
                Loc.T("common.save"), Loc.T("common.cancel"), delegate
                {
                    string title = (titleBox.Text ?? "").Trim();
                    if (title.Length == 0) { return false; }
                    working.Title = title;
                    working.Detail = (detailBox.Text ?? "").Trim();
                    working.CompetitionId = competition.Id;
                    if (hasDue.IsChecked == true)
                    {
                        DateTime day = dueDate.SelectedDate.HasValue ? dueDate.SelectedDate.Value : DateTime.Now.Date;
                        working.DueAt = IsoTime.Format(dueTime.Compose(day));
                    }
                    else
                    {
                        working.DueAt = "";
                    }
                    double weight;
                    if (double.TryParse(weightBox.Text, out weight) && weight > 0) { working.Weight = weight; }
                    working.NoteMarkdown = markdown.Text;
                    AppState.Repo.SaveMilestone(working);
                    AppState.Commit();
                    return true;
                }, false);
        }

        // ---------------------------------------------------------------- events
        private UIElement EventsPanel(Competition competition)
        {
            StackPanel panel = new StackPanel();
            panel.Margin = new Thickness(2, 12, 2, 2);

            Button add = UiKit.Tonal("+ " + Loc.T("comp.event_new"), delegate { ShowEventDialog(competition, null); });
            add.HorizontalAlignment = HorizontalAlignment.Left;
            panel.Children.Add(add);

            if (competition.Events.Count == 0)
            {
                panel.Children.Add(UiKit.Empty("calendar", Loc.T("comp.no_events"), Loc.T("comp.event_title")));
                return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            }

            List<CompetitionEvent> events = new List<CompetitionEvent>(competition.Events);
            events.Sort(delegate(CompetitionEvent a, CompetitionEvent b) { return a.StartDate.CompareTo(b.StartDate); });
            for (int i = 0; i < events.Count; i++)
            {
                CompetitionEvent entry = events[i];
                Border card = new Border();
                card.Style = UiKit.Style("RowCard");
                Grid grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                Border kind = UiKit.Badge(Loc.T("comp.event_kind_" + entry.Kind), null, UiKit.Brush("Brush.Primary"));
                kind.VerticalAlignment = VerticalAlignment.Center;
                grid.Children.Add(kind);

                StackPanel text = new StackPanel();
                text.Margin = new Thickness(11, 0, 10, 0);
                text.Children.Add(UiKit.Body(entry.Title));
                string meta = Services.CalendarService.FormatDateTime(entry.StartDate) +
                    (string.IsNullOrEmpty(entry.Location) ? "" : " · " + entry.Location);
                text.Children.Add(UiKit.Caption(meta));
                if (!string.IsNullOrEmpty(entry.Detail))
                {
                    TextBlock detail = UiKit.Caption(entry.Detail);
                    detail.TextTrimming = TextTrimming.CharacterEllipsis;
                    text.Children.Add(detail);
                }
                Grid.SetColumn(text, 1);
                grid.Children.Add(text);

                StackPanel actions = new StackPanel();
                actions.Orientation = Orientation.Horizontal;
                actions.Children.Add(UiKit.IconButton("edit", Loc.T("common.edit"), delegate { ShowEventDialog(competition, entry); }));
                actions.Children.Add(UiKit.IconButton("trash", Loc.T("common.delete"), delegate
                {
                    if (!UiKit.Confirm(_shell.Window, Loc.T("common.delete"), entry.Title)) { return; }
                    AppState.Repo.DeleteEvent(entry.Id);
                    AppState.Commit();
                }));
                Grid.SetColumn(actions, 2);
                grid.Children.Add(actions);

                card.Child = grid;
                panel.Children.Add(card);
            }
            return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private void ShowEventDialog(Competition competition, CompetitionEvent existing)
        {
            bool isNew = existing == null;
            CompetitionEvent working = isNew ? new CompetitionEvent() : existing;
            if (isNew) { working.CompetitionId = competition.Id; }

            TextBox titleBox = UiKit.Input(working.Title, Loc.T("comp.event_title"));
            TextBox locationBox = UiKit.Input(working.Location, Loc.T("comp.event_location"));
            TextBox detailBox = UiKit.Input(working.Detail, Loc.T("common.optional"));

            ComboBox kindCombo = UiKit.Combo(new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("registration", Loc.T("comp.event_kind_registration")),
                new KeyValuePair<string, string>("event", Loc.T("comp.event_kind_event")),
                new KeyValuePair<string, string>("submission", Loc.T("comp.event_kind_submission")),
                new KeyValuePair<string, string>("result", Loc.T("comp.event_kind_result")),
                new KeyValuePair<string, string>("other", Loc.T("comp.event_kind_other"))
            }, working.Kind);

            DateTime seed = isNew ? DateTime.Now.Date.AddDays(7).AddHours(9) : working.StartDate;
            System.Windows.Controls.DatePicker date = new System.Windows.Controls.DatePicker();
            date.SelectedDate = seed.Date;
            date.Style = UiKit.Style("DatePicker");
            TimeField time = new TimeField(seed, 15);

            Border previewHost = new Border();
            previewHost.Padding = new Thickness(12);
            previewHost.CornerRadius = new CornerRadius(11);
            previewHost.Background = UiKit.Brush("Brush.SurfaceContainer");
            previewHost.BorderThickness = new Thickness(1);
            previewHost.BorderBrush = UiKit.Brush("Brush.OutlineVariant");
            MarkdownEditor markdown = new MarkdownEditor(working.NoteMarkdown, previewHost, true);

            StackPanel content = new StackPanel();
            content.Children.Add(UiKit.Field(Loc.T("comp.event_title"), titleBox, null));
            content.Children.Add(UiKit.Field(Loc.T("comp.event_kind"), kindCombo, null));

            Grid whenRow = new Grid();
            whenRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            whenRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            whenRow.Children.Add(date);
            Grid.SetColumn(time, 1);
            time.Margin = new Thickness(8, 0, 0, 0);
            whenRow.Children.Add(time);
            content.Children.Add(UiKit.Field(Loc.T("common.date"), whenRow, null));

            content.Children.Add(UiKit.Field(Loc.T("comp.event_location"), locationBox, null));
            content.Children.Add(UiKit.Field(Loc.T("common.description"), detailBox, null));
            content.Children.Add(UiKit.SectionHeader(Loc.T("tasks.advanced_record"), null));
            content.Children.Add(markdown);

            UiKit.ShowDialog(_shell.Window, isNew ? Loc.T("comp.event_new") : Loc.T("common.edit"), content,
                Loc.T("common.save"), Loc.T("common.cancel"), delegate
                {
                    string title = (titleBox.Text ?? "").Trim();
                    if (title.Length == 0) { return false; }
                    working.Title = title;
                    working.Kind = UiKit.SelectedKey(kindCombo) ?? "event";
                    working.Location = (locationBox.Text ?? "").Trim();
                    working.Detail = (detailBox.Text ?? "").Trim();
                    working.CompetitionId = competition.Id;
                    DateTime day = date.SelectedDate.HasValue ? date.SelectedDate.Value : DateTime.Now.Date;
                    working.StartAt = IsoTime.Format(time.Compose(day));
                    working.NoteMarkdown = markdown.Text;
                    AppState.Repo.SaveEvent(working);
                    AppState.Commit();
                    return true;
                }, false);
        }

        // ---------------------------------------------------------------- work log
        private UIElement WorkLogPanel(Competition competition)
        {
            StackPanel panel = new StackPanel();
            panel.Margin = new Thickness(2, 12, 2, 2);
            panel.Children.Add(UiKit.Caption(Loc.T("comp.worklog") + " · " + Loc.T("tasks.markdown_hint")));

            Border previewHost = new Border();
            previewHost.Padding = new Thickness(12);
            previewHost.CornerRadius = new CornerRadius(11);
            previewHost.Background = UiKit.Brush("Brush.SurfaceContainer");
            previewHost.BorderThickness = new Thickness(1);
            previewHost.BorderBrush = UiKit.Brush("Brush.OutlineVariant");

            MarkdownEditor markdown = new MarkdownEditor(competition.NoteMarkdown, previewHost, true);
            panel.Children.Add(markdown);

            Button save = UiKit.Primary(Loc.T("common.save"), delegate
            {
                competition.NoteMarkdown = markdown.Text;
                AppState.Repo.SaveCompetition(competition);
                AppState.Commit();
                _shell.SetStatus(Loc.T("common.saved"));
            });
            save.HorizontalAlignment = HorizontalAlignment.Left;
            save.Margin = new Thickness(0, 12, 0, 0);
            panel.Children.Add(save);

            return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        // ---------------------------------------------------------------- files
        private UIElement FilesPanel(Competition competition)
        {
            StackPanel panel = new StackPanel();
            panel.Margin = new Thickness(2, 12, 2, 2);

            StackPanel headerRow = new StackPanel();
            headerRow.Orientation = Orientation.Horizontal;
            Button importFiles = UiKit.Tonal("+ " + Loc.T("tasks.import_file"), delegate
            {
                string[] files = Dialogs.PickFiles(_shell.Window, Loc.T("tasks.import_file"), null, true);
                if (files.Length == 0) { return; }
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        AppState.Repo.ImportAttachment(AttachmentOwner.Competition, competition.Id, files[i],
                            Loc.T("comp.files"), null);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(_shell.Window, ex.Message, Loc.T("error.generic"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                AppState.Commit();
            });
            headerRow.Children.Add(importFiles);

            Button importFolder = UiKit.Outlined("+ " + Loc.T("tasks.import_folder"), delegate
            {
                string folder = Dialogs.PickFolder(_shell.Window, Loc.T("tasks.import_folder"), null);
                if (string.IsNullOrEmpty(folder)) { return; }
                try
                {
                    AppState.Repo.ImportAttachment(AttachmentOwner.Competition, competition.Id, folder, Loc.T("comp.files"), null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(_shell.Window, ex.Message, Loc.T("error.generic"), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                AppState.Commit();
            });
            importFolder.Margin = new Thickness(8, 0, 0, 0);
            headerRow.Children.Add(importFolder);

            Button packAndGo = UiKit.Outlined(Loc.T("comp.pack_and_go"), delegate { PackAndGo(competition); });
            packAndGo.Margin = new Thickness(8, 0, 0, 0);
            ToolTipService.SetToolTip(packAndGo, Loc.T("comp.pack_and_go_hint"));
            headerRow.Children.Add(packAndGo);
            panel.Children.Add(headerRow);

            TextBlock hint = UiKit.Caption(Loc.T("comp.pack_and_go_hint"));
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(0, 8, 0, 8);
            panel.Children.Add(hint);

            if (competition.Attachments.Count == 0)
            {
                panel.Children.Add(UiKit.Empty("folder", Loc.T("comp.no_files"), Loc.T("comp.pack_and_go_hint")));
            }
            for (int i = 0; i < competition.Attachments.Count; i++)
            {
                Attachment attachment = competition.Attachments[i];
                Border card = new Border();
                card.Style = UiKit.Style("RowCard");
                Grid grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                grid.Children.Add(UiKit.Icon(attachment.IsFolder ? "folder" : "file", 16, UiKit.Brush("Brush.OnSurfaceVariant")));
                StackPanel text = new StackPanel();
                text.Margin = new Thickness(10, 0, 8, 0);
                text.Children.Add(UiKit.Body(attachment.FileName));
                text.Children.Add(UiKit.Caption(string.IsNullOrEmpty(attachment.Category) ? attachment.DisplaySize
                    : attachment.Category + " · " + attachment.DisplaySize));
                Grid.SetColumn(text, 1);
                grid.Children.Add(text);

                StackPanel actions = new StackPanel();
                actions.Orientation = Orientation.Horizontal;
                actions.Children.Add(UiKit.IconButton("eye", Loc.T("common.open"), delegate
                {
                    try { AppState.Repo.OpenAttachment(attachment); }
                    catch (Exception ex)
                    {
                        MessageBox.Show(_shell.Window, ex.Message, Loc.T("error.generic"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }));
                actions.Children.Add(UiKit.IconButton("edit", Loc.T("common.rename"), delegate
                {
                    string name = Dialogs.PromptText(_shell.Window, Loc.T("comp.files"),
                        Loc.T("tasks.rename_file_prompt"), System.IO.Path.GetFileNameWithoutExtension(attachment.FileName));
                    if (string.IsNullOrEmpty(name)) { return; }
                    try
                    {
                        AppState.Repo.RenameAttachment(attachment.Id, name, !attachment.IsFolder);
                        AppState.Commit();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(_shell.Window, ex.Message, Loc.T("error.generic"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }));
                actions.Children.Add(UiKit.IconButton("trash", Loc.T("common.delete"), delegate
                {
                    if (!UiKit.Confirm(_shell.Window, Loc.T("common.delete"), attachment.FileName)) { return; }
                    AppState.Repo.DeleteAttachment(attachment.Id, true);
                    AppState.Commit();
                }));
                Grid.SetColumn(actions, 2);
                grid.Children.Add(actions);

                card.Child = grid;
                panel.Children.Add(card);
            }
            return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private void OpenAttachmentFolder(Competition competition)
        {
            try
            {
                string folder = AppState.Session.ResolveRelative("attachments/competition/" + competition.Id);
                System.IO.Directory.CreateDirectory(folder);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(_shell.Window, ex.Message, Loc.T("error.generic"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// "Pack and go": write a self contained zip of one competition (files + a markdown brief)
        /// so a team mate or another device can pick the project up without the whole container.
        /// </summary>
        private void PackAndGo(Competition competition)
        {
            try
            {
                string folder = Dialogs.PickFolder(_shell.Window, Loc.T("comp.pack_and_go"), AppState.SessionOrNull.Root);
                if (string.IsNullOrEmpty(folder)) { return; }
                string target = Services.CompetitionPackService.Pack(AppState.SessionOrNull, competition, folder);
                _shell.SetStatus(Loc.T("backup.export_done", System.IO.Path.GetFileName(target)));
                UiKit.Message(_shell.Window, Loc.T("comp.pack_and_go"), Loc.T("backup.export_done", System.IO.Path.GetFileName(target)) + "\r\n\r\n" + target);
            }
            catch (Exception ex)
            {
                MessageBox.Show(_shell.Window, ex.Message, Loc.T("error.export_failed", ex.Message), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ---------------------------------------------------------------- linked todos
        private UIElement LinkedTodosPanel(Competition competition)
        {
            StackPanel panel = new StackPanel();
            panel.Margin = new Thickness(2, 12, 2, 2);
            panel.Children.Add(UiKit.Caption(Loc.T("comp.linked_todos") + " · " + Loc.T("comp.tag_hint")));

            List<Todo> linked = new List<Todo>();
            for (int i = 0; i < AppState.Data.Todos.Count; i++)
            {
                if (TaskQuery.ReferencesCompetition(AppState.Data.Todos[i], competition.Id))
                {
                    linked.Add(AppState.Data.Todos[i]);
                }
            }
            TaskQuery.Sort(linked, "dueAsc");

            Button add = UiKit.Tonal("+ " + Loc.T("comp.link_todo"), delegate
            {
                TaskEditor.ShowCreate(_shell, null);
            });
            add.HorizontalAlignment = HorizontalAlignment.Left;
            add.Margin = new Thickness(0, 8, 0, 8);
            panel.Children.Add(add);

            if (linked.Count == 0)
            {
                panel.Children.Add(UiKit.Empty("tasks", Loc.T("comp.no_linked_todos"), Loc.T("comp.tag_hint")));
            }
            DateTime now = DateTime.Now;
            for (int i = 0; i < linked.Count; i++)
            {
                Todo todo = linked[i];
                Border card = new Border();
                card.Style = UiKit.Style("RowCard");
                Grid grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                CheckBox check = new CheckBox();
                check.Style = UiKit.Style("CheckBox.Round");
                check.IsChecked = todo.IsDone;
                check.VerticalAlignment = VerticalAlignment.Center;
                check.Click += delegate(object sender, RoutedEventArgs e)
                {
                    AppState.Repo.SetTodoStatus(todo.Id, ((CheckBox)sender).IsChecked == true ? TodoStatus.Done : TodoStatus.Open);
                    AppState.Commit();
                };
                grid.Children.Add(check);

                StackPanel text = new StackPanel();
                text.Margin = new Thickness(10, 0, 8, 0);
                TextBlock title = UiKit.Body(todo.Title);
                if (todo.IsDone) { title.TextDecorations = TextDecorations.Strikethrough; title.Opacity = 0.65; }
                text.Children.Add(title);
                text.Children.Add(UiKit.Caption(todo.DueDate.HasValue
                    ? Loc.T("timeline.deadline") + ": " + Services.CalendarService.FormatDateTime(todo.DueDate.Value)
                    : Loc.T("common.no_deadline")));
                Grid.SetColumn(text, 1);
                grid.Children.Add(text);

                Button open = UiKit.TextButton(Loc.T("common.edit"), delegate { _shell.Navigate("tasks", todo.Id); });
                Grid.SetColumn(open, 2);
                grid.Children.Add(open);

                card.Child = grid;
                panel.Children.Add(card);
            }
            return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        // ---------------------------------------------------------------- delete
        private void DeleteCompetition(Competition competition)
        {
            int linked = 0;
            for (int i = 0; i < AppState.Data.Todos.Count; i++)
            {
                if (TaskQuery.ReferencesCompetition(AppState.Data.Todos[i], competition.Id)) { linked++; }
            }

            bool deleteTodos = false;
            if (linked > 0)
            {
                StackPanel content = new StackPanel();
                TextBlock prompt = UiKit.Body(Loc.T("comp.delete_confirm", competition.Name, linked));
                prompt.TextWrapping = TextWrapping.Wrap;
                content.Children.Add(prompt);
                CheckBox box = new CheckBox();
                box.Content = Loc.T("comp.delete_linked_todos");
                box.Margin = new Thickness(0, 10, 0, 0);
                content.Children.Add(box);
                if (!UiKit.ShowDialog(_shell.Window, Loc.T("common.delete"), content, Loc.T("common.delete"),
                        Loc.T("common.cancel"), delegate
                        {
                            deleteTodos = box.IsChecked == true;
                            return true;
                        }, true))
                {
                    return;
                }
            }
            else if (AppState.Settings.ConfirmDelete &&
                     !UiKit.Confirm(_shell.Window, Loc.T("common.delete"), competition.Name + "\r\n" + Loc.T("common.are_you_sure")))
            {
                return;
            }

            AppState.Repo.DeleteCompetition(competition.Id, deleteTodos);
            _selectedId = null;
            AppState.Commit();
        }
    }
}
