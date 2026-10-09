using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// The built-in perpetual calendar.  Pure Proleptic Gregorian month and year views with weekday
    /// headers - no lunar data, no public holidays and no network access - plus a day panel that
    /// lists everything due that day and offers quick capture.
    /// </summary>
    internal sealed class CalendarView : UserControl, IRefreshable
    {
        private readonly IShell _shell;
        private readonly Grid _monthHost;
        private readonly TextBlock _monthTitle;
        private readonly StackPanel _dayPanel;
        private readonly TextBlock _dayTitle;
        private readonly StackPanel _yearHost;
        private Border _dayHost;

        private DateTime _cursor = DateTime.Now;
        private DateTime _selected = DateTime.Now;
        private bool _yearMode;
        private Dictionary<DateTime, int> _density;

        public CalendarView(IShell shell)
        {
            _shell = shell;

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.Margin = new Thickness(22, 16, 22, 16);

            // ---------------- header
            Grid header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel nav = new StackPanel();
            nav.Orientation = Orientation.Horizontal;
            nav.Children.Add(UiKit.IconButton("chevron_left", Loc.T("calendar.prev_month"), delegate { Move(-1); }));
            _monthTitle = UiKit.Text("", "Text.Title");
            _monthTitle.MinWidth = 190;
            _monthTitle.TextAlignment = TextAlignment.Center;
            _monthTitle.VerticalAlignment = VerticalAlignment.Center;
            nav.Children.Add(_monthTitle);
            nav.Children.Add(UiKit.IconButton("chevron_right", Loc.T("calendar.next_month"), delegate { Move(1); }));
            header.Children.Add(nav);

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.HorizontalAlignment = HorizontalAlignment.Right;
            actions.VerticalAlignment = VerticalAlignment.Center;
            actions.Children.Add(UiKit.Outlined(Loc.T("calendar.today"), delegate
            {
                _cursor = DateTime.Now;
                _selected = DateTime.Now;
                Refresh();
            }));
            System.Windows.Controls.Primitives.ToggleButton yearToggle = new System.Windows.Controls.Primitives.ToggleButton();
            yearToggle.Content = Loc.T("calendar.year_view");
            yearToggle.Style = UiKit.Style("Button.Chip");
            yearToggle.Margin = new Thickness(8, 0, 0, 0);
            yearToggle.Checked += delegate { _yearMode = true; Refresh(); };
            yearToggle.Unchecked += delegate { _yearMode = false; Refresh(); };
            actions.Children.Add(yearToggle);

            Button add = UiKit.Primary("+ " + Loc.T("calendar.add_todo_on_day"), delegate
            {
                TaskEditor.ShowCreate(_shell, null);
            });
            add.Margin = new Thickness(8, 0, 0, 0);
            actions.Children.Add(add);
            Grid.SetColumn(actions, 2);
            header.Children.Add(actions);
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // ---------------- body
            Grid body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            body.Margin = new Thickness(0, 14, 0, 0);

            _monthHost = new Grid();
            _monthHost.Margin = new Thickness(0, 0, 12, 0);
            body.Children.Add(_monthHost);

            Border dayHost = UiKit.Card(null, false);
            Grid.SetColumn(dayHost, 1);
            StackPanel dayStack = new StackPanel();
            _dayTitle = UiKit.Subtitle("");
            dayStack.Children.Add(_dayTitle);
            _dayPanel = new StackPanel();
            _dayPanel.Margin = new Thickness(0, 8, 0, 0);
            ScrollViewer dayScroller = new ScrollViewer();
            dayScroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            dayScroller.Content = _dayPanel;
            dayScroller.MaxHeight = 640;
            dayStack.Children.Add(dayScroller);
            dayHost.Child = dayStack;
            body.Children.Add(dayHost);
            _dayHost = dayHost;

            _yearHost = new StackPanel();
            _yearHost.Visibility = Visibility.Collapsed;
            Grid.SetColumn(_yearHost, 1);
            body.Children.Add(_yearHost);

            Grid.SetRow(body, 1);
            root.Children.Add(body);
            Content = root;
            Refresh();
        }

        private void Move(int months)
        {
            _cursor = _cursor.AddMonths(months);
            Refresh();
        }

        public void Refresh()
        {
            _monthHost.Children.Clear();
            _dayPanel.Children.Clear();

            if (AppState.SessionOrNull == null) { return; }

            DateTime monthStart = new DateTime(_cursor.Year, _cursor.Month, 1);
            DateTime monthEnd = monthStart.AddMonths(1).AddDays(-1);
            _density = TaskQuery.LoadDensity(monthStart.AddDays(-7), monthEnd.AddDays(7));
            _monthTitle.Text = _cursor.Year + " 年 " + _cursor.Month + " 月";

            if (_yearMode)
            {
                RenderYear();
            }
            else
            {
                RenderMonth();
            }
            RenderDay();
        }

        private void RenderMonth()
        {
            _yearHost.Visibility = Visibility.Collapsed;
            _yearHost.Children.Clear();
            _yearHost.Tag = null;
            if (_dayHost != null) { _dayHost.Visibility = Visibility.Visible; }
            List<DateTime> cells = Services.CalendarService.MonthCells(_cursor.Year, _cursor.Month, 6);

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (int i = 0; i < 7; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int i = 0; i < 6; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }

            // Week number column header.
            TextBlock weekHeader = UiKit.Caption("W");
            weekHeader.HorizontalAlignment = HorizontalAlignment.Center;
            weekHeader.Margin = new Thickness(4, 0, 6, 6);
            grid.Children.Add(weekHeader);

            for (int i = 0; i < 7; i++)
            {
                TextBlock header = UiKit.Label(Services.CalendarService.WeekdayLong[i]);
                header.HorizontalAlignment = HorizontalAlignment.Center;
                header.Margin = new Thickness(0, 0, 0, 6);
                Grid.SetColumn(header, i + 1);
                grid.Children.Add(header);
            }

            for (int week = 0; week < 6; week++)
            {
                DateTime weekStart = cells[week * 7];
                TextBlock weekNumber = UiKit.Caption(Services.CalendarService.IsoWeekNumber(weekStart).ToString());
                weekNumber.HorizontalAlignment = HorizontalAlignment.Center;
                weekNumber.VerticalAlignment = VerticalAlignment.Center;
                weekNumber.Opacity = 0.6;
                Grid.SetRow(weekNumber, week + 1);
                grid.Children.Add(weekNumber);

                for (int day = 0; day < 7; day++)
                {
                    DateTime date = cells[week * 7 + day];
                    UIElement cell = DayCell(date);
                    Grid.SetRow(cell, week + 1);
                    Grid.SetColumn(cell, day + 1);
                    grid.Children.Add(cell);
                }
            }
            _monthHost.Children.Add(grid);
        }

        private UIElement DayCell(DateTime date)
        {
            bool inMonth = date.Month == _cursor.Month;
            bool isToday = date.Date == DateTime.Now.Date;
            bool isSelected = date.Date == _selected.Date;

            Border cell = new Border();
            cell.Margin = new Thickness(2);
            cell.CornerRadius = new CornerRadius(10);
            cell.Padding = new Thickness(6, 4, 6, 4);
            cell.MinHeight = 54;
            cell.Cursor = Cursors.Hand;
            cell.Background = isSelected
                ? UiKit.Brush("Brush.Selection")
                : (inMonth ? UiKit.Brush("Brush.SurfaceContainerLow") : Brushes.Transparent);
            cell.BorderThickness = new Thickness(isToday ? 2 : 1);
            cell.BorderBrush = isToday ? UiKit.Brush("Brush.Primary") : UiKit.Brush("Brush.OutlineVariant");
            if (!inMonth) { cell.Opacity = 0.55; }

            StackPanel stack = new StackPanel();
            TextBlock number = UiKit.Body(date.Day.ToString(CultureInfo.InvariantCulture));
            number.FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal;
            if (Services.CalendarService.IsWeekend(date)) { number.Foreground = UiKit.Brush("Brush.Error"); }
            stack.Children.Add(number);

            int count = 0;
            if (_density != null && _density.TryGetValue(date.Date, out count) && count > 0)
            {
                StackPanel dots = new StackPanel();
                dots.Orientation = Orientation.Horizontal;
                dots.Margin = new Thickness(0, 3, 0, 0);
                int shown = Math.Min(count, 4);
                for (int i = 0; i < shown; i++)
                {
                    Border dot = new Border();
                    dot.Width = 5;
                    dot.Height = 5;
                    dot.CornerRadius = new CornerRadius(3);
                    dot.Margin = new Thickness(0, 0, 2, 0);
                    dot.Background = UiKit.Brush("Brush.Primary");
                    dot.Opacity = 0.7;
                    dots.Children.Add(dot);
                }
                if (count > shown)
                {
                    TextBlock more = UiKit.Caption("+" + (count - shown));
                    dots.Children.Add(more);
                }
                stack.Children.Add(dots);
            }
            cell.Child = stack;
            cell.MouseLeftButtonUp += delegate
            {
                _selected = date;
                if (!inMonth) { _cursor = new DateTime(date.Year, date.Month, 1); }
                Refresh();
            };
            cell.MouseRightButtonUp += delegate
            {
                _selected = date;
                Refresh();
                _shell.Navigate("tasks");
            };
            ToolTipService.SetToolTip(cell, Services.CalendarService.FormatFull(date) +
                (count > 0 ? " · " + count + " " + Loc.T("common.items") : ""));
            return cell;
        }

        private void RenderYear()
        {
            _yearHost.Visibility = Visibility.Visible;
            if (_yearHost.Children.Count == 0 || _yearHost.Tag == null || (int)_yearHost.Tag != _cursor.Year)
            {
                _yearHost.Children.Clear();
                _yearHost.Tag = _cursor.Year;
                WrapPanel months = new WrapPanel();
                for (int m = 1; m <= 12; m++)
                {
                    months.Children.Add(MiniMonth(_cursor.Year, m));
                }
                _yearHost.Children.Add(months);
            }
            if (_dayHost != null) { _dayHost.Visibility = Visibility.Collapsed; }
            _monthHost.Children.Clear();
            _monthHost.Children.Add(UiKit.Caption(Loc.T("calendar.year_overview") + " " + _cursor.Year));
        }

        private UIElement MiniMonth(int year, int month)
        {
            Border card = UiKit.Card(null, false);
            card.Width = 210;
            card.Margin = new Thickness(0, 0, 10, 10);
            card.Padding = new Thickness(10);

            StackPanel stack = new StackPanel();
            TextBlock title = UiKit.Label(year + "-" + month.ToString("00"));
            title.FontWeight = FontWeights.SemiBold;
            title.Cursor = Cursors.Hand;
            title.MouseLeftButtonUp += delegate
            {
                _cursor = new DateTime(year, month, 1);
                _yearMode = false;
                Refresh();
            };
            stack.Children.Add(title);

            Grid grid = new Grid();
            for (int i = 0; i < 7; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int i = 0; i < 6; i++) { grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); }

            for (int i = 0; i < 7; i++)
            {
                TextBlock header = UiKit.Caption(Services.CalendarService.WeekdayShort[i]);
                header.HorizontalAlignment = HorizontalAlignment.Center;
                header.Opacity = 0.7;
                Grid.SetColumn(header, i);
                grid.Children.Add(header);
            }

            List<DateTime> cells = Services.CalendarService.MonthCells(year, month, 6);
            for (int i = 0; i < cells.Count; i++)
            {
                DateTime date = cells[i];
                bool inMonth = date.Month == month;
                TextBlock number = UiKit.Caption(date.Day.ToString(CultureInfo.InvariantCulture));
                number.HorizontalAlignment = HorizontalAlignment.Center;
                number.Margin = new Thickness(0, 1, 0, 1);
                if (!inMonth) { number.Opacity = 0.35; }
                if (date.Date == DateTime.Now.Date)
                {
                    number.Foreground = UiKit.Brush("Brush.Primary");
                    number.FontWeight = FontWeights.Bold;
                }
                else if (Services.CalendarService.IsWeekend(date))
                {
                    number.Foreground = UiKit.Brush("Brush.Error");
                }
                int count = 0;
                if (_density != null && _density.TryGetValue(date.Date, out count) && count > 0)
                {
                    number.TextDecorations = TextDecorations.Underline;
                }
                number.Cursor = Cursors.Hand;
                DateTime captured = date;
                number.MouseLeftButtonUp += delegate
                {
                    _selected = captured;
                    _cursor = new DateTime(captured.Year, captured.Month, 1);
                    _yearMode = false;
                    Refresh();
                };
                Grid.SetRow(number, i / 7 + 1);
                Grid.SetColumn(number, i % 7);
                grid.Children.Add(number);
            }
            stack.Children.Add(grid);
            card.Child = stack;
            return card;
        }

        private void RenderDay()
        {
            _dayTitle.Text = Services.CalendarService.FormatFull(_selected);
            _dayPanel.Children.Clear();

            List<TaskQuery.TimelineRow> items = TaskQuery.ItemsOn(_selected);
            if (items.Count == 0)
            {
                _dayPanel.Children.Add(UiKit.Muted(Loc.T("calendar.no_items_on_day")));
            }

            DateTime now = DateTime.Now;
            for (int i = 0; i < items.Count; i++)
            {
                TaskQuery.TimelineRow item = items[i];
                Border card = new Border();
                card.Style = UiKit.Style("RowCard");
                Grid grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                grid.Children.Add(UiKit.Dot(item.Color, 9));

                StackPanel text = new StackPanel();
                text.Margin = new Thickness(10, 0, 8, 0);
                TextBlock title = UiKit.Body(item.Title);
                if (item.Todo != null && item.Todo.IsDone)
                {
                    title.TextDecorations = TextDecorations.Strikethrough;
                    title.Opacity = 0.65;
                }
                text.Children.Add(title);
                string kind = item.Kind == "exam" ? Loc.T("timeline.exam") :
                              item.Kind == "milestone" ? Loc.T("timeline.milestone") :
                              item.Kind == "event" ? Loc.T("comp.events") : item.CategoryName;
                text.Children.Add(UiKit.Caption(kind + " · " + item.Start.ToString("HH:mm")));
                Grid.SetColumn(text, 1);
                grid.Children.Add(text);

                if (item.Todo != null)
                {
                    CheckBox check = new CheckBox();
                    check.Style = UiKit.Style("CheckBox.Round");
                    check.IsChecked = item.Todo.IsDone;
                    check.VerticalAlignment = VerticalAlignment.Center;
                    check.Click += delegate(object sender, RoutedEventArgs e)
                    {
                        AppState.Repo.SetTodoStatus(item.Todo.Id, ((CheckBox)sender).IsChecked == true ? TodoStatus.Done : TodoStatus.Open);
                        AppState.Commit();
                    };
                    Grid.SetColumn(check, 2);
                    grid.Children.Add(check);
                }
                card.Child = grid;
                card.Cursor = Cursors.Hand;
                card.MouseLeftButtonUp += delegate
                {
                    if (item.Todo != null) { _shell.Navigate("tasks", item.Todo.Id); }
                    else if (item.Exam != null) { _shell.Navigate("exams", item.Exam.Id); }
                    else if (item.Competition != null) { _shell.Navigate("competitions", item.Competition.Id); }
                };
                _dayPanel.Children.Add(card);
            }

            StackPanel quick = new StackPanel();
            quick.Orientation = Orientation.Horizontal;
            quick.Margin = new Thickness(0, 10, 0, 0);
            quick.Children.Add(UiKit.Outlined("+ " + Loc.T("calendar.add_todo_on_day"), delegate
            {
                TaskEditor.ShowCreate(_shell, null);
            }));
            Button exam = UiKit.Outlined("+ " + Loc.T("calendar.add_exam_on_day"), delegate
            {
                ExamEditor.ShowCreateAt(_shell, _selected.Date.AddHours(9));
            });
            exam.Margin = new Thickness(8, 0, 0, 0);
            quick.Children.Add(exam);
            _dayPanel.Children.Add(quick);
        }
    }
}
