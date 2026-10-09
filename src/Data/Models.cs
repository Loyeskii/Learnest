using System;
using System.Collections.Generic;
using Learnest.Core;

namespace Learnest.Data
{
    /// <summary>
    /// Plain data transfer objects for the container database.  Each one knows how to move
    /// itself to and from a SQLite row so the repository stays free of mapping noise.
    ///
    /// Column order in <c>Columns</c> must match the order used by <c>FromRow</c>.
    /// </summary>
    public static class ThemeMode
    {
        public const string System = "system";
        public const string Light = "light";
        public const string Dark = "dark";

        public static bool IsValid(string value)
        {
            return value == System || value == Light || value == Dark;
        }
    }

    public static class TodoStatus
    {
        public const string Open = "open";
        public const string Doing = "doing";
        public const string Done = "done";
        public const string Cancelled = "cancelled";

        public static readonly string[] All = new string[] { Open, Doing, Done, Cancelled };

        public static bool IsFinished(string status)
        {
            return status == Done || status == Cancelled;
        }
    }

    public static class AttachmentOwner
    {
        public const string Todo = "todo";
        public const string Exam = "exam";
        public const string Competition = "competition";
        public const string Milestone = "milestone";
        public const string Event = "event";
    }

    public static class BackgroundMode
    {
        public const string None = "none";
        public const string Fill = "fill";         // 填充：等比缩放铺满，裁掉多余
        public const string Fit = "fit";           // 适应：等比缩放到完整可见
        public const string Stretch = "stretch";   // 拉伸：非等比铺满
        public const string Tile = "tile";         // 平铺
        public const string Center = "center";     // 居中：原始尺寸居中
    }

    // ------------------------------------------------------------------ Category
    public sealed class Category
    {
        public string Id = "";
        public string Name = "";
        public string Color = "#3B82F6";
        public string Description = "";
        public string Icon = "";
        public int SortOrder;
        public bool IsSystem;
        public string CreatedAt = "";
        public string UpdatedAt = "";

        /// <summary>System bucket that receives todos whose category was removed.</summary>
        public const string UncategorizedName = "Uncategorized";
        public const string UncategorizedColor = "#94A3B8";

        public bool IsUncategorized { get { return IsSystem && string.Equals(Name, UncategorizedName, StringComparison.OrdinalIgnoreCase); } }

        public static Category CreateUncategorized()
        {
            Category c = new Category();
            c.Id = "uncategorized";
            c.Name = UncategorizedName;
            c.Color = UncategorizedColor;
            c.Description = "未指定分类的待办事项";
            c.IsSystem = true;
            c.SortOrder = 9999;
            c.CreatedAt = IsoTime.Now();
            c.UpdatedAt = c.CreatedAt;
            return c;
        }

        public static readonly string[] Columns = new string[]
        {
            "id", "name", "color", "description", "icon", "sort_order", "is_system", "created_at", "updated_at"
        };

        public object[] ToRow()
        {
            return new object[]
            {
                Id, Name, Color, Description, Icon, (long)SortOrder, IsSystem ? 1L : 0L, CreatedAt, UpdatedAt
            };
        }

        public static Category FromRow(SqliteStatement s)
        {
            Category c = new Category();
            c.Id = s.GetString(0);
            c.Name = s.GetString(1);
            c.Color = s.GetString(2);
            c.Description = s.GetString(3);
            c.Icon = s.GetString(4);
            c.SortOrder = s.GetInt(5);
            c.IsSystem = s.GetBool(6);
            c.CreatedAt = s.GetString(7);
            c.UpdatedAt = s.GetString(8);
            return c;
        }

        public Category Clone()
        {
            return (Category)MemberwiseClone();
        }
    }

    // ------------------------------------------------------------------ Attachment
    public sealed class Attachment
    {
        public string Id = "";
        public string OwnerKind = AttachmentOwner.Todo;
        public string OwnerId = "";
        public string FileName = "";
        /// <summary>Always relative to the container root, e.g. "attachments/todo/&lt;id&gt;/hw1.pdf".</summary>
        public string RelativePath = "";
        public long SizeBytes;
        public string Mime = "";
        public string Category = "";
        public string Note = "";
        public bool IsFolder;
        public string CreatedAt = "";
        public int SortOrder;

        public string DisplaySize
        {
            get
            {
                if (IsFolder) { return "文件夹"; }
                if (SizeBytes < 1024) { return SizeBytes + " B"; }
                if (SizeBytes < 1024 * 1024) { return (SizeBytes / 1024.0).ToString("0.#") + " KB"; }
                if (SizeBytes < 1024L * 1024 * 1024) { return (SizeBytes / (1024.0 * 1024)).ToString("0.##") + " MB"; }
                return (SizeBytes / (1024.0 * 1024 * 1024)).ToString("0.##") + " GB";
            }
        }

        public static readonly string[] Columns = new string[]
        {
            "id", "owner_kind", "owner_id", "file_name", "relative_path", "size_bytes", "mime",
            "category", "note", "is_folder", "created_at", "sort_order"
        };

        public object[] ToRow()
        {
            return new object[]
            {
                Id, OwnerKind, OwnerId, FileName, RelativePath, SizeBytes, Mime,
                Category, Note, IsFolder ? 1L : 0L, CreatedAt, (long)SortOrder
            };
        }

        public static Attachment FromRow(SqliteStatement s)
        {
            Attachment a = new Attachment();
            a.Id = s.GetString(0);
            a.OwnerKind = s.GetString(1);
            a.OwnerId = s.GetString(2);
            a.FileName = s.GetString(3);
            a.RelativePath = s.GetString(4);
            a.SizeBytes = s.GetInt64(5);
            a.Mime = s.GetString(6);
            a.Category = s.GetString(7);
            a.Note = s.GetString(8);
            a.IsFolder = s.GetBool(9);
            a.CreatedAt = s.GetString(10);
            a.SortOrder = s.GetInt(11);
            return a;
        }
    }

    // ------------------------------------------------------------------ Todo
    public sealed class Todo
    {
        public string Id = "";
        public string Title = "";
        public string CategoryId = "";
        public string Status = TodoStatus.Open;
        public int Priority = 1;
        public double Progress;
        public string DueAt = "";
        public string StartAt = "";
        public string CompletedAt = "";
        public string CreatedAt = "";
        public string UpdatedAt = "";
        /// <summary>Advanced record: markdown body opened from the "高级记录" button.</summary>
        public string NoteMarkdown = "";
        public string NoteUpdatedAt = "";
        public int LeadMinutes = 1440;
        public string RepeatRule = "";
        public int SortOrder;
        public string ColorOverride = "";
        public List<string> Tags = new List<string>();
        public List<string> Steps = new List<string>();

        // Populated by the repository, not stored on the todos row.
        public List<string> CompetitionIds = new List<string>();
        public List<string> MilestoneIds = new List<string>();
        public List<Attachment> Attachments = new List<Attachment>();

        public bool IsDone { get { return Status == TodoStatus.Done; } }
        public bool IsOpen { get { return Status == TodoStatus.Open || Status == TodoStatus.Doing; } }
        public bool HasNote { get { return !string.IsNullOrEmpty(NoteMarkdown); } }
        public int AttachmentCount { get { return Attachments.Count; } }

        /// <summary>Detached copy used by the editors so Cancel really cancels.</summary>
        public Todo Clone()
        {
            Todo copy = (Todo)MemberwiseClone();
            copy.Tags = new List<string>(Tags);
            copy.Steps = new List<string>(Steps);
            copy.CompetitionIds = new List<string>(CompetitionIds);
            copy.MilestoneIds = new List<string>(MilestoneIds);
            copy.Attachments = new List<Attachment>(Attachments);
            return copy;
        }

        public DateTime? DueDate
        {
            get
            {
                DateTime d;
                if (IsoTime.TryParse(DueAt, out d)) { return d; }
                return null;
            }
        }

        public DateTime CreatedDate
        {
            get
            {
                DateTime d;
                if (IsoTime.TryParse(CreatedAt, out d)) { return d; }
                return DateTime.Now;
            }
        }

        public DateTime UpdatedDate
        {
            get
            {
                DateTime d;
                if (IsoTime.TryParse(UpdatedAt, out d)) { return d; }
                return CreatedDate;
            }
        }

        public DateTime? CompletedDate
        {
            get
            {
                DateTime d;
                if (IsoTime.TryParse(CompletedAt, out d)) { return d; }
                return null;
            }
        }

        public static readonly string[] Columns = new string[]
        {
            "id", "title", "category_id", "status", "priority", "progress", "due_at", "start_at",
            "completed_at", "created_at", "updated_at", "note_markdown", "note_updated_at",
            "lead_minutes", "repeat_rule", "sort_order", "color_override", "tags", "steps"
        };

        public object[] ToRow()
        {
            return new object[]
            {
                Id, Title, CategoryId, Status, (long)Priority, Progress, DueAt, StartAt,
                CompletedAt, CreatedAt, UpdatedAt, NoteMarkdown, NoteUpdatedAt,
                (long)LeadMinutes, RepeatRule, (long)SortOrder, ColorOverride,
                StringList.Codec.Encode(Tags), StringList.Codec.Encode(Steps)
            };
        }

        /// <summary>Public list to JSON array helper, shared with the competition pack writer.</summary>
        public static JsonArray StringArrayOf(List<string> items)
        {
            JsonArray array = new JsonArray();
            if (items == null) { return array; }
            for (int i = 0; i < items.Count; i++) { array.Add(items[i]); }
            return array;
        }

        internal static JsonArray StringArray(List<string> items)
        {
            return StringArrayOf(items);
        }

        public static Todo FromRow(SqliteStatement s)
        {
            Todo t = new Todo();
            t.Id = s.GetString(0);
            t.Title = s.GetString(1);
            t.CategoryId = s.GetString(2);
            t.Status = s.GetString(3);
            t.Priority = s.GetInt(4);
            t.Progress = s.GetDouble(5);
            t.DueAt = s.GetString(6);
            t.StartAt = s.GetString(7);
            t.CompletedAt = s.GetString(8);
            t.CreatedAt = s.GetString(9);
            t.UpdatedAt = s.GetString(10);
            t.NoteMarkdown = s.GetString(11);
            t.NoteUpdatedAt = s.GetString(12);
            t.LeadMinutes = s.GetInt(13);
            t.RepeatRule = s.GetString(14);
            t.SortOrder = s.GetInt(15);
            t.ColorOverride = s.GetString(16);
            t.Tags = StringList.Codec.Decode(s.GetString(17));
            t.Steps = StringList.Codec.Decode(s.GetString(18));
            return t;
        }
    }

    // ------------------------------------------------------------------ Exam
    public sealed class Exam
    {
        public string Id = "";
        public string Title = "";
        public string Course = "";
        public string CategoryId = "";
        public string StartAt = "";
        public string EndAt = "";
        public string Location = "";
        public string Seat = "";
        public string Status = "planned";     // planned | done | cancelled
        public string NoteMarkdown = "";
        public int RemindMinutes = 1440;
        public bool PinCountdown = true;
        public string Score = "";
        public string CreatedAt = "";
        public string UpdatedAt = "";
        public int SortOrder;
        public List<Attachment> Attachments = new List<Attachment>();

        public bool IsDone { get { return string.Equals(Status, "done", StringComparison.OrdinalIgnoreCase); } }

        public DateTime StartDate
        {
            get
            {
                DateTime d;
                if (IsoTime.TryParse(StartAt, out d)) { return d; }
                return DateTime.Now;
            }
        }

        public DateTime? EndDate
        {
            get
            {
                DateTime d;
                if (IsoTime.TryParse(EndAt, out d)) { return d; }
                return null;
            }
        }

        public static readonly string[] Columns = new string[]
        {
            "id", "title", "course", "category_id", "start_at", "end_at", "location", "seat",
            "status", "note_markdown", "remind_minutes", "pin_countdown", "score",
            "created_at", "updated_at", "sort_order"
        };

        public object[] ToRow()
        {
            return new object[]
            {
                Id, Title, Course, CategoryId, StartAt, EndAt, Location, Seat,
                Status, NoteMarkdown, (long)RemindMinutes, PinCountdown ? 1L : 0L, Score,
                CreatedAt, UpdatedAt, (long)SortOrder
            };
        }

        public static Exam FromRow(SqliteStatement s)
        {
            Exam e = new Exam();
            e.Id = s.GetString(0);
            e.Title = s.GetString(1);
            e.Course = s.GetString(2);
            e.CategoryId = s.GetString(3);
            e.StartAt = s.GetString(4);
            e.EndAt = s.GetString(5);
            e.Location = s.GetString(6);
            e.Seat = s.GetString(7);
            e.Status = s.GetString(8);
            e.NoteMarkdown = s.GetString(9);
            e.RemindMinutes = s.GetInt(10);
            e.PinCountdown = s.GetBool(11);
            e.Score = s.GetString(12);
            e.CreatedAt = s.GetString(13);
            e.UpdatedAt = s.GetString(14);
            e.SortOrder = s.GetInt(15);
            return e;
        }
    }

    // ------------------------------------------------------------------ Milestone
    public sealed class Milestone
    {
        public string Id = "";
        public string CompetitionId = "";
        public string Title = "";
        public string Detail = "";
        public string NoteMarkdown = "";
        public string DueAt = "";
        public string CompletedAt = "";
        public bool IsDone;
        public double Weight = 1;
        public string CreatedAt = "";
        public string UpdatedAt = "";
        public int SortOrder;

        public DateTime? DueDate
        {
            get
            {
                DateTime d;
                if (IsoTime.TryParse(DueAt, out d)) { return d; }
                return null;
            }
        }

        public static readonly string[] Columns = new string[]
        {
            "id", "competition_id", "title", "detail", "note_markdown", "due_at", "completed_at",
            "is_done", "weight", "created_at", "updated_at", "sort_order"
        };

        public object[] ToRow()
        {
            return new object[]
            {
                Id, CompetitionId, Title, Detail, NoteMarkdown, DueAt, CompletedAt,
                IsDone ? 1L : 0L, Weight, CreatedAt, UpdatedAt, (long)SortOrder
            };
        }

        public static Milestone FromRow(SqliteStatement s)
        {
            Milestone m = new Milestone();
            m.Id = s.GetString(0);
            m.CompetitionId = s.GetString(1);
            m.Title = s.GetString(2);
            m.Detail = s.GetString(3);
            m.NoteMarkdown = s.GetString(4);
            m.DueAt = s.GetString(5);
            m.CompletedAt = s.GetString(6);
            m.IsDone = s.GetBool(7);
            m.Weight = s.GetDouble(8);
            m.CreatedAt = s.GetString(9);
            m.UpdatedAt = s.GetString(10);
            m.SortOrder = s.GetInt(11);
            return m;
        }
    }

    // ------------------------------------------------------------------ Competition event
    public sealed class CompetitionEvent
    {
        public string Id = "";
        public string CompetitionId = "";
        public string Title = "";
        public string Kind = "event";   // registration | event | submission | result | other
        public string StartAt = "";
        public string EndAt = "";
        public string Location = "";
        public string Detail = "";
        public string NoteMarkdown = "";
        public bool AllDay;
        public string CreatedAt = "";
        public string UpdatedAt = "";
        public int SortOrder;

        public DateTime StartDate
        {
            get
            {
                DateTime d;
                if (IsoTime.TryParse(StartAt, out d)) { return d; }
                return DateTime.Now;
            }
        }

        public static readonly string[] Columns = new string[]
        {
            "id", "competition_id", "title", "kind", "start_at", "end_at", "location", "detail",
            "note_markdown", "all_day", "created_at", "updated_at", "sort_order"
        };

        public object[] ToRow()
        {
            return new object[]
            {
                Id, CompetitionId, Title, Kind, StartAt, EndAt, Location, Detail,
                NoteMarkdown, AllDay ? 1L : 0L, CreatedAt, UpdatedAt, (long)SortOrder
            };
        }

        public static CompetitionEvent FromRow(SqliteStatement s)
        {
            CompetitionEvent e = new CompetitionEvent();
            e.Id = s.GetString(0);
            e.CompetitionId = s.GetString(1);
            e.Title = s.GetString(2);
            e.Kind = s.GetString(3);
            e.StartAt = s.GetString(4);
            e.EndAt = s.GetString(5);
            e.Location = s.GetString(6);
            e.Detail = s.GetString(7);
            e.NoteMarkdown = s.GetString(8);
            e.AllDay = s.GetBool(9);
            e.CreatedAt = s.GetString(10);
            e.UpdatedAt = s.GetString(11);
            e.SortOrder = s.GetInt(12);
            return e;
        }
    }

    // ------------------------------------------------------------------ Competition
    public sealed class Competition
    {
        public string Id = "";
        public string Name = "";
        /// <summary>Label other todos reference.  Kept unique by the repository.</summary>
        public string Tag = "";
        public string Color = "#8B5CF6";
        public string Organizer = "";
        public string Stage = "planned";   // planned | registered | in_progress | submitted | finished | archived
        public string Level = "";
        public string Description = "";
        public string NoteMarkdown = "";
        public string StartAt = "";
        public string EndAt = "";
        public double Progress;
        public bool Starred;
        public List<string> Team = new List<string>();
        public List<string> Links = new List<string>();
        public List<string> AttachmentCategories = new List<string>();
        public string CreatedAt = "";
        public string UpdatedAt = "";
        public int SortOrder;

        public List<Milestone> Milestones = new List<Milestone>();
        public List<CompetitionEvent> Events = new List<CompetitionEvent>();
        public List<Attachment> Attachments = new List<Attachment>();
        /// <summary>Todos referencing this competition through the link table or its tag.</summary>
        public List<Todo> LinkedTodos = new List<Todo>();

        public static readonly string[] Columns = new string[]
        {
            "id", "name", "tag", "color", "organizer", "stage", "level", "description",
            "note_markdown", "start_at", "end_at", "progress", "starred", "team", "links",
            "attachment_categories", "created_at", "updated_at", "sort_order"
        };

        public object[] ToRow()
        {
            return new object[]
            {
                Id, Name, Tag, Color, Organizer, Stage, Level, Description,
                NoteMarkdown, StartAt, EndAt, Progress, Starred ? 1L : 0L,
                StringList.Codec.Encode(Team), StringList.Codec.Encode(Links),
                StringList.Codec.Encode(AttachmentCategories), CreatedAt, UpdatedAt, (long)SortOrder
            };
        }

        public static Competition FromRow(SqliteStatement s)
        {
            Competition c = new Competition();
            c.Id = s.GetString(0);
            c.Name = s.GetString(1);
            c.Tag = s.GetString(2);
            c.Color = s.GetString(3);
            c.Organizer = s.GetString(4);
            c.Stage = s.GetString(5);
            c.Level = s.GetString(6);
            c.Description = s.GetString(7);
            c.NoteMarkdown = s.GetString(8);
            c.StartAt = s.GetString(9);
            c.EndAt = s.GetString(10);
            c.Progress = s.GetDouble(11);
            c.Starred = s.GetBool(12);
            c.Team = StringList.Codec.Decode(s.GetString(13));
            c.Links = StringList.Codec.Decode(s.GetString(14));
            c.AttachmentCategories = StringList.Codec.Decode(s.GetString(15));
            c.CreatedAt = s.GetString(16);
            c.UpdatedAt = s.GetString(17);
            c.SortOrder = s.GetInt(18);
            return c;
        }

        public DateTime? EndDate
        {
            get
            {
                DateTime d;
                if (IsoTime.TryParse(EndAt, out d)) { return d; }
                return null;
            }
        }

        /// <summary>
        /// Full JSON projection including milestones, events and attachments.  Used by the
        /// "pack and go" archive so a competition can be rebuilt without the whole container.
        /// </summary>
        public JsonValue ToJson()
        {
            JsonObject o = new JsonObject();
            o["id"] = JsonValue.String(Id);
            o["name"] = JsonValue.String(Name);
            o["tag"] = JsonValue.String(Tag);
            o["color"] = JsonValue.String(Color);
            o["organizer"] = JsonValue.String(Organizer);
            o["stage"] = JsonValue.String(Stage);
            o["level"] = JsonValue.String(Level);
            o["description"] = JsonValue.String(Description);
            o["noteMarkdown"] = JsonValue.String(NoteMarkdown);
            o["startAt"] = JsonValue.String(StartAt);
            o["endAt"] = JsonValue.String(EndAt);
            o["progress"] = JsonValue.Number(Progress);
            o["starred"] = JsonValue.Bool(Starred);
            o["createdAt"] = JsonValue.String(CreatedAt);
            o["updatedAt"] = JsonValue.String(UpdatedAt);
            o["sortOrder"] = JsonValue.Number(SortOrder);
            o["team"] = JsonValue.Array(Todo.StringArrayOf(Team));
            o["links"] = JsonValue.Array(Todo.StringArrayOf(Links));
            o["attachmentCategories"] = JsonValue.Array(Todo.StringArrayOf(AttachmentCategories));

            JsonArray milestones = new JsonArray();
            for (int i = 0; i < Milestones.Count; i++)
            {
                Milestone item = Milestones[i];
                JsonObject m = new JsonObject();
                m["id"] = JsonValue.String(item.Id);
                m["title"] = JsonValue.String(item.Title);
                m["detail"] = JsonValue.String(item.Detail);
                m["noteMarkdown"] = JsonValue.String(item.NoteMarkdown);
                m["dueAt"] = JsonValue.String(item.DueAt);
                m["completedAt"] = JsonValue.String(item.CompletedAt);
                m["isDone"] = JsonValue.Bool(item.IsDone);
                m["weight"] = JsonValue.Number(item.Weight);
                m["sortOrder"] = JsonValue.Number(item.SortOrder);
                milestones.Add(JsonValue.Object(m));
            }
            o["milestones"] = JsonValue.Array(milestones);

            JsonArray events = new JsonArray();
            for (int i = 0; i < Events.Count; i++)
            {
                CompetitionEvent item = Events[i];
                JsonObject e = new JsonObject();
                e["id"] = JsonValue.String(item.Id);
                e["title"] = JsonValue.String(item.Title);
                e["kind"] = JsonValue.String(item.Kind);
                e["startAt"] = JsonValue.String(item.StartAt);
                e["endAt"] = JsonValue.String(item.EndAt);
                e["location"] = JsonValue.String(item.Location);
                e["detail"] = JsonValue.String(item.Detail);
                e["noteMarkdown"] = JsonValue.String(item.NoteMarkdown);
                e["allDay"] = JsonValue.Bool(item.AllDay);
                events.Add(JsonValue.Object(e));
            }
            o["events"] = JsonValue.Array(events);

            JsonArray attachments = new JsonArray();
            for (int i = 0; i < Attachments.Count; i++)
            {
                Attachment item = Attachments[i];
                JsonObject a = new JsonObject();
                a["fileName"] = JsonValue.String(item.FileName);
                a["relativePath"] = JsonValue.String(item.RelativePath);
                a["sizeBytes"] = JsonValue.Number(item.SizeBytes);
                a["category"] = JsonValue.String(item.Category);
                a["isFolder"] = JsonValue.Bool(item.IsFolder);
                attachments.Add(JsonValue.Object(a));
            }
            o["attachments"] = JsonValue.Array(attachments);
            return JsonValue.Object(o);
        }

        public static Competition FromJson(JsonValue v)
        {
            Competition c = new Competition();
            c.Id = v.Get("id").AsString(IsoTime.ShortId("comp"));
            c.Name = v.Get("name").AsString("");
            c.Tag = v.Get("tag").AsString("");
            c.Color = v.Get("color").AsString("#8B5CF6");
            c.Organizer = v.Get("organizer").AsString("");
            c.Stage = v.Get("stage").AsString("planned");
            c.Level = v.Get("level").AsString("");
            c.Description = v.Get("description").AsString("");
            c.NoteMarkdown = v.Get("noteMarkdown").AsString("");
            c.StartAt = v.Get("startAt").AsString("");
            c.EndAt = v.Get("endAt").AsString("");
            c.Progress = v.Get("progress").AsDouble(0);
            c.Starred = v.Get("starred").AsBool(false);
            c.CreatedAt = v.Get("createdAt").AsString(IsoTime.Now());
            c.UpdatedAt = v.Get("updatedAt").AsString(c.CreatedAt);
            c.SortOrder = v.Get("sortOrder").AsInt(0);
            c.Team = v.Get("team").AsStringList();
            c.Links = v.Get("links").AsStringList();
            c.AttachmentCategories = v.Get("attachmentCategories").AsStringList();

            JsonArray milestones = v.Get("milestones").AsArray();
            for (int i = 0; i < milestones.Count; i++)
            {
                JsonValue item = milestones[i];
                Milestone m = new Milestone();
                m.Id = item.Get("id").AsString(IsoTime.ShortId("ms"));
                m.CompetitionId = c.Id;
                m.Title = item.Get("title").AsString("");
                m.Detail = item.Get("detail").AsString("");
                m.NoteMarkdown = item.Get("noteMarkdown").AsString("");
                m.DueAt = item.Get("dueAt").AsString("");
                m.CompletedAt = item.Get("completedAt").AsString("");
                m.IsDone = item.Get("isDone").AsBool(false);
                m.Weight = item.Get("weight").AsDouble(1);
                m.SortOrder = item.Get("sortOrder").AsInt(i);
                c.Milestones.Add(m);
            }

            JsonArray events = v.Get("events").AsArray();
            for (int i = 0; i < events.Count; i++)
            {
                JsonValue item = events[i];
                CompetitionEvent e = new CompetitionEvent();
                e.Id = item.Get("id").AsString(IsoTime.ShortId("evt"));
                e.CompetitionId = c.Id;
                e.Title = item.Get("title").AsString("");
                e.Kind = item.Get("kind").AsString("event");
                e.StartAt = item.Get("startAt").AsString("");
                e.EndAt = item.Get("endAt").AsString("");
                e.Location = item.Get("location").AsString("");
                e.Detail = item.Get("detail").AsString("");
                e.NoteMarkdown = item.Get("noteMarkdown").AsString("");
                e.AllDay = item.Get("allDay").AsBool(false);
                c.Events.Add(e);
            }

            JsonArray attachments = v.Get("attachments").AsArray();
            for (int i = 0; i < attachments.Count; i++)
            {
                JsonValue item = attachments[i];
                Attachment a = new Attachment();
                a.Id = IsoTime.ShortId("att");
                a.OwnerKind = AttachmentOwner.Competition;
                a.OwnerId = c.Id;
                a.FileName = item.Get("fileName").AsString("");
                a.RelativePath = item.Get("relativePath").AsString("");
                a.SizeBytes = item.Get("sizeBytes").AsLong(0);
                a.Category = item.Get("category").AsString("");
                a.IsFolder = item.Get("isFolder").AsBool(false);
                c.Attachments.Add(a);
            }
            return c;
        }

        public double MilestoneProgress()
        {
            if (Milestones.Count == 0) { return Progress; }
            double total = 0;
            double done = 0;
            for (int i = 0; i < Milestones.Count; i++)
            {
                double weight = Milestones[i].Weight <= 0 ? 1 : Milestones[i].Weight;
                total += weight;
                if (Milestones[i].IsDone) { done += weight; }
            }
            return total <= 0 ? Progress : done / total;
        }

        public Milestone FindMilestone(string id)
        {
            for (int i = 0; i < Milestones.Count; i++)
            {
                if (Milestones[i].Id == id) { return Milestones[i]; }
            }
            return null;
        }
    }

    // ------------------------------------------------------------------ Activity
    public sealed class ActivityEntry
    {
        public string Id = "";
        public string At = "";
        public string Kind = "info";
        public string RefKind = "";
        public string RefId = "";
        public string Message = "";

        public static readonly string[] Columns = new string[]
        {
            "id", "at", "kind", "ref_kind", "ref_id", "message"
        };

        public object[] ToRow()
        {
            return new object[] { Id, At, Kind, RefKind, RefId, Message };
        }

        public static ActivityEntry FromRow(SqliteStatement s)
        {
            ActivityEntry a = new ActivityEntry();
            a.Id = s.GetString(0);
            a.At = s.GetString(1);
            a.Kind = s.GetString(2);
            a.RefKind = s.GetString(3);
            a.RefId = s.GetString(4);
            a.Message = s.GetString(5);
            return a;
        }
    }

    /// <summary>
    /// Encodes a string list into a single text column.  A unit separator (U+001F) is used
    /// instead of a comma so tags and steps may contain any punctuation, and a leading count
    /// marker keeps "one empty string" distinguishable from "no items".
    /// </summary>
    public static class StringList
    {
        public const char Separator = '\u001f';
        public const char RecordSeparator = '\u001e';

        public static class Codec
        {
            public static string Encode(List<string> items)
            {
                if (items == null || items.Count == 0) { return ""; }
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int i = 0; i < items.Count; i++)
                {
                    if (i > 0) { sb.Append(Separator); }
                    string value = items[i] ?? "";
                    sb.Append(value.Replace(Separator, ' ').Replace(RecordSeparator, ' '));
                }
                return sb.ToString();
            }

            public static List<string> Decode(string encoded)
            {
                List<string> list = new List<string>();
                if (string.IsNullOrEmpty(encoded)) { return list; }
                string[] parts = encoded.Split(Separator);
                for (int i = 0; i < parts.Length; i++)
                {
                    string value = parts[i];
                    if (value.Length > 0) { list.Add(value); }
                }
                return list;
            }
        }
    }

    /// <summary>Markdown checklist helpers shared by the note editor and the task list.</summary>
    public static class MarkdownSteps
    {
        /// <summary>Pull "- [ ] text" / "- [x] text" lines out of a markdown body.</summary>
        public static List<string> ExtractChecklist(string markdown)
        {
            List<string> steps = new List<string>();
            if (string.IsNullOrEmpty(markdown)) { return steps; }
            string[] lines = markdown.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                bool bullet = line.StartsWith("- [", StringComparison.Ordinal) || line.StartsWith("* [", StringComparison.Ordinal);
                if (!bullet) { continue; }
                if (line.Length < 6) { continue; }
                string rest = line.Substring(4).TrimStart();
                rest = rest.TrimStart(']').TrimStart(' ');
                if (rest.Length > 0) { steps.Add(rest); }
            }
            return steps;
        }

        public static string StripMarkdown(string markdown, int maxLength)
        {
            if (string.IsNullOrEmpty(markdown)) { return ""; }
            string text = markdown.Replace("\r\n", "\n");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"```[\s\S]*?```", " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"`([^`]*)`", "$1");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"!\[[^\]]*\]\([^)]*\)", " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\[([^\]]*)\]\([^)]*\)", "$1");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^\s{0,3}#{1,6}\s*", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^\s{0,3}>\s?", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^\s{0,3}[-*+]\s+\[[ xX]\]\s*", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^\s{0,3}[-*+]\s+", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"[*_~]{1,3}", "");
            text = text.Replace("\n", " ").Trim();
            while (text.Contains("  ")) { text = text.Replace("  ", " "); }
            if (maxLength > 0 && text.Length > maxLength) { text = text.Substring(0, maxLength) + "…"; }
            return text;
        }
    }
}
