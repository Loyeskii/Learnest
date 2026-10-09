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
using System.Windows.Shapes;
namespace Learnest.Views
{
    /// <summary>
    /// Timeline: every to-do drawn as a span from its creation time to its deadline (marking the
    /// last modification), coloured by category or competition, with examinations and competition
    /// milestones on the same axis.  Check boxes at the top choose which kinds are shown.
    /// </summary>
    internal sealed class TimelineView : UserControl, IRefreshable
    {
        private readonly IShell _shell;
        private readonly Canvas _chart;
        private readonly Canvas _labels;
        private readonly Canvas _header;
        private readonly ScrollViewer _chartScroller;
        private readonly StackPanel _legend;
        private readonly TextBlock _summary;
        private readonly TaskQuery.TimelineOptions _options = new TaskQuery.TimelineOptions();

        private double _pixelsPerDay = 9;
        private DateTime _origin;
        private int _spanDays = 90;

        private const double RowHeight = 30;
        private const double LabelWidth = 300;

        public TimelineView(IShell shell)
        {
            _shell = shell;

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // ---------------- header
            Grid header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Margin = new Thickness(22, 16, 22, 0);

            StackPanel titles = new StackPanel();
            titles.Children.Add(UiKit.Text(Loc.T("timeline.title"), "Text.Display"));
            _summary = UiKit.Muted("");
            _summary.Margin = new Thickness(0, 2, 0, 0);
            titles.Children.Add(_summary);
            header.Children.Add(titles);

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.VerticalAlignment = VerticalAlignment.Center;
            actions.Children.Add(UiKit.IconButton("chevron_left", Loc.T("calendar.prev_month"), delegate { Pan(-_spanDays / 4); }));
            actions.Children.Add(UiKit.IconButton("minus", Loc.T("timeline.zoom_out"), delegate { Zoom(1 / 1.35); }));
            actions.Children.Add(UiKit.IconButton("plus", Loc.T("timeline.zoom_in"), delegate { Zoom(1.35); }));
            actions.Children.Add(UiKit.IconButton("chevron_right", Loc.T("calendar.next_month"), delegate { Pan(_spanDays / 4); }));
            Button today = UiKit.TextButton(Loc.T("calendar.today"), delegate
            {
                _origin = DateTime.Now.Date.AddDays(-7);
                Render();
            });
            actions.Children.Add(today);
            Grid.SetColumn(actions, 1);
            header.Children.Add(actions);
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // ---------------- filters
            WrapPanel filters = new WrapPanel();
            filters.Margin = new Thickness(22, 10, 22, 4);
            filters.Children.Add(FilterChip(Loc.T("timeline.show_todos"), _options.ShowTodos, delegate(bool value) { _options.ShowTodos = value; }));
            filters.Children.Add(FilterChip(Loc.T("timeline.show_exams"), _options.ShowExams, delegate(bool value) { _options.ShowExams = value; }));
            filters.Children.Add(FilterChip(Loc.T("timeline.show_competitions"), _options.ShowCompetitions, delegate(bool value) { _options.ShowCompetitions = value; }));
            filters.Children.Add(FilterChip(Loc.T("timeline.show_milestones"), _options.ShowMilestones, delegate(bool value) { _options.ShowMilestones = value; }));
            filters.Children.Add(FilterChip(Loc.T("comp.events"), _options.ShowEvents, delegate(bool value) { _options.ShowEvents = value; }));
            filters.Children.Add(FilterChip(Loc.T("timeline.show_completed"), _options.ShowCompleted, delegate(bool value) { _options.ShowCompleted = value; }));

            ComboBox spanCombo = UiKit.Combo(new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("30", "30 " + Loc.T("timeline.day")),
                new KeyValuePair<string, string>("90", "90 " + Loc.T("timeline.day")),
                new KeyValuePair<string, string>("180", "180 " + Loc.T("timeline.day")),
                new KeyValuePair<string, string>("365", "365 " + Loc.T("timeline.day"))
            }, _spanDays.ToString());
            spanCombo.Width = 120;
            spanCombo.Margin = new Thickness(8, 0, 0, 6);
            spanCombo.SelectionChanged += delegate
            {
                int days;
                if (int.TryParse(UiKit.SelectedKey(spanCombo), out days))
                {
                    _spanDays = days;
                    FitZoom();
                    Render();
                }
            };
            filters.Children.Add(spanCombo);
            Grid.SetRow(filters, 1);
            root.Children.Add(filters);

            // ---------------- legend
            _legend = new StackPanel();
            _legend.Orientation = Orientation.Horizontal;
            _legend.Margin = new Thickness(22, 2, 22, 6);
            Grid.SetRow(_legend, 2);
            root.Children.Add(_legend);

            // ---------------- chart
            Grid body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LabelWidth) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            body.Margin = new Thickness(22, 0, 22, 14);
            Grid.SetRow(body, 3);

            Border labelsHost = new Border();
            labelsHost.Background = UiKit.Brush("Brush.SurfaceContainerLow");
            labelsHost.BorderBrush = UiKit.Brush("Brush.OutlineVariant");
            labelsHost.BorderThickness = new Thickness(1);
            labelsHost.CornerRadius = new CornerRadius(11);
            ScrollViewer labelScroller = new ScrollViewer();
            labelScroller.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            labelScroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            _labels = new Canvas();
            labelScroller.Content = _labels;
            labelsHost.Child = labelScroller;
            body.Children.Add(labelsHost);

            Border chartHost = new Border();
            chartHost.Background = UiKit.Brush("Brush.SurfaceContainerLow");
            chartHost.BorderBrush = UiKit.Brush("Brush.OutlineVariant");
            chartHost.BorderThickness = new Thickness(1);
            chartHost.CornerRadius = new CornerRadius(11);
            Grid.SetColumn(chartHost, 1);

            Grid chartInner = new Grid();
            chartInner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            chartInner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _header = new Canvas { Height = 34 };
            Grid.SetRow(_header, 0);
            chartInner.Children.Add(_header);

            _chartScroller = new ScrollViewer();
            _chartScroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            _chartScroller.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            _chart = new Canvas();
            _chartScroller.Content = _chart;
            Grid.SetRow(_chartScroller, 1);
            chartInner.Children.Add(_chartScroller);

            chartHost.Child = chartInner;
            body.Children.Add(chartHost);

            // Keep the label column locked to the chart's vertical position.
            _chartScroller.ScrollChanged += delegate(object sender, ScrollChangedEventArgs e)
            {
                if (Math.Abs(e.VerticalChange) > 0.1)
                {
                    labelScroller.ScrollToVerticalOffset(e.VerticalOffset);
                }
            };
            _chartScroller.PreviewMouseWheel += delegate(object sender, MouseWheelEventArgs e)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    Zoom(e.Delta > 0 ? 1.2 : 1 / 1.2);
                    e.Handled = true;
                }
            };

            root.Children.Add(body);
            Content = root;

            _origin = DateTime.Now.Date.AddDays(-14);
            FitZoom();
            Refresh();
        }

        private System.Windows.Controls.Primitives.ToggleButton FilterChip(string label, bool initial, Action<bool> onChange)
        {
            System.Windows.Controls.Primitives.ToggleButton chip = new System.Windows.Controls.Primitives.ToggleButton();
            chip.Content = label;
            chip.Style = UiKit.Style("Button.Chip");
            chip.IsChecked = initial;
            chip.Margin = new Thickness(0, 0, 6, 6);
            chip.Checked += delegate { onChange(true); Render(); };
            chip.Unchecked += delegate { onChange(false); Render(); };
            return chip;
        }

        private void FitZoom()
        {
            double available = Math.Max(400, _chartScroller.ActualWidth > 0 ? _chartScroller.ActualWidth : 900);
            _pixelsPerDay = Math.Max(1.6, Math.Min(60, available / Math.Max(1, _spanDays)));
        }

        private void Zoom(double factor)
        {
            _pixelsPerDay = Math.Max(1.6, Math.Min(90, _pixelsPerDay * factor));
            Render();
        }

        private void Pan(int days)
        {
            _origin = _origin.AddDays(days);
            Render();
        }

        // ================================================================ render
        public void Refresh()
        {
            Render();
        }

        private void Render()
        {
            _chart.Children.Clear();
            _labels.Children.Clear();
            _header.Children.Clear();
            _legend.Children.Clear();

            if (AppState.SessionOrNull == null)
            {
                _summary.Text = Loc.T("container.none_selected");
                return;
            }

            DateTime to = _origin.AddDays(_spanDays);
            _options.Search = _shell.SearchText;
            if (string.IsNullOrEmpty(_options.CategoryId) && !string.IsNullOrEmpty(_shell.ActiveCategoryId))
            {
                _options.CategoryId = _shell.ActiveCategoryId;
            }
            else if (string.IsNullOrEmpty(_shell.ActiveCategoryId))
            {
                _options.CategoryId = null;
            }

            List<TaskQuery.TimelineRow> rows = TaskQuery.BuildTimeline(_options, _origin, to);

            double width = Math.Max(200, _spanDays * _pixelsPerDay);
            double height = Math.Max(RowHeight * 4, rows.Count * RowHeight + 16);
            _chart.Width = width;
            _chart.Height = height;
            _labels.Width = LabelWidth;
            _labels.Height = height;
            _header.Width = width;

            _summary.Text = Loc.T("timeline.row_count", rows.Count) + " · " +
                            _origin.ToString("yyyy-MM-dd") + " → " + to.ToString("yyyy-MM-dd") +
                            " · " + Loc.T("timeline.span_days", _spanDays);

            DrawHeader(width);
            DrawGrid(height);

            // Row bands and bars.
            for (int i = 0; i < rows.Count; i++)
            {
                double top = 8 + i * RowHeight;
                TaskQuery.TimelineRow row = rows[i];
                Color color = ColorUtil.Parse(row.Color, Colors.SteelBlue);

                if (i % 2 == 1)
                {
                    Rectangle band = new Rectangle();
                    band.Width = width;
                    band.Height = RowHeight;
                    band.Fill = UiKit.Alpha(ColorUtil.Parse(ColorUtil.ToHex(UiKit.UiColor("Brush.OutlineVariant")), Colors.LightGray), 0.18);
                    Canvas.SetLeft(band, 0);
                    Canvas.SetTop(band, top);
                    _chart.Children.Add(band);
                }

                DrawBar(row, color, top, width);

                // Label column: colour chip + title + span summary.
                Border chip = UiKit.Dot(row.Color, 9);
                Canvas.SetLeft(chip, 12);
                Canvas.SetTop(chip, top + 8);
                _labels.Children.Add(chip);

                TextBlock label = UiKit.Body(row.Title);
                label.Width = LabelWidth - 110;
                label.TextTrimming = TextTrimming.CharacterEllipsis;
                Canvas.SetLeft(label, 28);
                Canvas.SetTop(label, top + 4);
                label.ToolTip = BuildTooltip(row);
                _labels.Children.Add(label);

                TextBlock span = UiKit.Caption(Services.CalendarService.DescribeSpan(row.End - row.Start));
                Canvas.SetLeft(span, LabelWidth - 78);
                Canvas.SetTop(span, top + 6);
                _labels.Children.Add(span);
            }

            if (rows.Count == 0)
            {
                TextBlock empty = UiKit.Muted(Loc.T("timeline.empty"));
                Canvas.SetLeft(empty, 20);
                Canvas.SetTop(empty, 30);
                _chart.Children.Add(empty);
            }

            DrawLegend(rows);
            DrawTodayMarker(height);
        }

        private void DrawBar(TaskQuery.TimelineRow row, Color color, double top, double width)
        {
            double startX = (row.Start - _origin).TotalDays * _pixelsPerDay;
            double endX = (row.End - _origin).TotalDays * _pixelsPerDay;
            double barWidth = Math.Max(4, endX - startX);

            Border bar = new Border();
            bar.Height = row.Kind == "todo" ? 14 : 10;
            bar.CornerRadius = new CornerRadius(7);
            bar.Background = new LinearGradientBrush(
                ColorUtil.Blend(color, Colors.White, 0.05),
                ColorUtil.Blend(color, Colors.White, 0.45), 0);
            bar.Width = barWidth;
            bar.Tag = row;
            bar.ToolTip = BuildTooltip(row);
            bar.Cursor = Cursors.Hand;
            bar.MouseLeftButtonUp += delegate { OpenRow(row); };

            // Completed work reads lighter and stops short of the deadline.
            if (row.Todo != null && row.Todo.IsDone)
            {
                bar.Opacity = 0.55;
            }
            Canvas.SetLeft(bar, startX);
            Canvas.SetTop(bar, top + (RowHeight - bar.Height) / 2 - 2);
            _chart.Children.Add(bar);

            // Creation marker: the left cap of the span is when the item was created.
            if (row.Kind == "todo" || row.Kind == "competition")
            {
                Rectangle created = new Rectangle();
                created.Width = 3;
                created.Height = 20;
                created.Fill = UiKit.Alpha(ColorUtil.Shade(color, -0.3), 0.9);
                created.ToolTip = Loc.T("timeline.created") + ": " + Services.CalendarService.FormatDateTime(row.Created);
                Canvas.SetLeft(created, startX - 1);
                Canvas.SetTop(created, top + 3);
                _chart.Children.Add(created);
            }

            // Last modification marker.
            if (row.Kind == "todo")
            {
                double updatedX = (row.Updated - _origin).TotalDays * _pixelsPerDay;
                if (updatedX >= startX && updatedX <= startX + barWidth + 4)
                {
                    Rectangle updated = new Rectangle();
                    updated.Width = 2;
                    updated.Height = 18;
                    updated.Fill = UiKit.Brush("Brush.OnSurfaceVariant");
                    updated.Opacity = 0.75;
                    updated.ToolTip = Loc.T("timeline.updated") + ": " + Services.CalendarService.FormatDateTime(row.Updated);
                    Canvas.SetLeft(updated, updatedX);
                    Canvas.SetTop(updated, top + 4);
                    _chart.Children.Add(updated);
                }
            }

            // Deadline diamond.
            if (row.HasDeadline)
            {
                double dueX = (row.End - _origin).TotalDays * _pixelsPerDay;
                System.Windows.Shapes.Polygon diamond = new System.Windows.Shapes.Polygon();
                diamond.Points = new PointCollection
                {
                    new Point(6, 0), new Point(12, 6), new Point(6, 12), new Point(0, 6)
                };
                diamond.Fill = new SolidColorBrush(ColorUtil.Shade(color, -0.15));
                diamond.ToolTip = (row.Kind == "exam" ? Loc.T("timeline.exam") :
                                   row.Kind == "milestone" ? Loc.T("timeline.milestone") :
                                   row.Kind == "event" ? Loc.T("comp.events") : Loc.T("timeline.deadline")) +
                                  ": " + Services.CalendarService.FormatDateTime(row.End);
                Canvas.SetLeft(diamond, dueX - 6);
                Canvas.SetTop(diamond, top + 3);
                _chart.Children.Add(diamond);
            }
        }

        private static string BuildTooltip(TaskQuery.TimelineRow row)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine(row.Title);
            sb.AppendLine(row.CategoryName);
            sb.AppendLine(Loc.T("timeline.created") + ": " + Services.CalendarService.FormatDateTime(row.Created));
            if (row.Kind == "todo")
            {
                sb.AppendLine(Loc.T("timeline.updated") + ": " + Services.CalendarService.FormatDateTime(row.Updated));
            }
            sb.Append(row.HasDeadline
                ? Loc.T("timeline.deadline") + ": " + Services.CalendarService.FormatDateTime(row.End)
                : Loc.T("common.no_deadline"));
            return sb.ToString();
        }

        private void OpenRow(TaskQuery.TimelineRow row)
        {
            if (row.Todo != null) { _shell.Navigate("tasks", row.Todo.Id); }
            else if (row.Exam != null) { _shell.Navigate("exams", row.Exam.Id); }
            else if (row.Competition != null) { _shell.Navigate("competitions", row.Competition.Id); }
        }

        private void DrawHeader(double width)
        {
            int tickDays = Services.CalendarService.SuggestTickDays(_pixelsPerDay);
            _header.Width = width;

            Rectangle background = new Rectangle();
            background.Width = width;
            background.Height = 34;
            background.Fill = Brushes.Transparent;

            DateTime day = _origin;
            // Align the first tick to a natural boundary.
            if (tickDays >= 30)
            {
                day = new DateTime(_origin.Year, _origin.Month, 1);
            }
            else if (tickDays >= 7)
            {
                day = Services.CalendarService.StartOfWeek(_origin);
            }

            for (; day <= _origin.AddDays(_spanDays); day = day.AddDays(tickDays))
            {
                double x = (day - _origin).TotalDays * _pixelsPerDay;
                if (x < -40 || x > width + 40) { continue; }

                Rectangle tick = new Rectangle();
                tick.Width = 1;
                tick.Height = 28;
                tick.Fill = UiKit.Brush("Brush.Outline");
                tick.Opacity = 0.6;
                Canvas.SetLeft(tick, x);
                Canvas.SetTop(tick, 6);
                _header.Children.Add(tick);

                string text = tickDays >= 30
                    ? day.ToString("yyyy-MM")
                    : tickDays >= 7 ? day.ToString("MM-dd") : day.ToString("MM-dd");
                TextBlock caption = UiKit.Caption(text);
                Canvas.SetLeft(caption, x + 4);
                Canvas.SetTop(caption, 4);
                _header.Children.Add(caption);

                if (tickDays < 14)
                {
                    TextBlock weekday = UiKit.Caption(Services.CalendarService.WeekdayShortName(day.DayOfWeek));
                    weekday.Opacity = 0.7;
                    Canvas.SetLeft(weekday, x + 4);
                    Canvas.SetTop(weekday, 18);
                    _header.Children.Add(weekday);
                }
            }
        }

        private void DrawGrid(double height)
        {
            int tickDays = Services.CalendarService.SuggestTickDays(_pixelsPerDay);
            DateTime day = _origin;
            if (tickDays >= 30) { day = new DateTime(_origin.Year, _origin.Month, 1); }
            else if (tickDays >= 7) { day = Services.CalendarService.StartOfWeek(_origin); }

            for (; day <= _origin.AddDays(_spanDays); day = day.AddDays(tickDays))
            {
                double x = (day - _origin).TotalDays * _pixelsPerDay;
                Rectangle line = new Rectangle();
                line.Width = 1;
                line.Height = height;
                line.Fill = UiKit.Brush("Brush.OutlineVariant");
                line.Opacity = 0.5;
                Canvas.SetLeft(line, x);
                Canvas.SetTop(line, 0);
                _chart.Children.Add(line);
            }
        }

        private void DrawTodayMarker(double height)
        {
            double x = (DateTime.Now.Date - _origin).TotalDays * _pixelsPerDay;
            if (x < 0 || x > _chart.Width) { return; }
            Rectangle marker = new Rectangle();
            marker.Width = 2;
            marker.Height = height;
            marker.Fill = UiKit.Brush("Brush.Primary");
            marker.ToolTip = Loc.T("timeline.today_line");
            Canvas.SetLeft(marker, x);
            Canvas.SetTop(marker, 0);
            _chart.Children.Add(marker);

            Border tag = UiKit.Badge(Loc.T("common.today"), UiKit.Brush("Brush.Primary"), UiKit.Brush("Brush.OnPrimary"));
            Canvas.SetLeft(tag, x + 3);
            Canvas.SetTop(tag, 2);
            _chart.Children.Add(tag);
        }

        private void DrawLegend(List<TaskQuery.TimelineRow> rows)
        {
            List<string> shown = new List<string>();
            for (int i = 0; i < AppState.Data.Categories.Count; i++)
            {
                Category category = AppState.Data.Categories[i];
                shown.Add(category.Color + "|" + category.Name);
            }
            int limit = Math.Min(shown.Count, 8);
            for (int i = 0; i < limit; i++)
            {
                string[] parts = shown[i].Split('|');
                StackPanel item = new StackPanel();
                item.Orientation = Orientation.Horizontal;
                item.Margin = new Thickness(0, 0, 14, 4);
                item.Children.Add(UiKit.Dot(parts[0], 9));
                TextBlock label = UiKit.Caption(parts[1]);
                label.Margin = new Thickness(6, 0, 0, 0);
                item.Children.Add(label);
                _legend.Children.Add(item);
            }

            StackPanel legendTitle = new StackPanel();
            legendTitle.Orientation = Orientation.Horizontal;
            legendTitle.Margin = new Thickness(0, 0, 14, 4);
            legendTitle.Children.Add(UiKit.Icon("filter", 12, UiKit.Brush("Brush.OnSurfaceFaint")));
            TextBlock title = UiKit.Caption(Loc.T("timeline.legend") + ":");
            title.Margin = new Thickness(5, 0, 0, 0);
            legendTitle.Children.Add(title);
            _legend.Children.Insert(0, legendTitle);
        }
    }
}
