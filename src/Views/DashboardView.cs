using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Learnest.Data;
using Learnest.Services;
using Learnest.Theming;

using System.Windows.Shapes;
namespace Learnest.Views
{
    /// <summary>
    /// Overview screen: the counters a student checks first (overdue, due today, this week), the
    /// items inside the deadline alert window, the exam countdown, category distribution and a
    /// short activity log.
    /// </summary>
    internal sealed class DashboardView : UserControl, IRefreshable
    {
        private readonly IShell _shell;
        private readonly StackPanel _root;

        public DashboardView(IShell shell)
        {
            _shell = shell;
            ScrollViewer scroller = new ScrollViewer();
            scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroller.Padding = new Thickness(22, 18, 22, 22);
            _root = new StackPanel();
            scroller.Content = _root;
            Content = scroller;
            Refresh();
        }

        public void Refresh()
        {
            _root.Children.Clear();
            if (AppState.SessionOrNull == null)
            {
                _root.Children.Add(UiKit.Empty("folder", Loc.T("container.none_selected"), Loc.T("container.welcome_body")));
                return;
            }

            DateTime now = DateTime.Now;
            TaskQuery.Summary summary = TaskQuery.Summarise(now);

            _root.Children.Add(Header(now));
            _root.Children.Add(StatRow(summary));
            _root.Children.Add(CompletionCard(summary));

            Grid columns = new Grid();
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            columns.Margin = new Thickness(0, 14, 0, 0);

            StackPanel left = new StackPanel();
            left.Margin = new Thickness(0, 0, 10, 0);
            left.Children.Add(AlertCard(now));
            left.Children.Add(UpcomingCard(now));
            left.Children.Add(ExamsCard(now));
            columns.Children.Add(left);

            StackPanel right = new StackPanel();
            right.Margin = new Thickness(10, 0, 0, 0);
            right.Children.Add(CategoryCard(now));
            right.Children.Add(CompetitionCard());
            right.Children.Add(ActivityCard());
            Grid.SetColumn(right, 1);
            columns.Children.Add(right);

            _root.Children.Add(columns);
        }

        private UIElement Header(DateTime now)
        {
            Grid header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titles = new StackPanel();
            TextBlock greeting = UiKit.Text(Loc.T("nav.dashboard"), "Text.Display");
            titles.Children.Add(greeting);
            TextBlock subtitle = UiKit.Muted(Services.CalendarService.FormatFull(now) + " · " + AppPaths.Tagline);
            subtitle.Margin = new Thickness(0, 2, 0, 0);
            titles.Children.Add(subtitle);
            header.Children.Add(titles);

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.VerticalAlignment = VerticalAlignment.Center;
            Button add = UiKit.Primary("+ " + Loc.T("tasks.new_todo"), delegate
            {
                TaskEditor.ShowCreate(_shell, null);
            });
            actions.Children.Add(add);
            Button widget = UiKit.Outlined(Loc.T("widget.pin"), delegate { _shell.ShowWidget(WidgetWindow.Current == null); });
            widget.Margin = new Thickness(8, 0, 0, 0);
            actions.Children.Add(widget);
            Grid.SetColumn(actions, 1);
            header.Children.Add(actions);
            return header;
        }

        private UIElement StatRow(TaskQuery.Summary summary)
        {
            WrapPanel row = new WrapPanel();
            row.Margin = new Thickness(0, 16, 0, 0);
            row.Children.Add(StatCard(Loc.T("tasks.view_overdue"), summary.Overdue.ToString(), "Brush.Error", "alert"));
            row.Children.Add(StatCard(Loc.T("tasks.due_today"), summary.DueToday.ToString(), "Brush.Warning", "clock"));
            row.Children.Add(StatCard(Loc.T("tasks.due_this_week"), summary.DueThisWeek.ToString(), "Brush.Primary", "calendar"));
            row.Children.Add(StatCard(Loc.T("common.in_progress"), summary.Open.ToString(), "Brush.OnSurfaceVariant", "list"));
            row.Children.Add(StatCard(Loc.T("exams.title"), summary.ExamCount.ToString(), "Brush.Error", "exam"));
            row.Children.Add(StatCard(Loc.T("comp.title"), summary.CompetitionCount.ToString(), "Brush.Tertiary", "trophy"));
            return row;
        }

        private UIElement StatCard(string label, string value, string brushKey, string icon)
        {
            Border card = new Border();
            card.Style = UiKit.Style("Card");
            card.Width = 168;
            card.Margin = new Thickness(0, 0, 10, 10);

            StackPanel stack = new StackPanel();
            StackPanel top = new StackPanel();
            top.Orientation = Orientation.Horizontal;
            top.Children.Add(UiKit.Icon(icon, 15, UiKit.Brush(brushKey)));
            TextBlock caption = UiKit.Label(label);
            caption.Margin = new Thickness(7, 0, 0, 0);
            top.Children.Add(caption);
            stack.Children.Add(top);

            TextBlock number = UiKit.Text(value, "Text.Display");
            number.FontSize = 30;
            number.Foreground = UiKit.Brush(brushKey);
            number.Margin = new Thickness(0, 6, 0, 0);
            stack.Children.Add(number);

            card.Child = stack;
            return card;
        }

        private UIElement CompletionCard(TaskQuery.Summary summary)
        {
            StackPanel stack = new StackPanel();
            Grid header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock title = UiKit.Subtitle(Loc.T("common.progress"));
            header.Children.Add(title);
            TextBlock percent = UiKit.Label((summary.CompletionRate * 100).ToString("0") + "%");
            Grid.SetColumn(percent, 1);
            header.Children.Add(percent);
            stack.Children.Add(header);

            ProgressBar bar = new ProgressBar();
            bar.Minimum = 0;
            bar.Maximum = 1;
            bar.Value = summary.CompletionRate;
            bar.Margin = new Thickness(0, 8, 0, 6);
            stack.Children.Add(bar);

            stack.Children.Add(UiKit.Caption(
                Loc.T("common.done") + " " + summary.DoneTotal + " · " +
                Loc.T("common.in_progress") + " " + summary.Open + " · " +
                Loc.T("common.attachments") + " " + summary.AttachmentCount));

            Border card = UiKit.Card(stack, false);
            card.Margin = new Thickness(0, 4, 0, 0);
            return card;
        }

        private UIElement AlertCard(DateTime now)
        {
            List<TaskQuery.AlertHit> hits = TaskQuery.Alerts(now);
            StackPanel content = new StackPanel();
            content.Children.Add(UiKit.SectionHeader(
                Loc.T("alert.deadline_approaching", hits.Count),
                UiKit.TextButton(Loc.T("widget.filter"), delegate { _shell.Navigate("tasks"); })));

            if (hits.Count == 0)
            {
                TextBlock none = UiKit.Muted(Loc.T("alert.no_alerts"));
                none.Margin = new Thickness(2, 4, 0, 6);
                content.Children.Add(none);
            }
            else
            {
                int limit = Math.Min(hits.Count, 6);
                for (int i = 0; i < limit; i++)
                {
                    content.Children.Add(AlertRow(hits[i], now));
                }
                if (hits.Count > limit)
                {
                    content.Children.Add(UiKit.Caption(Loc.T("common.more") + " " + (hits.Count - limit)));
                }
            }

            Border card = UiKit.Card(content, false);
            card.Margin = new Thickness(0, 0, 0, 12);
            return card;
        }

        private UIElement AlertRow(TaskQuery.AlertHit hit, DateTime now)
        {
            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Margin = new Thickness(0, 3, 0, 3);

            Border dot = UiKit.Dot(hit.Color, 9);
            dot.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(dot);

            StackPanel text = new StackPanel();
            text.Margin = new Thickness(9, 0, 8, 0);
            TextBlock title = UiKit.Body(hit.Title);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            text.Children.Add(title);
            TextBlock sub = UiKit.Caption(hit.Subtitle + " · " + Services.CalendarService.FormatDateTime(hit.Due));
            text.Children.Add(sub);
            Grid.SetColumn(text, 1);
            row.Children.Add(text);

            string label = Services.CalendarService.DescribeDistance(hit.Due, now);
            Border badge = UiKit.Badge(label, UiKit.Alpha(ThemeManager.Palette.UrgencyColor(TaskQuery.UrgencyKey(hit.Urgency)), 0.18),
                UiKit.Brush(UrgencyBrushKey(hit.Urgency)));
            badge.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(badge, 2);
            row.Children.Add(badge);

            row.Cursor = System.Windows.Input.Cursors.Hand;
            row.MouseLeftButtonUp += delegate
            {
                if (hit.Kind == "todo") { _shell.Navigate("tasks", hit.RefId); }
                else if (hit.Kind == "exam") { _shell.Navigate("exams", hit.RefId); }
                else { _shell.Navigate("competitions", hit.CompetitionId); }
            };
            return row;
        }

        private static string UrgencyBrushKey(Urgency urgency)
        {
            switch (urgency)
            {
                case Urgency.Overdue: return "Brush.Error";
                case Urgency.Critical: return "Brush.Error";
                case Urgency.Today: return "Brush.Warning";
                case Urgency.Done: return "Brush.Success";
                default: return "Brush.Primary";
            }
        }

        private UIElement UpcomingCard(DateTime now)
        {
            TaskFilter filter = new TaskFilter();
            filter.Status = "open";
            List<Todo> items = TaskQuery.Apply(AppState.Data.Todos, filter, now);
            StackPanel content = new StackPanel();
            content.Children.Add(UiKit.SectionHeader(Loc.T("tasks.title") + " · " + Loc.T("tasks.view_open"),
                UiKit.TextButton(Loc.T("common.more") + " ›", delegate { _shell.Navigate("tasks"); })));

            if (items.Count == 0)
            {
                content.Children.Add(UiKit.Muted(Loc.T("tasks.empty_hint")));
            }
            else
            {
                int limit = Math.Min(items.Count, 8);
                for (int i = 0; i < limit; i++)
                {
                    content.Children.Add(TaskRow(items[i], now));
                }
            }
            Border card = UiKit.Card(content, false);
            card.Margin = new Thickness(0, 0, 0, 12);
            return card;
        }

        private UIElement TaskRow(Todo todo, DateTime now)
        {
            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            CheckBox check = new CheckBox();
            check.Style = UiKit.Style("CheckBox.Round");
            check.IsChecked = todo.IsDone;
            check.VerticalAlignment = VerticalAlignment.Center;
            check.Tag = todo.Id;
            check.Click += delegate(object sender, RoutedEventArgs e)
            {
                CheckBox box = (CheckBox)sender;
                AppState.Repo.SetTodoStatus((string)box.Tag, box.IsChecked == true ? TodoStatus.Done : TodoStatus.Open);
                AppState.Commit();
                Refresh();
            };
            row.Children.Add(check);

            StackPanel text = new StackPanel();
            text.Margin = new Thickness(10, 0, 8, 0);
            TextBlock title = UiKit.Body(todo.Title);
            if (todo.IsDone) { title.TextDecorations = TextDecorations.Strikethrough; title.Opacity = 0.65; }
            text.Children.Add(title);
            string meta = AppState.Data.NameOf(todo.CategoryId);
            if (todo.DueDate.HasValue)
            {
                meta += " · " + Services.CalendarService.FormatDateTime(todo.DueDate.Value);
            }
            text.Children.Add(UiKit.Caption(meta));
            Grid.SetColumn(text, 1);
            row.Children.Add(text);

            if (todo.DueDate.HasValue)
            {
                Border badge = UiKit.Badge(Services.CalendarService.DescribeDistance(todo.DueDate.Value, now),
                    null, UiKit.Brush(UrgencyBrushKey(TaskQuery.UrgencyOf(todo, now))));
                Grid.SetColumn(badge, 2);
                row.Children.Add(badge);
            }

            row.MouseLeftButtonUp += delegate { _shell.Navigate("tasks", todo.Id); };
            row.Cursor = System.Windows.Input.Cursors.Hand;
            row.Margin = new Thickness(0, 3, 0, 3);
            return row;
        }

        private UIElement ExamsCard(DateTime now)
        {
            StackPanel content = new StackPanel();
            content.Children.Add(UiKit.SectionHeader(Loc.T("exams.title"),
                UiKit.TextButton(Loc.T("exams.new_exam"), delegate { ExamEditor.ShowCreate(_shell); })));

            List<Exam> exams = new List<Exam>(AppState.Data.Exams);
            exams.Sort(delegate(Exam a, Exam b) { return a.StartDate.CompareTo(b.StartDate); });
            int shown = 0;
            for (int i = 0; i < exams.Count && shown < 3; i++)
            {
                Exam exam = exams[i];
                if (exam.StartDate < now.AddDays(-1)) { continue; }
                shown++;
                Grid row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Margin = new Thickness(0, 3, 0, 3);

                StackPanel text = new StackPanel();
                text.Children.Add(UiKit.Body(exam.Title));
                text.Children.Add(UiKit.Caption(Services.CalendarService.FormatDateTime(exam.StartDate) +
                    (string.IsNullOrEmpty(exam.Location) ? "" : " · " + exam.Location)));
                row.Children.Add(text);

                TimeSpan span = exam.StartDate - now;
                string countdown = span.Ticks < 0 ? Loc.T("common.done")
                    : span.TotalDays >= 1 ? ((int)span.TotalDays) + " " + Loc.T("timeline.day")
                    : ((int)span.TotalHours) + " h";
                Border badge = UiKit.Badge(countdown, null, UiKit.Brush("Brush.Error"));
                Grid.SetColumn(badge, 1);
                row.Children.Add(badge);
                content.Children.Add(row);
            }
            if (shown == 0) { content.Children.Add(UiKit.Muted(Loc.T("exams.no_exams"))); }

            Border card = UiKit.Card(content, false);
            card.Margin = new Thickness(0, 0, 0, 0);
            return card;
        }

        private UIElement CategoryCard(DateTime now)
        {
            StackPanel content = new StackPanel();
            content.Children.Add(UiKit.SectionHeader(Loc.T("common.categories"),
                UiKit.TextButton("+ " + Loc.T("common.new"), delegate
                {
                    CategoryEditor.ShowCreate(_shell, delegate { Refresh(); });
                })));

            List<TaskQuery.CategoryCount> breakdown = TaskQuery.CategoryBreakdown(now);
            for (int i = 0; i < breakdown.Count; i++)
            {
                TaskQuery.CategoryCount entry = breakdown[i];
                if (entry.Open + entry.Done == 0) { continue; }

                Grid row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Margin = new Thickness(0, 2, 0, 2);

                row.Children.Add(UiKit.Dot(entry.Category.Color, 9));
                TextBlock name = UiKit.Body(entry.Category.Name);
                name.Margin = new Thickness(9, 0, 8, 0);
                Grid.SetColumn(name, 1);
                row.Children.Add(name);
                TextBlock counts = UiKit.Caption(entry.Open + " / " + (entry.Open + entry.Done));
                Grid.SetColumn(counts, 2);
                row.Children.Add(counts);
                content.Children.Add(row);
            }
            if (content.Children.Count <= 1) { content.Children.Add(UiKit.Muted(Loc.T("common.no_data"))); }

            Border card = UiKit.Card(content, false);
            card.Margin = new Thickness(0, 0, 0, 12);
            return card;
        }

        private UIElement CompetitionCard()
        {
            StackPanel content = new StackPanel();
            content.Children.Add(UiKit.SectionHeader(Loc.T("comp.title"),
                UiKit.TextButton(Loc.T("comp.new"), delegate { CompetitionEditor.ShowCreate(_shell); })));

            List<Competition> competitions = AppState.Data.Competitions;
            int shown = 0;
            for (int i = 0; i < competitions.Count && shown < 4; i++)
            {
                Competition competition = competitions[i];
                if (competition.Stage == "archived" || competition.Stage == "finished") { continue; }
                shown++;
                Grid row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.Margin = new Thickness(0, 3, 0, 3);
                row.Children.Add(UiKit.Dot(competition.Color, 9));
                StackPanel text = new StackPanel();
                text.Margin = new Thickness(9, 0, 0, 0);
                text.Children.Add(UiKit.Body(competition.Name));
                int linked = competition.LinkedTodos.Count;
                text.Children.Add(UiKit.Caption(Loc.T("comp.stage_" + competition.Stage) + " · " +
                    competition.Milestones.Count + " " + Loc.T("comp.milestones") + " · " +
                    linked + " " + Loc.T("comp.linked_todos")));
                Grid.SetColumn(text, 1);
                row.Children.Add(text);
                row.Cursor = System.Windows.Input.Cursors.Hand;
                string id = competition.Id;
                row.MouseLeftButtonUp += delegate { _shell.Navigate("competitions", id); };
                content.Children.Add(row);
            }
            if (shown == 0) { content.Children.Add(UiKit.Muted(Loc.T("comp.empty_hint"))); }

            Border card = UiKit.Card(content, false);
            card.Margin = new Thickness(0, 0, 0, 12);
            return card;
        }

        private UIElement ActivityCard()
        {
            StackPanel content = new StackPanel();
            content.Children.Add(UiKit.SectionHeader(Loc.T("common.details"), null));
            List<ActivityEntry> activity = AppState.Data.Activity;
            int shown = 0;
            for (int i = 0; i < activity.Count && shown < 8; i++)
            {
                ActivityEntry entry = activity[i];
                Grid row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Margin = new Thickness(0, 2, 0, 2);
                TextBlock text = UiKit.Caption(entry.Message);
                text.TextTrimming = TextTrimming.CharacterEllipsis;
                row.Children.Add(text);
                TextBlock when = UiKit.Caption(UiKit.RelativeTime(Core.IsoTime.Parse(entry.At, DateTime.Now)));
                Grid.SetColumn(when, 1);
                row.Children.Add(when);
                content.Children.Add(row);
                shown++;
            }
            if (shown == 0) { content.Children.Add(UiKit.Muted(Loc.T("common.no_data"))); }
            return UiKit.Card(content, false);
        }
    }
}
