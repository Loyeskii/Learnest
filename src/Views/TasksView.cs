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
    /// ToDos and Deadlines.  Each entry is a single line by default (title + deadline + urgency);
    /// everything richer lives one click away in the advanced record editor.  The toolbar covers
    /// the quick add box, status views, category filter, search, sorting and completion.
    /// </summary>
    internal sealed class TasksView : UserControl, IRefreshable
    {
        private readonly IShell _shell;
        private readonly StackPanel _list;
        private readonly TextBlock _summaryText;
        private readonly TextBox _quickAdd;
        private readonly ComboBox _categoryCombo;
        private readonly ComboBox _statusCombo;
        private readonly ComboBox _sortCombo;
        private readonly ComboBox _competitionCombo;
        private readonly CheckBox _showCompleted;
        private readonly StackPanel _toolbarChips;

        private string _status = "open";
        private string _sort = "dueAsc";
        private string _highlightId;

        public TasksView(IShell shell)
        {
            _shell = shell;

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ---------------- quick add
            Border quickCard = new Border();
            quickCard.Style = UiKit.Style("Card");
            quickCard.Margin = new Thickness(22, 16, 22, 0);

            Grid quickGrid = new Grid();
            quickGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            quickGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            quickGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _quickAdd = UiKit.Input("", Loc.T("tasks.quick_add_placeholder"));
            _quickAdd.FontSize = 14;
            _quickAdd.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) { QuickAdd(false); }
                else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { QuickAdd(true); }
            };
            quickGrid.Children.Add(_quickAdd);

            Button add = UiKit.Primary("+ " + Loc.T("common.add"), delegate { QuickAdd(false); });
            add.Margin = new Thickness(8, 0, 0, 0);
            add.MinWidth = 96;
            Grid.SetColumn(add, 1);
            quickGrid.Children.Add(add);

            Button advanced = UiKit.Outlined(Loc.T("tasks.advanced_record"), delegate
            {
                string title = (_quickAdd.Text ?? "").Trim();
                if (title.Length == 0) { _shell.SetStatus(Loc.T("tasks.quick_add_placeholder")); return; }
                Todo draft = new Todo();
                draft.Title = title;
                draft.CategoryId = _shell.ActiveCategoryId ?? Category.CreateUncategorized().Id;
                TaskEditor.ShowCreate(_shell, _shell.ActiveCategoryId);
            });
            advanced.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(advanced, 2);
            quickGrid.Children.Add(advanced);

            quickCard.Child = quickGrid;
            Grid.SetRow(quickCard, 0);
            root.Children.Add(quickCard);

            // ---------------- toolbar
            StackPanel toolbar = new StackPanel();
            toolbar.Margin = new Thickness(22, 12, 22, 6);

            Grid headerRow = new Grid();
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _summaryText = UiKit.Text("", "Text.Subtitle");
            headerRow.Children.Add(_summaryText);
            _toolbarChips = new StackPanel();
            _toolbarChips.Orientation = Orientation.Horizontal;
            Grid.SetColumn(_toolbarChips, 1);
            headerRow.Children.Add(_toolbarChips);
            toolbar.Children.Add(headerRow);

            WrapPanel filters = new WrapPanel();
            filters.Margin = new Thickness(0, 8, 0, 0);

            List<KeyValuePair<string, string>> categoryChoices = new List<KeyValuePair<string, string>>();
            categoryChoices.Add(new KeyValuePair<string, string>("", Loc.T("common.all") + " · " + Loc.T("common.categories")));
            for (int i = 0; i < AppState.Data.Categories.Count; i++)
            {
                Category category = AppState.Data.Categories[i];
                categoryChoices.Add(new KeyValuePair<string, string>(category.Id, category.Name));
            }
            _categoryCombo = UiKit.Combo(categoryChoices, null);
            _categoryCombo.Width = 150;
            _categoryCombo.SelectionChanged += delegate { Refresh(); };
            filters.Children.Add(WithLabel(Loc.T("common.category"), _categoryCombo));

            _statusCombo = UiKit.Combo(new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("open", Loc.T("tasks.view_open")),
                new KeyValuePair<string, string>("today", Loc.T("tasks.view_today")),
                new KeyValuePair<string, string>("week", Loc.T("tasks.view_week")),
                new KeyValuePair<string, string>("overdue", Loc.T("tasks.view_overdue")),
                new KeyValuePair<string, string>("no_deadline", Loc.T("common.no_deadline")),
                new KeyValuePair<string, string>("done", Loc.T("tasks.view_done")),
                new KeyValuePair<string, string>("all", Loc.T("tasks.view_all"))
            }, _status);
            _statusCombo.Width = 130;
            _statusCombo.SelectionChanged += delegate
            {
                _status = UiKit.SelectedKey(_statusCombo) ?? "open";
                Refresh();
            };
            filters.Children.Add(WithLabel(Loc.T("common.status"), _statusCombo));

            _sortCombo = UiKit.Combo(new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("dueAsc", Loc.T("tasks.sort_due_asc")),
                new KeyValuePair<string, string>("dueDesc", Loc.T("tasks.sort_due_desc")),
                new KeyValuePair<string, string>("modifiedDesc", Loc.T("tasks.sort_modified_desc")),
                new KeyValuePair<string, string>("modifiedAsc", Loc.T("tasks.sort_modified_asc")),
                new KeyValuePair<string, string>("createdDesc", Loc.T("tasks.sort_created_desc")),
                new KeyValuePair<string, string>("createdAsc", Loc.T("tasks.sort_created_asc")),
                new KeyValuePair<string, string>("priority", Loc.T("tasks.sort_priority")),
                new KeyValuePair<string, string>("title", Loc.T("tasks.sort_title"))
            }, _sort);
            _sortCombo.Width = 176;
            _sortCombo.SelectionChanged += delegate
            {
                _sort = UiKit.SelectedKey(_sortCombo) ?? "dueAsc";
                Refresh();
            };
            filters.Children.Add(WithLabel(Loc.T("common.sort"), _sortCombo));

            List<KeyValuePair<string, string>> competitionChoices = new List<KeyValuePair<string, string>>();
            competitionChoices.Add(new KeyValuePair<string, string>("", Loc.T("common.all") + " · " + Loc.T("comp.title")));
            for (int i = 0; i < AppState.Data.Competitions.Count; i++)
            {
                Competition competition = AppState.Data.Competitions[i];
                competitionChoices.Add(new KeyValuePair<string, string>(competition.Id, competition.Name));
            }
            _competitionCombo = UiKit.Combo(competitionChoices, null);
            _competitionCombo.Width = 170;
            _competitionCombo.SelectionChanged += delegate { Refresh(); };
            filters.Children.Add(WithLabel(Loc.T("comp.title"), _competitionCombo));

            _showCompleted = new CheckBox();
            _showCompleted.Content = Loc.T("timeline.show_completed");
            _showCompleted.Margin = new Thickness(8, 14, 0, 0);
            _showCompleted.Checked += delegate { Refresh(); };
            _showCompleted.Unchecked += delegate { Refresh(); };
            filters.Children.Add(_showCompleted);

            toolbar.Children.Add(filters);
            Grid.SetRow(toolbar, 1);
            root.Children.Add(toolbar);

            // ---------------- list
            ScrollViewer scroller = new ScrollViewer();
            scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroller.Padding = new Thickness(22, 0, 22, 8);
            _list = new StackPanel();
            scroller.Content = _list;
            Grid.SetRow(scroller, 2);
            root.Children.Add(scroller);

            // ---------------- status bar
            Border footer = new Border();
            footer.Padding = new Thickness(22, 6, 22, 8);
            StackPanel footerRow = new StackPanel();
            footerRow.Orientation = Orientation.Horizontal;
            Button exportCsv = UiKit.TextButton(Loc.T("export.todos_csv"), delegate { ExportCsv(); });
            footerRow.Children.Add(exportCsv);
            Button newCategory = UiKit.TextButton("+ " + Loc.T("tasks.category_new"), delegate
            {
                CategoryEditor.ShowCreate(_shell, delegate { Refresh(); });
            });
            footerRow.Children.Add(newCategory);
            footer.Child = footerRow;
            Grid.SetRow(footer, 3);
            root.Children.Add(footer);

            Content = root;
            Refresh();
        }

        private static UIElement WithLabel(string label, UIElement editor)
        {
            StackPanel stack = new StackPanel();
            stack.Margin = new Thickness(0, 0, 12, 8);
            TextBlock caption = UiKit.Label(label);
            caption.Margin = new Thickness(2, 0, 0, 4);
            stack.Children.Add(caption);
            stack.Children.Add(editor);
            return stack;
        }

        // ================================================================ actions
        private void QuickAdd(bool openAdvanced)
        {
            string title = (_quickAdd.Text ?? "").Trim();
            if (title.Length == 0) { return; }
            Todo todo = new Todo();
            todo.Title = title;
            todo.CategoryId = _shell.ActiveCategoryId ?? Category.CreateUncategorized().Id;
            todo.CreatedAt = IsoTime.Now();
            todo.LeadMinutes = 1440;
            if (!openAdvanced)
            {
                // Default deadline: today 23:59, which is what a student almost always wants.
                DateTime today = DateTime.Now.Date.AddHours(23).AddMinutes(59);
                if (DateTime.Now > today) { today = today.AddDays(1); }
                todo.DueAt = IsoTime.Format(today);
            }
            AppState.Repo.SaveTodo(todo, Loc.T("tasks.new_todo") + ": " + todo.Title);
            AppState.Commit();
            _quickAdd.Text = "";
            _quickAdd.Focus();
            _shell.SetStatus(Loc.T("common.saved") + " · " + todo.Title);
            if (openAdvanced)
            {
                Todo persisted = AppState.Repo.FindTodo(todo.Id);
                if (persisted != null) { TaskEditor.ShowAdvanced(_shell, persisted); }
            }
        }

        private void ExportCsv()
        {
            if (AppState.SessionOrNull == null) { return; }
            string folder = Dialogs.PickFolder(_shell.Window, Loc.T("backup.export_target"), AppState.SessionOrNull.Root);
            if (string.IsNullOrEmpty(folder)) { return; }
            try
            {
                string path = BackupService.ExportTodosCsv(AppState.SessionOrNull, folder, AppState.Data.Todos);
                _shell.SetStatus(Loc.T("backup.export_done", path));
            }
            catch (Exception ex)
            {
                MessageBox.Show(_shell.Window, ex.Message, Loc.T("error.generic"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ================================================================ rendering
        public void Refresh()
        {
            _list.Children.Clear();
            _toolbarChips.Children.Clear();

            string argument = _shell.TakeArgument();
            if (!string.IsNullOrEmpty(argument)) { _highlightId = argument; }

            TaskFilter filter = new TaskFilter();
            filter.Status = _status;
            filter.CategoryId = !string.IsNullOrEmpty(_shell.ActiveCategoryId)
                ? _shell.ActiveCategoryId : UiKit.SelectedKey(_categoryCombo);
            filter.CompetitionId = UiKit.SelectedKey(_competitionCombo);
            filter.Search = _shell.SearchText;

            DateTime now = DateTime.Now;
            bool showCompleted = _showCompleted.IsChecked.GetValueOrDefault() ||
                                 string.Equals(_status, "done", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(_status, "all", StringComparison.OrdinalIgnoreCase);
            List<Todo> todos = new List<Todo>();
            for (int i = 0; i < AppState.Data.Todos.Count; i++)
            {
                Todo todo = AppState.Data.Todos[i];
                // Completed items stay hidden until the user asks for them.
                if (todo.IsDone && !showCompleted) { continue; }
                if (TaskQuery.Matches(todo, filter, now)) { todos.Add(todo); }
            }
            TaskQuery.Sort(todos, _sort);

            int overdue = 0;
            for (int i = 0; i < todos.Count; i++)
            {
                if (TaskQuery.UrgencyOf(todos[i], now) == Urgency.Overdue) { overdue++; }
            }
            string scope = filter.CategoryId != null
                ? AppState.Data.NameOf(filter.CategoryId) : Loc.T("common.all");
            _summaryText.Text = Loc.T("tasks.title") + " · " + scope + " · " + todos.Count + " " + Loc.T("common.items") +
                                (overdue > 0 ? "  (" + Loc.T("common.overdue") + " " + overdue + ")" : "");

            Button scopeButton = UiKit.TextButton(Loc.T("common.filter"), null);
            scopeButton.IsEnabled = false;
            if (!string.IsNullOrEmpty(_shell.ActiveCategoryId))
            {
                Button clear = UiKit.TextButton(Loc.T("common.reset") + " · " + Loc.T("common.category"),
                    delegate { _shell.ClearCategoryFilter(); });
                _toolbarChips.Children.Add(clear);
            }
            _toolbarChips.Children.Add(scopeButton);

            if (todos.Count == 0)
            {
                _list.Children.Add(UiKit.Empty("tasks", Loc.T("tasks.empty_title"), Loc.T("tasks.empty_hint")));
                return;
            }

            // Group by category when no explicit category filter is active.
            if (string.IsNullOrEmpty(filter.CategoryId) && string.IsNullOrEmpty(filter.Search) &&
                (_sort == "dueAsc" || _sort == "priority"))
            {
                List<TaskQuery.CategoryCount> breakdown = TaskQuery.CategoryBreakdown(now);
                HashSet<string> rendered = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < breakdown.Count; i++)
                {
                    Category category = breakdown[i].Category;
                    List<Todo> bucket = new List<Todo>();
                    for (int j = 0; j < todos.Count; j++)
                    {
                        if (todos[j].CategoryId == category.Id) { bucket.Add(todos[j]); }
                    }
                    if (bucket.Count == 0) { continue; }
                    for (int j = 0; j < bucket.Count; j++) { rendered.Add(bucket[j].Id); }
                    _list.Children.Add(GroupHeader(category));
                    for (int j = 0; j < bucket.Count; j++) { _list.Children.Add(Row(bucket[j], now)); }
                }
                List<Todo> leftovers = new List<Todo>();
                for (int i = 0; i < todos.Count; i++)
                {
                    if (!rendered.Contains(todos[i].Id)) { leftovers.Add(todos[i]); }
                }
                if (leftovers.Count > 0)
                {
                    Category fallback = new Category();
                    fallback.Id = "";
                    fallback.Name = Loc.T("tasks.no_deadline_group");
                    fallback.Color = Category.UncategorizedColor;
                    _list.Children.Add(GroupHeader(fallback));
                    for (int j = 0; j < leftovers.Count; j++) { _list.Children.Add(Row(leftovers[j], now)); }
                }
                return;
            }

            for (int i = 0; i < todos.Count; i++) { _list.Children.Add(Row(todos[i], now)); }
        }

        private UIElement GroupHeader(Category category)
        {
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Margin = new Thickness(0, 14, 0, 6);

            grid.Children.Add(UiKit.Dot(category.Color, 10));
            TextBlock name = UiKit.Text(category.Name, "Text.Subtitle");
            name.FontSize = 14;
            name.Margin = new Thickness(9, 0, 0, 0);
            Grid.SetColumn(name, 1);
            grid.Children.Add(name);
            return grid;
        }

        private UIElement Row(Todo todo, DateTime now)
        {
            bool highlighted = !string.IsNullOrEmpty(_highlightId) && _highlightId == todo.Id;
            Urgency urgency = TaskQuery.UrgencyOf(todo, now);
            bool reminding = TaskQuery.IsReminding(todo, now);

            Border card = new Border();
            card.Style = UiKit.Style("RowCard");
            if (highlighted)
            {
                card.BorderBrush = UiKit.Brush("Brush.Primary");
                card.BorderThickness = new Thickness(2);
            }
            else if (reminding)
            {
                // Inside the reminder window: a red gradient border plus a light tint.  Static by
                // design - the reminder is a persistent visual state, not a flashing one.
                card.BorderBrush = UiKit.AlertBorderBrush();
                card.BorderThickness = new Thickness(2);
                card.Background = UiKit.AlertTintBrush();
            }

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Completion checkbox
            CheckBox check = new CheckBox();
            check.Style = UiKit.Style("CheckBox.Round");
            check.IsChecked = todo.IsDone;
            check.VerticalAlignment = VerticalAlignment.Center;
            check.Tag = todo.Id;
            ToolTipService.SetToolTip(check, todo.IsDone ? Loc.T("tasks.mark_incomplete") : Loc.T("tasks.mark_complete"));
            check.Click += delegate(object sender, RoutedEventArgs e)
            {
                CheckBox box = (CheckBox)sender;
                AppState.Repo.SetTodoStatus((string)box.Tag, box.IsChecked == true ? TodoStatus.Done : TodoStatus.Open);
                AppState.Commit();
            };
            grid.Children.Add(check);

            // Title line + meta line
            StackPanel text = new StackPanel();
            text.Margin = new Thickness(11, 0, 10, 0);

            TextBlock title = UiKit.Text(todo.Title, "Text.Body");
            title.FontWeight = todo.Priority >= 2 ? FontWeights.SemiBold : FontWeights.Normal;
            if (todo.IsDone)
            {
                title.TextDecorations = TextDecorations.Strikethrough;
                title.Opacity = 0.6;
            }
            text.Children.Add(title);

            StackPanel meta = new StackPanel();
            meta.Orientation = Orientation.Horizontal;

            Category category = AppState.Data.CategoryOrUncategorized(todo.CategoryId);
            if (category != null)
            {
                meta.Children.Add(UiKit.Dot(category.Color, 8));
                TextBlock categoryName = UiKit.Caption(category.Name);
                categoryName.Margin = new Thickness(6, 0, 10, 0);
                meta.Children.Add(categoryName);
            }
            if (todo.DueDate.HasValue)
            {
                TextBlock due = UiKit.Caption(Services.CalendarService.FormatDateTime(todo.DueDate.Value) + " · " +
                    Services.CalendarService.DescribeDistance(todo.DueDate.Value, now));
                due.Foreground = UiKit.Brush(UrgencyBrushKey(urgency));
                meta.Children.Add(due);
            }
            else
            {
                meta.Children.Add(UiKit.Caption(Loc.T("common.no_deadline")));
            }
            if (todo.IsDone && todo.CompletedDate.HasValue)
            {
                TextBlock done = UiKit.Caption(" · " +
                    Loc.T("tasks.completed_at", Services.CalendarService.FormatDateTime(todo.CompletedDate.Value)));
                meta.Children.Add(done);
            }
            if (todo.Attachments.Count > 0)
            {
                StackPanel paperclip = new StackPanel();
                paperclip.Orientation = Orientation.Horizontal;
                paperclip.Margin = new Thickness(10, 0, 0, 0);
                paperclip.Children.Add(UiKit.Icon("paperclip", 11, UiKit.Brush("Brush.OnSurfaceFaint")));
                TextBlock count = UiKit.Caption(" " + todo.Attachments.Count);
                paperclip.Children.Add(count);
                meta.Children.Add(paperclip);
            }
            if (todo.HasNote)
            {
                StackPanel note = new StackPanel();
                note.Orientation = Orientation.Horizontal;
                note.Margin = new Thickness(10, 0, 0, 0);
                note.Children.Add(UiKit.Icon("note", 11, UiKit.Brush("Brush.OnSurfaceFaint")));
                meta.Children.Add(note);
            }
            for (int i = 0; i < todo.CompetitionIds.Count; i++)
            {
                Competition competition = AppState.Data.FindCompetition(todo.CompetitionIds[i]) ??
                                          AppState.Data.FindCompetitionByTag(todo.CompetitionIds[i]);
                if (competition == null) { continue; }
                Border chip = UiKit.Badge(competition.Name, UiKit.Alpha(ColorUtil.Parse(competition.Color, Colors.Gray), 0.18),
                    new SolidColorBrush(ColorUtil.Parse(competition.Color, Colors.Gray)));
                chip.Margin = new Thickness(10, 0, 0, 0);
                meta.Children.Add(chip);
            }
            text.Children.Add(meta);
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            // Urgency badge
            if (todo.DueDate.HasValue && !todo.IsDone)
            {
                Border badge = UiKit.Badge(TaskQuery.UrgencyLabel(urgency), null, UiKit.Brush(UrgencyBrushKey(urgency)));
                badge.VerticalAlignment = VerticalAlignment.Center;
                badge.Margin = new Thickness(0, 0, 10, 0);
                Grid.SetColumn(badge, 2);
                grid.Children.Add(badge);
            }

            // Row actions
            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.VerticalAlignment = VerticalAlignment.Center;

            Button advanced = UiKit.IconButton("markdown", Loc.T("tasks.advanced_record"), delegate
            {
                Todo fresh = AppState.Repo.FindTodo(todo.Id);
                if (fresh != null) { TaskEditor.ShowAdvanced(_shell, fresh); }
            });
            actions.Children.Add(advanced);

            Button edit = UiKit.IconButton("edit", Loc.T("common.edit"), delegate
            {
                Todo fresh = AppState.Repo.FindTodo(todo.Id);
                if (fresh != null) { TaskEditor.ShowEdit(_shell, fresh); }
            });
            actions.Children.Add(edit);

            Button remove = UiKit.IconButton("trash", Loc.T("common.delete"), delegate
            {
                if (AppState.Settings.ConfirmDelete &&
                    !UiKit.Confirm(_shell.Window, Loc.T("tasks.delete_todo"), todo.Title + "\r\n" + Loc.T("common.are_you_sure")))
                {
                    return;
                }
                AppState.Repo.DeleteTodo(todo.Id);
                AppState.Commit();
            });
            actions.Children.Add(remove);

            Grid.SetColumn(actions, 3);
            grid.Children.Add(actions);

            card.Child = grid;
            card.Tag = todo.Id;
            card.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                // Clicking the row (but not a button) toggles the advanced record open.
                if (e.OriginalSource is Button || e.OriginalSource is CheckBox) { return; }
                Todo fresh = AppState.Repo.FindTodo(todo.Id);
                if (fresh != null) { TaskEditor.ShowAdvanced(_shell, fresh); }
            };
            return card;
        }

        private static string UrgencyBrushKey(Urgency urgency)
        {
            switch (urgency)
            {
                case Urgency.Overdue: return "Brush.Error";
                case Urgency.Critical: return "Brush.Error";
                case Urgency.Today: return "Brush.Warning";
                case Urgency.Done: return "Brush.Success";
                case Urgency.Soon: return "Brush.Primary";
                default: return "Brush.OnSurfaceVariant";
            }
        }
    }
}
