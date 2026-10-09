using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Learnest.Data;
using Learnest.Services;

using System.Windows.Shapes;
namespace Learnest.Views
{
    /// <summary>
    /// Examination Planner: the exam timetable with a live countdown, one line per exam by default
    /// and the markdown record (review scope, papers, seat info) behind the advanced record button.
    /// Every exam also appears on the Timeline and drives the widget alert when it is close.
    /// </summary>
    internal sealed class ExamsView : UserControl, IRefreshable
    {
        private readonly IShell _shell;
        private readonly StackPanel _list;
        private readonly TextBlock _nextExam;
        private readonly TextBlock _summary;

        public ExamsView(IShell shell)
        {
            _shell = shell;

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Margin = new Thickness(22, 16, 22, 8);

            StackPanel titles = new StackPanel();
            titles.Children.Add(UiKit.Text(Loc.T("exams.title"), "Text.Display"));
            _summary = UiKit.Muted("");
            _summary.Margin = new Thickness(0, 2, 0, 0);
            titles.Children.Add(_summary);
            _nextExam = UiKit.Text("", "Text.Subtitle");
            _nextExam.Margin = new Thickness(0, 6, 0, 0);
            _nextExam.Foreground = UiKit.Brush("Brush.Error");
            titles.Children.Add(_nextExam);
            header.Children.Add(titles);

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.VerticalAlignment = VerticalAlignment.Top;
            Button add = UiKit.Primary("+ " + Loc.T("exams.new_exam"), delegate { ExamEditor.ShowCreate(_shell); });
            actions.Children.Add(add);
            Button timetable = UiKit.Outlined(Loc.T("timeline.title"), delegate { _shell.Navigate("timeline"); });
            timetable.Margin = new Thickness(8, 0, 0, 0);
            actions.Children.Add(timetable);
            Grid.SetColumn(actions, 1);
            header.Children.Add(actions);

            Grid.SetRow(header, 0);
            root.Children.Add(header);

            ScrollViewer scroller = new ScrollViewer();
            scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroller.Padding = new Thickness(22, 0, 22, 16);
            _list = new StackPanel();
            scroller.Content = _list;
            Grid.SetRow(scroller, 1);
            root.Children.Add(scroller);

            Content = root;
            Refresh();
        }

        public void Refresh()
        {
            _list.Children.Clear();
            if (AppState.SessionOrNull == null) { return; }

            string argument = _shell.TakeArgument();
            DateTime now = DateTime.Now;

            List<Exam> all = new List<Exam>(AppState.Data.Exams);
            all.Sort(delegate(Exam a, Exam b) { return a.StartDate.CompareTo(b.StartDate); });

            List<Exam> upcoming = new List<Exam>();
            List<Exam> past = new List<Exam>();
            for (int i = 0; i < all.Count; i++)
            {
                Exam exam = all[i];
                if (exam.IsDone || exam.StartDate.AddHours(3) < now) { past.Add(exam); }
                else { upcoming.Add(exam); }
            }

            _summary.Text = Loc.T("exams.upcoming") + " " + upcoming.Count + " · " +
                            Loc.T("exams.past") + " " + past.Count;
            if (upcoming.Count > 0)
            {
                Exam next = upcoming[0];
                TimeSpan span = next.StartDate - now;
                string countdown = span.Ticks < 0 ? Loc.T("common.today")
                    : span.TotalDays >= 1 ? ((int)span.TotalDays) + " " + Loc.T("timeline.day")
                    : ((int)span.TotalHours) + " " + Loc.T("calendar.hour");
                _nextExam.Text = Loc.T("exams.next_exam") + ": " + next.Title + " · " + countdown +
                                 " · " + Services.CalendarService.FormatDateTime(next.StartDate);
            }
            else
            {
                _nextExam.Text = Loc.T("exams.no_exams");
            }

            if (all.Count == 0)
            {
                _list.Children.Add(UiKit.Empty("exam", Loc.T("exams.empty"), Loc.T("exams.new_exam")));
                return;
            }

            _list.Children.Add(UiKit.SectionHeader(Loc.T("exams.upcoming"), null));
            if (upcoming.Count == 0) { _list.Children.Add(UiKit.Muted(Loc.T("exams.no_exams"))); }
            for (int i = 0; i < upcoming.Count; i++)
            {
                _list.Children.Add(Row(upcoming[i], now, argument));
            }

            if (past.Count > 0)
            {
                _list.Children.Add(UiKit.SectionHeader(Loc.T("exams.past"), null));
                for (int i = past.Count - 1; i >= 0; i--)
                {
                    _list.Children.Add(Row(past[i], now, argument));
                }
            }
        }

        private UIElement Row(Exam exam, DateTime now, string highlightId)
        {
            Border card = new Border();
            card.Style = UiKit.Style("RowCard");
            if (exam.Id == highlightId)
            {
                card.BorderBrush = UiKit.Brush("Brush.Primary");
                card.BorderThickness = new Thickness(2);
            }

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Countdown block
            Border countdown = new Border();
            countdown.Width = 78;
            countdown.CornerRadius = new CornerRadius(10);
            countdown.Padding = new Thickness(6, 4, 6, 4);
            TimeSpan span = exam.StartDate - now;
            bool finished = exam.IsDone || span.Ticks < 0;
            Color accent = finished ? UiKit.UiColor("Brush.OnSurfaceVariant") : UiKit.UiColor("Brush.Error");
            countdown.Background = UiKit.Alpha(accent, 0.14);
            StackPanel countdownText = new StackPanel();
            string big;
            string small;
            if (finished)
            {
                big = exam.IsDone ? Loc.T("common.done") : "—";
                small = Services.CalendarService.FormatDateShort(exam.StartDate);
            }
            else if (span.TotalDays >= 1)
            {
                big = ((int)span.TotalDays).ToString();
                small = Loc.T("exams.days_left", (int)span.TotalDays);
            }
            else
            {
                big = ((int)Math.Max(0, span.TotalHours)).ToString();
                small = Loc.T("exams.hours_left", (int)Math.Max(0, span.TotalHours));
            }
            TextBlock bigText = UiKit.Text(big, "Text.Subtitle");
            bigText.FontSize = 20;
            bigText.Foreground = new SolidColorBrush(accent);
            bigText.HorizontalAlignment = HorizontalAlignment.Center;
            countdownText.Children.Add(bigText);
            TextBlock smallText = UiKit.Caption(small);
            smallText.HorizontalAlignment = HorizontalAlignment.Center;
            countdownText.Children.Add(smallText);
            countdown.Child = countdownText;
            countdown.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(countdown);

            StackPanel text = new StackPanel();
            text.Margin = new Thickness(12, 0, 10, 0);
            TextBlock title = UiKit.Text(exam.Title, "Text.Body");
            title.FontWeight = FontWeights.SemiBold;
            if (finished) { title.Opacity = 0.7; }
            text.Children.Add(title);

            StackPanel meta = new StackPanel();
            meta.Orientation = Orientation.Horizontal;
            meta.Children.Add(UiKit.Caption(Services.CalendarService.FormatDateTime(exam.StartDate)));
            if (exam.EndDate.HasValue)
            {
                meta.Children.Add(UiKit.Caption("  →  " + exam.EndDate.Value.ToString("HH:mm")));
            }
            if (!string.IsNullOrEmpty(exam.Course))
            {
                meta.Children.Add(UiKit.Caption(" · " + exam.Course));
            }
            if (!string.IsNullOrEmpty(exam.Location))
            {
                meta.Children.Add(UiKit.Caption(" · " + exam.Location));
            }
            if (!string.IsNullOrEmpty(exam.Seat))
            {
                meta.Children.Add(UiKit.Caption(" · " + Loc.T("exams.seat") + " " + exam.Seat));
            }
            if (!string.IsNullOrEmpty(exam.Score))
            {
                meta.Children.Add(UiKit.Caption(" · " + Loc.T("exams.score") + " " + exam.Score));
            }
            text.Children.Add(meta);
            if (!string.IsNullOrEmpty(exam.NoteMarkdown))
            {
                string preview = Markdown.ToPlainText(exam.NoteMarkdown);
                string[] lines = preview.Replace("\r\n", "\n").Split('\n');
                string firstLine = "";
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Trim().Length > 0) { firstLine = lines[i].Trim(); break; }
                }
                if (firstLine.Length > 0)
                {
                    TextBlock note = UiKit.Caption(firstLine);
                    note.TextTrimming = TextTrimming.CharacterEllipsis;
                    note.Margin = new Thickness(0, 2, 0, 0);
                    text.Children.Add(note);
                }
            }
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            Border status = UiKit.Badge(Loc.T("exams.status_" + exam.Status), null,
                UiKit.Brush(exam.Status == "done" ? "Brush.Success" :
                            exam.Status == "cancelled" ? "Brush.OnSurfaceFaint" : "Brush.Primary"));
            status.VerticalAlignment = VerticalAlignment.Center;
            status.Margin = new Thickness(0, 0, 8, 0);
            Grid.SetColumn(status, 2);
            grid.Children.Add(status);

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.VerticalAlignment = VerticalAlignment.Center;
            actions.Children.Add(UiKit.IconButton("markdown", Loc.T("tasks.advanced_record"), delegate
            {
                Exam fresh = AppState.Repo.FindExam(exam.Id);
                if (fresh != null) { ExamEditor.ShowEdit(_shell, fresh); }
            }));
            CheckBox done = new CheckBox();
            done.Style = UiKit.Style("CheckBox.Round");
            done.IsChecked = exam.IsDone;
            done.VerticalAlignment = VerticalAlignment.Center;
            done.Margin = new Thickness(4, 0, 4, 0);
            ToolTipService.SetToolTip(done, Loc.T("exams.mark_done"));
            done.Click += delegate(object sender, RoutedEventArgs e)
            {
                Exam fresh = AppState.Repo.FindExam(exam.Id);
                if (fresh == null) { return; }
                fresh.Status = ((CheckBox)sender).IsChecked == true ? "done" : "planned";
                AppState.Repo.SaveExam(fresh);
                AppState.Commit();
            };
            actions.Children.Add(done);
            actions.Children.Add(UiKit.IconButton("trash", Loc.T("common.delete"), delegate
            {
                if (AppState.Settings.ConfirmDelete &&
                    !UiKit.Confirm(_shell.Window, Loc.T("common.delete"), exam.Title + "\r\n" + Loc.T("common.are_you_sure")))
                {
                    return;
                }
                AppState.Repo.DeleteExam(exam.Id);
                AppState.Commit();
            }));
            Grid.SetColumn(actions, 3);
            grid.Children.Add(actions);

            card.Child = grid;
            card.Cursor = Cursors.Hand;
            card.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.OriginalSource is Button || e.OriginalSource is CheckBox) { return; }
                Exam fresh = AppState.Repo.FindExam(exam.Id);
                if (fresh != null) { ExamEditor.ShowEdit(_shell, fresh); }
            };
            return card;
        }
    }
}
