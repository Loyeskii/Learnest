using System;
using System.Collections.Generic;
using Learnest.Data;

using Learnest.Core;
namespace Learnest.Services
{
    /// <summary>How urgent an item is right now.  Drives colours, ordering and the flash alert.</summary>
    public enum Urgency
    {
        None = 0,
        Later = 1,
        Soon = 2,
        Today = 3,
        Critical = 4,
        Overdue = 5,
        Done = 6
    }

    public sealed class TaskFilter
    {
        public string CategoryId;
        public string CompetitionId;
        public string Search;
        /// <summary>open | all | done | today | week | overdue | no_deadline | competitions</summary>
        public string Status = "open";
        public bool SortByDue = true;

        public static TaskFilter Open()
        {
            return new TaskFilter();
        }
    }

    /// <summary>
    /// Read-side queries over the loaded snapshot: urgency, filtering, the deadline alert window,
    /// the widget list, the timeline projection and dashboard counters.  Pure functions over
    /// in-memory data, so scrolling and repainting never touch the database.
    /// </summary>
    public static class TaskQuery
    {
        public static Urgency UrgencyOf(Todo todo, DateTime now)
        {
            if (todo == null) { return Urgency.None; }
            if (todo.IsDone || todo.Status == TodoStatus.Cancelled) { return Urgency.Done; }
            DateTime? due = todo.DueDate;
            if (!due.HasValue) { return Urgency.None; }
            TimeSpan span = due.Value - now;
            if (span.Ticks < 0) { return Urgency.Overdue; }
            if (span.TotalHours <= 6) { return Urgency.Critical; }
            if (span.TotalHours <= 24) { return Urgency.Today; }
            int lead = todo.LeadMinutes > 0 ? todo.LeadMinutes : 1440;
            if (span.TotalMinutes <= lead) { return Urgency.Soon; }
            return Urgency.Later;
        }

        public static Urgency UrgencyOf(Exam exam, DateTime now)
        {
            if (exam == null) { return Urgency.None; }
            if (exam.IsDone) { return Urgency.Done; }
            TimeSpan span = exam.StartDate - now;
            if (span.Ticks < 0) { return Urgency.Overdue; }
            if (span.TotalHours <= 24) { return Urgency.Critical; }
            if (span.TotalDays <= 3) { return Urgency.Today; }
            if (span.TotalDays <= 14) { return Urgency.Soon; }
            return Urgency.Later;
        }

        public static string UrgencyKey(Urgency urgency)
        {
            switch (urgency)
            {
                case Urgency.Overdue: return "overdue";
                case Urgency.Critical: return "critical";
                case Urgency.Today: return "today";
                case Urgency.Soon: return "soon";
                case Urgency.Done: return "done";
                default: return "later";
            }
        }

        public static string UrgencyLabel(Urgency urgency)
        {
            switch (urgency)
            {
                case Urgency.Overdue: return Loc.T("common.overdue");
                case Urgency.Critical: return Loc.T("alert.deadline_approaching", 6);
                case Urgency.Today: return Loc.T("common.today");
                case Urgency.Soon: return Loc.T("common.due_soon");
                case Urgency.Done: return Loc.T("common.done");
                default: return Loc.T("common.no_deadline");
            }
        }

        // ---------------------------------------------------------------- filtering
        public static bool Matches(Todo todo, TaskFilter filter, DateTime now)
        {
            if (todo == null) { return false; }
            if (filter == null) { filter = TaskFilter.Open(); }
            string status = (filter.Status ?? "open").ToLowerInvariant();

            switch (status)
            {
                case "open":
                    if (!todo.IsOpen) { return false; }
                    break;
                case "done":
                    if (!todo.IsDone) { return false; }
                    break;
                case "all":
                    break;
                case "today":
                    if (!todo.IsOpen) { return false; }
                    if (!todo.DueDate.HasValue || todo.DueDate.Value.Date != now.Date) { return false; }
                    break;
                case "week":
                    if (!todo.IsOpen) { return false; }
                    if (!todo.DueDate.HasValue || todo.DueDate.Value.Date > now.Date.AddDays(7)) { return false; }
                    break;
                case "overdue":
                    if (!todo.IsOpen) { return false; }
                    if (!todo.DueDate.HasValue || todo.DueDate.Value >= now) { return false; }
                    break;
                case "no_deadline":
                    if (!todo.IsOpen) { return false; }
                    if (todo.DueDate.HasValue) { return false; }
                    break;
                case "competitions":
                    if (todo.CompetitionIds.Count == 0) { return false; }
                    break;
            }

            if (!string.IsNullOrEmpty(filter.CategoryId) && todo.CategoryId != filter.CategoryId) { return false; }

            if (!string.IsNullOrEmpty(filter.CompetitionId) && !ReferencesCompetition(todo, filter.CompetitionId))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(filter.Search))
            {
                string needle = filter.Search.Trim();
                if (!Contains(todo.Title, needle) &&
                    !Contains(todo.NoteMarkdown, needle) &&
                    !ContainsList(todo.Tags, needle) &&
                    !ContainsList(todo.CompetitionIds, needle))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>A todo may reference a competition by id, by its tag, or through its tag list.</summary>
        public static bool ReferencesCompetition(Todo todo, string competitionIdOrTag)
        {
            if (todo == null || string.IsNullOrEmpty(competitionIdOrTag)) { return false; }
            Competition competition = AppState.Data.FindCompetition(competitionIdOrTag);
            string tag = competition != null ? competition.Tag : null;

            for (int i = 0; i < todo.CompetitionIds.Count; i++)
            {
                string reference = todo.CompetitionIds[i];
                if (reference == competitionIdOrTag) { return true; }
                if (!string.IsNullOrEmpty(tag) && string.Equals(reference, tag, StringComparison.OrdinalIgnoreCase)) { return true; }
                Competition other = AppState.Data.FindCompetition(reference);
                if (other != null && (other.Id == competitionIdOrTag ||
                    string.Equals(other.Tag, competitionIdOrTag, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(other.Name, competitionIdOrTag, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
            if (!string.IsNullOrEmpty(tag))
            {
                for (int i = 0; i < todo.Tags.Count; i++)
                {
                    if (string.Equals(todo.Tags[i], tag, StringComparison.OrdinalIgnoreCase)) { return true; }
                }
            }
            return false;
        }

        private static bool Contains(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack)) { return false; }
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsList(List<string> values, string needle)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (Contains(values[i], needle)) { return true; }
            }
            return false;
        }

        public static List<Todo> Apply(IEnumerable<Todo> source, TaskFilter filter, DateTime now)
        {
            List<Todo> result = new List<Todo>();
            foreach (Todo todo in source)
            {
                if (Matches(todo, filter, now)) { result.Add(todo); }
            }
            Sort(result, filter == null || filter.SortByDue ? "dueAsc" : "priority");
            return result;
        }

        /// <summary>Sorting shared by the lists and the pin widget.</summary>
        public static void Sort(List<Todo> items, string sortKey)
        {
            switch (sortKey)
            {
                case "dueDesc":
                    items.Sort(delegate(Todo a, Todo b) { return CompareByDue(b, a); });
                    return;
                case "modifiedDesc":
                    items.Sort(delegate(Todo a, Todo b) { return b.UpdatedDate.CompareTo(a.UpdatedDate); });
                    return;
                case "modifiedAsc":
                    items.Sort(delegate(Todo a, Todo b) { return a.UpdatedDate.CompareTo(b.UpdatedDate); });
                    return;
                case "createdDesc":
                    items.Sort(delegate(Todo a, Todo b) { return b.CreatedDate.CompareTo(a.CreatedDate); });
                    return;
                case "createdAsc":
                    items.Sort(delegate(Todo a, Todo b) { return a.CreatedDate.CompareTo(b.CreatedDate); });
                    return;
                case "priority":
                    items.Sort(delegate(Todo a, Todo b)
                    {
                        int priority = b.Priority.CompareTo(a.Priority);
                        return priority != 0 ? priority : CompareByDue(a, b);
                    });
                    return;
                case "title":
                    items.Sort(delegate(Todo a, Todo b) { return string.Compare(a.Title, b.Title, StringComparison.CurrentCulture); });
                    return;
                default:
                    items.Sort(delegate(Todo a, Todo b) { return CompareByDue(a, b); });
                    return;
            }
        }

        public static void Sort(List<Todo> items, bool byDue)
        {
            Sort(items, byDue ? "dueAsc" : "priority");
        }

        private static int CompareByDue(Todo a, Todo b)
        {
            DateTime? da = a.DueDate;
            DateTime? db = b.DueDate;
            if (!da.HasValue && !db.HasValue) { return CompareFallback(a, b); }
            if (!da.HasValue) { return 1; }
            if (!db.HasValue) { return -1; }
            int cmp = da.Value.CompareTo(db.Value);
            return cmp != 0 ? cmp : CompareFallback(a, b);
        }

        private static int CompareFallback(Todo a, Todo b)
        {
            int cmp = b.Priority.CompareTo(a.Priority);
            if (cmp != 0) { return cmp; }
            return string.Compare(a.Title, b.Title, StringComparison.CurrentCulture);
        }

        // ---------------------------------------------------------------- widget & alerts
        public static List<Todo> WidgetItems(string filter, string sortKey, DateTime now)
        {
            List<Todo> result = new List<Todo>();
            bool showCompleted = AppState.Settings.WidgetShowCompleted;
            List<Todo> todos = AppState.Data.Todos;
            for (int i = 0; i < todos.Count; i++)
            {
                Todo todo = todos[i];
                if (todo.Status == TodoStatus.Cancelled) { continue; }
                if (todo.IsDone && !showCompleted) { continue; }
                switch (filter)
                {
                    case "today":
                        if (!todo.DueDate.HasValue || todo.DueDate.Value.Date != now.Date) { continue; }
                        break;
                    case "week":
                        if (!todo.DueDate.HasValue || todo.DueDate.Value.Date > now.Date.AddDays(7)) { continue; }
                        break;
                    case "all":
                        break;
                    default:
                        if (!todo.DueDate.HasValue) { continue; }
                        if (todo.DueDate.Value.Date > now.Date.AddDays(30)) { continue; }
                        break;
                }
                result.Add(todo);
            }
            Sort(result, sortKey);
            return result;
        }

        /// <summary>
        /// True when a to-do has entered its reminder window: still unfinished, has a deadline, and
        /// that deadline is either already past or inside the alert lead time
        /// (settings.AlertLeadHours).
        ///
        /// This single rule drives the static red gradient border shown on reminding items in the
        /// task list and in the pin widget.  There is deliberately no animation: the reminder is a
        /// persistent visual state, not a flashing one.
        /// </summary>
        public static bool IsReminding(Todo todo, DateTime now)
        {
            if (todo == null || !todo.IsOpen) { return false; }
            DateTime? due = todo.DueDate;
            if (!due.HasValue) { return false; }
            if (due.Value <= now) { return true; }
            double leadHours = 72;
            if (AppState.SessionOrNull != null && AppState.Settings.AlertLeadHours > 0)
            {
                leadHours = AppState.Settings.AlertLeadHours;
            }
            return due.Value <= now.AddHours(leadHours);
        }

        public sealed class AlertHit
        {
            /// <summary>todo | exam | milestone</summary>
            public string Kind;
            public string Title;
            public string Subtitle;
            public DateTime Due;
            public Urgency Urgency;
            public string RefId;
            public string Color = "#3B82F6";
            public string CompetitionId;

            public string Key { get { return Kind + "|" + RefId + "|" + Due.ToString("yyyyMMddHHmm"); } }
        }

        /// <summary>
        /// Everything inside the alert window (settings.AlertLeadHours).  Only *unfinished* items
        /// are considered, which is what makes the pin widget's flashing alert trustworthy.
        /// </summary>
        public static List<AlertHit> Alerts(DateTime now)
        {
            List<AlertHit> hits = new List<AlertHit>();
            if (AppState.SessionOrNull == null) { return hits; }
            double leadHours = AppState.Settings.AlertLeadHours > 0 ? AppState.Settings.AlertLeadHours : 72;
            DateTime horizon = now.AddHours(leadHours);
            AppState.Snapshot data = AppState.Data;

            for (int i = 0; i < data.Todos.Count; i++)
            {
                Todo todo = data.Todos[i];
                if (!todo.IsOpen) { continue; }
                DateTime? due = todo.DueDate;
                if (!due.HasValue || due.Value > horizon) { continue; }
                AlertHit hit = new AlertHit();
                hit.Kind = "todo";
                hit.Title = todo.Title;
                hit.Subtitle = data.NameOf(todo.CategoryId);
                hit.Due = due.Value;
                hit.Urgency = UrgencyOf(todo, now);
                hit.RefId = todo.Id;
                hit.Color = AppState.ColorFor(todo);
                hits.Add(hit);
            }

            for (int i = 0; i < data.Exams.Count; i++)
            {
                Exam exam = data.Exams[i];
                if (exam.IsDone || string.Equals(exam.Status, "cancelled", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (exam.StartDate > horizon) { continue; }
                AlertHit hit = new AlertHit();
                hit.Kind = "exam";
                hit.Title = exam.Title;
                hit.Subtitle = string.IsNullOrEmpty(exam.Location) ? Loc.T("exams.title") : exam.Location;
                hit.Due = exam.StartDate;
                hit.Urgency = UrgencyOf(exam, now);
                hit.RefId = exam.Id;
                hit.Color = "#DC2626";
                hits.Add(hit);
            }

            for (int i = 0; i < data.Competitions.Count; i++)
            {
                Competition competition = data.Competitions[i];
                for (int j = 0; j < competition.Milestones.Count; j++)
                {
                    Milestone milestone = competition.Milestones[j];
                    if (milestone.IsDone || !milestone.DueDate.HasValue) { continue; }
                    if (milestone.DueDate.Value > horizon) { continue; }
                    AlertHit hit = new AlertHit();
                    hit.Kind = "milestone";
                    hit.Title = competition.Name + " · " + milestone.Title;
                    hit.Subtitle = Loc.T("timeline.milestone");
                    hit.Due = milestone.DueDate.Value;
                    TimeSpan span = hit.Due - now;
                    hit.Urgency = span.Ticks < 0 ? Urgency.Overdue : span.TotalHours <= 24 ? Urgency.Today : Urgency.Soon;
                    hit.RefId = milestone.Id;
                    hit.Color = competition.Color;
                    hit.CompetitionId = competition.Id;
                    hits.Add(hit);
                }
            }

            hits.Sort(delegate(AlertHit a, AlertHit b) { return a.Due.CompareTo(b.Due); });
            return hits;
        }

        // ---------------------------------------------------------------- timeline
        public sealed class TimelineRow
        {
            public Todo Todo;
            public Exam Exam;
            public Milestone Milestone;
            public CompetitionEvent Event;
            public Competition Competition;
            public DateTime Created;
            public DateTime Updated;
            public DateTime Start;
            public DateTime End;
            public bool HasDeadline;
            public string Color = "#3B82F6";
            public string CategoryName = "";
            public Urgency Urgency;
            /// <summary>todo | exam | competition | milestone | event</summary>
            public string Kind = "todo";

            public string Title
            {
                get
                {
                    if (Exam != null) { return Exam.Title; }
                    if (Milestone != null) { return Milestone.Title; }
                    if (Event != null) { return Event.Title; }
                    return Todo != null ? Todo.Title : "";
                }
            }
        }

        public sealed class TimelineOptions
        {
            public bool ShowTodos = true;
            public bool ShowExams = true;
            public bool ShowCompetitions = true;
            public bool ShowMilestones = true;
            public bool ShowEvents = true;
            public bool ShowCompleted;
            public string CategoryId;
            public string CompetitionId;
            public string Search;
        }

        public static List<TimelineRow> BuildTimeline(TimelineOptions options, DateTime from, DateTime to)
        {
            if (options == null) { options = new TimelineOptions(); }
            List<TimelineRow> rows = new List<TimelineRow>();
            AppState.Snapshot data = AppState.Data;
            DateTime now = DateTime.Now;

            if (options.ShowTodos)
            {
                for (int i = 0; i < data.Todos.Count; i++)
                {
                    Todo todo = data.Todos[i];
                    if (todo.IsDone && !options.ShowCompleted) { continue; }
                    if (!string.IsNullOrEmpty(options.CategoryId) && todo.CategoryId != options.CategoryId) { continue; }
                    if (!string.IsNullOrEmpty(options.CompetitionId) && !ReferencesCompetition(todo, options.CompetitionId)) { continue; }
                    if (!string.IsNullOrEmpty(options.Search) &&
                        todo.Title.IndexOf(options.Search, StringComparison.OrdinalIgnoreCase) < 0) { continue; }

                    DateTime created = todo.CreatedDate;
                    DateTime updated = todo.UpdatedDate;
                    DateTime start = todo.DueDate.HasValue && todo.DueDate.Value < created ? todo.DueDate.Value : created;
                    DateTime end = todo.DueDate.HasValue ? todo.DueDate.Value : (todo.IsDone ? updated : now);
                    if (end < start) { end = start; }
                    if (end < from || start > to) { continue; }

                    TimelineRow row = new TimelineRow();
                    row.Todo = todo;
                    row.Kind = "todo";
                    row.Created = created;
                    row.Updated = updated;
                    row.Start = start;
                    row.End = end;
                    row.HasDeadline = todo.DueDate.HasValue;
                    row.Color = AppState.ColorFor(todo);
                    row.CategoryName = data.NameOf(todo.CategoryId);
                    row.Urgency = UrgencyOf(todo, now);
                    rows.Add(row);
                }
            }

            if (options.ShowExams)
            {
                for (int i = 0; i < data.Exams.Count; i++)
                {
                    Exam exam = data.Exams[i];
                    DateTime start = exam.StartDate.AddHours(-2);
                    DateTime end = exam.EndDate.HasValue ? exam.EndDate.Value : exam.StartDate.AddHours(2);
                    if (end < from || start > to) { continue; }
                    TimelineRow row = new TimelineRow();
                    row.Exam = exam;
                    row.Kind = "exam";
                    row.Created = exam.StartDate;
                    row.Updated = exam.StartDate;
                    row.Start = start;
                    row.End = end;
                    row.HasDeadline = true;
                    row.Color = "#DC2626";
                    row.CategoryName = string.IsNullOrEmpty(exam.Course) ? Loc.T("exams.title") : exam.Course;
                    row.Urgency = UrgencyOf(exam, now);
                    rows.Add(row);
                }
            }

            for (int i = 0; i < data.Competitions.Count; i++)
            {
                Competition competition = data.Competitions[i];
                if (!string.IsNullOrEmpty(options.CompetitionId) && competition.Id != options.CompetitionId) { continue; }

                if (options.ShowCompetitions)
                {
                    DateTime start;
                    DateTime end;
                    if (IsoTime.TryParse(competition.StartAt, out start) && IsoTime.TryParse(competition.EndAt, out end) && end >= start)
                    {
                        if (!(end < from || start > to))
                        {
                            rows.Add(Marker(competition, null, null, "competition", start, end, competition.Color));
                        }
                    }
                }

                if (options.ShowMilestones)
                {
                    for (int j = 0; j < competition.Milestones.Count; j++)
                    {
                        Milestone milestone = competition.Milestones[j];
                        if (!milestone.DueDate.HasValue) { continue; }
                        DateTime at = milestone.DueDate.Value;
                        if (at < from || at > to) { continue; }
                        TimelineRow row = Marker(competition, milestone, null, "milestone", at, at, competition.Color);
                        row.Urgency = milestone.IsDone ? Urgency.Done
                            : (at < now ? Urgency.Overdue : (at - now).TotalDays <= 3 ? Urgency.Today : Urgency.Soon);
                        rows.Add(row);
                    }
                }

                if (options.ShowEvents)
                {
                    for (int j = 0; j < competition.Events.Count; j++)
                    {
                        CompetitionEvent entry = competition.Events[j];
                        DateTime at = entry.StartDate;
                        if (at < from || at > to) { continue; }
                        TimelineRow row = Marker(competition, null, entry, "event", at, at, competition.Color);
                        row.Urgency = at < now ? Urgency.Later : Urgency.Soon;
                        rows.Add(row);
                    }
                }
            }

            rows.Sort(delegate(TimelineRow a, TimelineRow b)
            {
                int cmp = a.Start.CompareTo(b.Start);
                return cmp != 0 ? cmp : string.Compare(a.Title, b.Title, StringComparison.CurrentCulture);
            });
            return rows;
        }

        private static TimelineRow Marker(Competition competition, Milestone milestone, CompetitionEvent entry,
            string kind, DateTime start, DateTime end, string color)
        {
            TimelineRow row = new TimelineRow();
            row.Competition = competition;
            row.Milestone = milestone;
            row.Event = entry;
            row.Kind = kind;
            row.Created = start;
            row.Updated = end;
            row.Start = start;
            row.End = end;
            row.HasDeadline = true;
            row.Color = color;
            row.CategoryName = competition.Tag;
            row.Urgency = Urgency.Later;
            return row;
        }

        // ---------------------------------------------------------------- dashboard helpers
        public sealed class Summary
        {
            public int Open;
            public int Overdue;
            public int DueToday;
            public int DueThisWeek;
            public int DoneTotal;
            public int DoneThisWeek;
            public int ExamCount;
            public int CompetitionCount;
            public int AttachmentCount;
            public long AttachmentBytes;

            public double CompletionRate
            {
                get
                {
                    int total = Open + DoneTotal;
                    return total == 0 ? 0 : (double)DoneTotal / total;
                }
            }
        }

        public static Summary Summarise(DateTime now)
        {
            Summary summary = new Summary();
            if (AppState.SessionOrNull == null) { return summary; }
            Repository.Counters counters = AppState.SessionOrNull.Repository.LoadCounters(now);
            summary.Open = counters.Open;
            summary.Overdue = counters.Overdue;
            summary.DueToday = counters.DueToday;
            summary.DueThisWeek = counters.DueThisWeek;
            summary.DoneTotal = counters.DoneTotal;
            summary.DoneThisWeek = counters.DoneThisWeek;
            summary.ExamCount = counters.ExamCount;
            summary.CompetitionCount = counters.CompetitionCount;

            AppState.Snapshot data = AppState.Data;
            for (int i = 0; i < data.Todos.Count; i++)
            {
                List<Attachment> attachments = data.Todos[i].Attachments;
                for (int j = 0; j < attachments.Count; j++)
                {
                    summary.AttachmentCount++;
                    summary.AttachmentBytes += attachments[j].SizeBytes;
                }
            }
            return summary;
        }

        public sealed class CategoryCount
        {
            public Category Category;
            public int Open;
            public int Done;
            public int Overdue;
        }

        public static List<CategoryCount> CategoryBreakdown(DateTime now)
        {
            List<CategoryCount> result = new List<CategoryCount>();
            AppState.Snapshot data = AppState.Data;
            for (int i = 0; i < data.Categories.Count; i++)
            {
                Category category = data.Categories[i];
                CategoryCount count = new CategoryCount();
                count.Category = category;
                for (int j = 0; j < data.Todos.Count; j++)
                {
                    Todo todo = data.Todos[j];
                    if (todo.CategoryId != category.Id) { continue; }
                    if (todo.IsDone) { count.Done++; continue; }
                    if (todo.Status == TodoStatus.Cancelled) { continue; }
                    count.Open++;
                    if (todo.DueDate.HasValue && todo.DueDate.Value < now) { count.Overdue++; }
                }
                result.Add(count);
            }
            result.Sort(delegate(CategoryCount a, CategoryCount b)
            {
                return (b.Open + b.Done).CompareTo(a.Open + a.Done);
            });
            return result;
        }

        /// <summary>Items due on a particular day, for the calendar day panel.</summary>
        public static List<TimelineRow> ItemsOn(DateTime day)
        {
            List<TimelineRow> items = new List<TimelineRow>();
            AppState.Snapshot data = AppState.Data;
            DateTime now = DateTime.Now;

            for (int i = 0; i < data.Todos.Count; i++)
            {
                Todo todo = data.Todos[i];
                if (!todo.DueDate.HasValue || todo.DueDate.Value.Date != day.Date) { continue; }
                TimelineRow row = new TimelineRow();
                row.Todo = todo;
                row.Kind = "todo";
                row.Start = todo.DueDate.Value;
                row.End = todo.DueDate.Value;
                row.HasDeadline = true;
                row.Color = AppState.ColorFor(todo);
                row.CategoryName = data.NameOf(todo.CategoryId);
                row.Urgency = UrgencyOf(todo, now);
                items.Add(row);
            }
            for (int i = 0; i < data.Exams.Count; i++)
            {
                Exam exam = data.Exams[i];
                if (exam.StartDate.Date != day.Date) { continue; }
                TimelineRow row = new TimelineRow();
                row.Exam = exam;
                row.Kind = "exam";
                row.Start = exam.StartDate;
                row.End = exam.EndDate.HasValue ? exam.EndDate.Value : exam.StartDate;
                row.HasDeadline = true;
                row.Color = "#DC2626";
                row.CategoryName = string.IsNullOrEmpty(exam.Course) ? Loc.T("exams.title") : exam.Course;
                row.Urgency = UrgencyOf(exam, now);
                items.Add(row);
            }
            for (int i = 0; i < data.Competitions.Count; i++)
            {
                Competition competition = data.Competitions[i];
                for (int j = 0; j < competition.Milestones.Count; j++)
                {
                    Milestone milestone = competition.Milestones[j];
                    if (!milestone.DueDate.HasValue || milestone.DueDate.Value.Date != day.Date) { continue; }
                    TimelineRow row = new TimelineRow();
                    row.Milestone = milestone;
                    row.Competition = competition;
                    row.Kind = "milestone";
                    row.Start = milestone.DueDate.Value;
                    row.End = milestone.DueDate.Value;
                    row.HasDeadline = true;
                    row.Color = competition.Color;
                    row.CategoryName = competition.Tag;
                    row.Urgency = milestone.IsDone ? Urgency.Done : Urgency.Today;
                    items.Add(row);
                }
                for (int j = 0; j < competition.Events.Count; j++)
                {
                    CompetitionEvent entry = competition.Events[j];
                    if (entry.StartDate.Date != day.Date) { continue; }
                    TimelineRow row = new TimelineRow();
                    row.Event = entry;
                    row.Competition = competition;
                    row.Kind = "event";
                    row.Start = entry.StartDate;
                    row.End = entry.StartDate;
                    row.HasDeadline = true;
                    row.Color = competition.Color;
                    row.CategoryName = competition.Tag;
                    row.Urgency = Urgency.Soon;
                    items.Add(row);
                }
            }
            items.Sort(delegate(TimelineRow a, TimelineRow b) { return a.Start.CompareTo(b.Start); });
            return items;
        }

        /// <summary>Days inside a range that carry at least one item, for the month grid dots.</summary>
        public static Dictionary<DateTime, int> LoadDensity(DateTime from, DateTime to)
        {
            Dictionary<DateTime, int> density = new Dictionary<DateTime, int>();
            AppState.Snapshot data = AppState.Data;
            for (int i = 0; i < data.Todos.Count; i++)
            {
                Todo todo = data.Todos[i];
                if (!todo.DueDate.HasValue) { continue; }
                Bump(density, todo.DueDate.Value.Date, from, to);
            }
            for (int i = 0; i < data.Exams.Count; i++)
            {
                Bump(density, data.Exams[i].StartDate.Date, from, to);
            }
            for (int i = 0; i < data.Competitions.Count; i++)
            {
                Competition competition = data.Competitions[i];
                for (int j = 0; j < competition.Milestones.Count; j++)
                {
                    if (competition.Milestones[j].DueDate.HasValue)
                    {
                        Bump(density, competition.Milestones[j].DueDate.Value.Date, from, to);
                    }
                }
                for (int j = 0; j < competition.Events.Count; j++)
                {
                    Bump(density, competition.Events[j].StartDate.Date, from, to);
                }
            }
            return density;
        }

        private static void Bump(Dictionary<DateTime, int> density, DateTime day, DateTime from, DateTime to)
        {
            if (day < from.Date || day > to.Date) { return; }
            int count;
            density.TryGetValue(day, out count);
            density[day] = count + 1;
        }

        /// <summary>All distinct tags used by todos, for the tag editor.</summary>
        public static List<string> AllTags()
        {
            List<string> tags = new List<string>();
            AppState.Snapshot data = AppState.Data;
            for (int i = 0; i < data.Todos.Count; i++)
            {
                for (int j = 0; j < data.Todos[i].Tags.Count; j++)
                {
                    string tag = data.Todos[i].Tags[j];
                    if (!tags.Contains(tag)) { tags.Add(tag); }
                }
            }
            tags.Sort(StringComparer.CurrentCultureIgnoreCase);
            return tags;
        }
    }
}
