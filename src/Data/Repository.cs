using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Learnest.Core;

namespace Learnest.Data
{
    /// <summary>
    /// All database reads and writes.  Learnest is a single user, offline application, so the
    /// repository loads the full working set into memory (a few thousand rows at most) and
    /// writes targeted SQL on every edit; the UI never blocks on a query.
    /// </summary>
    public sealed class Repository
    {
        private readonly ContainerSession _session;

        internal Repository(ContainerSession session)
        {
            _session = session;
        }

        private SqliteDatabase Db { get { return _session.Database; } }

        public ContainerSession Session { get { return _session; } }

        // ================================================================= seeds
        /// <summary>Create the default categories on first use (idempotent).</summary>
        public void SeedDefaults()
        {
            EnsureUncategorized();
            string flagged = _session.GetMeta("defaultCategoriesSeeded", "");
            if (flagged == "1") { return; }
            if (Count("categories") <= 1)
            {
                SaveCategory(NewCategory("课程作业", "#3B82F6", "日常课程作业与实验报告"));
                SaveCategory(NewCategory("考试复习", "#EF4444", "期中、期末与随堂测验复习"));
                SaveCategory(NewCategory("竞赛活动", "#8B5CF6", "学科竞赛、创新创业与社团活动"));
                SaveCategory(NewCategory("论文阅读", "#14B8A6", "文献阅读与读书笔记"));
                SaveCategory(NewCategory("个人事务", "#F59E0B", "生活、证件与个人安排"));
            }
            _session.WriteMeta("defaultCategoriesSeeded", "1");
        }

        private static Category NewCategory(string name, string color, string description)
        {
            Category c = new Category();
            c.Id = IsoTime.ShortId("cat");
            c.Name = name;
            c.Color = color;
            c.Description = description;
            c.CreatedAt = IsoTime.Now();
            c.UpdatedAt = c.CreatedAt;
            return c;
        }

        /// <summary>The Uncategorized bucket always exists and cannot be deleted.</summary>
        public Category EnsureUncategorized()
        {
            Category existing = FindCategory(Category.CreateUncategorized().Id);
            if (existing != null) { return existing; }
            Category c = Category.CreateUncategorized();
            SaveCategory(c);
            return c;
        }

        private long Count(string table)
        {
            return Db.ExecuteScalarLong("SELECT COUNT(*) FROM " + table + ";");
        }

        /// <summary>All unfinished todos, used by scoped queries that still need in memory shape.</summary>
        public List<Todo> LoadOpenTodos()
        {
            List<Todo> list = new List<Todo>();
            string sql = "SELECT " + Join(Todo.Columns) + " FROM todos WHERE status IN ('open','doing') ORDER BY due_at, sort_order;";
            using (SqliteStatement statement = Db.Prepare(sql))
            {
                while (statement.Step()) { list.Add(Todo.FromRow(statement)); }
            }
            AttachTags(list);
            return list;
        }

        /// <summary>Rows whose due date falls on or before the horizon, closest first.</summary>
        public List<Todo> LoadTodosDueBefore(DateTime horizon, bool includeCompleted)
        {
            List<Todo> list = new List<Todo>();
            string sql = "SELECT " + Join(Todo.Columns) + " FROM todos WHERE due_at <> '' AND due_at <= ?" +
                         (includeCompleted ? "" : " AND status IN ('open','doing')") +
                         " ORDER BY due_at;";
            using (SqliteStatement statement = Db.Prepare(sql))
            {
                statement.BindText(1, IsoTime.Format(horizon));
                while (statement.Step()) { list.Add(Todo.FromRow(statement)); }
            }
            AttachTags(list);
            return list;
        }

        public List<Exam> LoadUpcomingExams(DateTime horizon)
        {
            List<Exam> list = new List<Exam>();
            string sql = "SELECT " + Join(Exam.Columns) + " FROM exams WHERE start_at <= ? AND status <> 'cancelled' ORDER BY start_at;";
            using (SqliteStatement statement = Db.Prepare(sql))
            {
                statement.BindText(1, IsoTime.Format(horizon));
                while (statement.Step()) { list.Add(Exam.FromRow(statement)); }
            }
            return list;
        }

        /// <summary>Counters for the dashboard, computed in SQL.</summary>
        public sealed class Counters
        {
            public int Open;
            public int Overdue;
            public int DueToday;
            public int DueThisWeek;
            public int DoneTotal;
            public int DoneThisWeek;
            public int ExamCount;
            public int CompetitionCount;
        }

        public Counters LoadCounters(DateTime now)
        {
            Counters c = new Counters();
            string nowText = IsoTime.Format(now);
            string todayEnd = IsoTime.Format(now.Date.AddDays(1));
            string weekEnd = IsoTime.Format(now.Date.AddDays(7));
            string weekStart = IsoTime.Format(now.Date.AddDays(-7));

            c.Open = (int)Db.ExecuteScalarLong("SELECT COUNT(*) FROM todos WHERE status IN ('open','doing');");
            c.Overdue = (int)Db.ExecuteScalarLong("SELECT COUNT(*) FROM todos WHERE status IN ('open','doing') AND due_at <> '' AND due_at < ?;", nowText);
            c.DueToday = (int)Db.ExecuteScalarLong("SELECT COUNT(*) FROM todos WHERE status IN ('open','doing') AND due_at <> '' AND due_at < ?;", todayEnd);
            c.DueThisWeek = (int)Db.ExecuteScalarLong("SELECT COUNT(*) FROM todos WHERE status IN ('open','doing') AND due_at <> '' AND due_at < ?;", weekEnd);
            c.DoneTotal = (int)Db.ExecuteScalarLong("SELECT COUNT(*) FROM todos WHERE status = 'done';");
            c.DoneThisWeek = (int)Db.ExecuteScalarLong("SELECT COUNT(*) FROM todos WHERE status = 'done' AND completed_at >= ?;", weekStart);
            c.ExamCount = (int)Db.ExecuteScalarLong("SELECT COUNT(*) FROM exams WHERE status <> 'done' AND start_at >= ?;", nowText);
            c.CompetitionCount = (int)Db.ExecuteScalarLong("SELECT COUNT(*) FROM competitions WHERE stage NOT IN ('finished','archived');");
            return c;
        }

        // ================================================================= categories
        public List<Category> LoadCategories()
        {
            List<Category> list = new List<Category>();
            string sql = "SELECT " + Join(Category.Columns) + " FROM categories ORDER BY is_system, sort_order, name;";
            using (SqliteStatement statement = Db.Prepare(sql))
            {
                while (statement.Step()) { list.Add(Category.FromRow(statement)); }
            }
            return list;
        }

        public Category FindCategory(string id)
        {
            if (string.IsNullOrEmpty(id)) { return null; }
            string sql = "SELECT " + Join(Category.Columns) + " FROM categories WHERE id = ?;";
            using (SqliteStatement statement = Db.Prepare(sql))
            {
                statement.BindText(1, id);
                if (statement.Step()) { return Category.FromRow(statement); }
            }
            return null;
        }

        public Category FindCategoryByName(string name)
        {
            if (string.IsNullOrEmpty(name)) { return null; }
            string sql = "SELECT " + Join(Category.Columns) + " FROM categories WHERE name = ? LIMIT 1;";
            using (SqliteStatement statement = Db.Prepare(sql))
            {
                statement.BindText(1, name);
                if (statement.Step()) { return Category.FromRow(statement); }
            }
            return null;
        }

        public void SaveCategory(Category category)
        {
            if (category == null) { throw new ArgumentNullException("category"); }
            if (string.IsNullOrEmpty(category.Id)) { category.Id = IsoTime.ShortId("cat"); }
            if (string.IsNullOrEmpty(category.CreatedAt)) { category.CreatedAt = IsoTime.Now(); }
            category.UpdatedAt = IsoTime.Now();
            if (string.IsNullOrEmpty(category.Color)) { category.Color = "#3B82F6"; }

            string sql = "INSERT INTO categories (" + Join(Category.Columns) + ") VALUES (" + Marks(Category.Columns.Length) + ") " +
                         "ON CONFLICT(id) DO UPDATE SET name = excluded.name, color = excluded.color, " +
                         "description = excluded.description, icon = excluded.icon, sort_order = excluded.sort_order, " +
                         "updated_at = excluded.updated_at;";
            Db.ExecuteNonQuery(sql, category.ToRow());
        }

        /// <summary>Number of todos that point at a category (used by the delete prompt).</summary>
        public int CountTodosInCategory(string categoryId)
        {
            return (int)Db.ExecuteScalarLong("SELECT COUNT(*) FROM todos WHERE category_id = ?;", categoryId ?? "");
        }

        /// <summary>
        /// Delete a category.  Todos inside it are either moved to <paramref name="moveToCategoryId"/>
        /// (pass the Uncategorized id to park them) or deleted together with their attachments.
        /// </summary>
        public void DeleteCategory(string categoryId, bool deleteTodos, string moveToCategoryId)
        {
            if (string.IsNullOrEmpty(categoryId)) { return; }
            Category category = FindCategory(categoryId);
            if (category != null && category.IsUncategorized) { return; }   // never delete the bucket

            List<Todo> affected = LoadTodosInCategory(categoryId);
            Db.Transaction(delegate
            {
                if (deleteTodos)
                {
                    for (int i = 0; i < affected.Count; i++)
                    {
                        DeleteTodoCascade(affected[i].Id);
                    }
                }
                else
                {
                    string target = string.IsNullOrEmpty(moveToCategoryId) ? Category.CreateUncategorized().Id : moveToCategoryId;
                    Db.ExecuteNonQuery("UPDATE todos SET category_id = ?, updated_at = ? WHERE category_id = ?;",
                        target, IsoTime.Now(), categoryId);
                }
                Db.ExecuteNonQuery("DELETE FROM categories WHERE id = ?;", categoryId);
            });

            if (deleteTodos)
            {
                for (int i = 0; i < affected.Count; i++) { RemoveAttachmentFolder(AttachmentOwner.Todo, affected[i].Id); }
            }
            Log("category", "category", categoryId, (category != null ? category.Name : categoryId) + (deleteTodos ? " 分类及其待办已删除" : " 分类已删除，待办已迁移"));
        }

        public List<Todo> LoadTodosInCategory(string categoryId)
        {
            List<Todo> list = new List<Todo>();
            string sql = "SELECT " + Join(Todo.Columns) + " FROM todos WHERE category_id = ? ORDER BY sort_order, created_at;";
            using (SqliteStatement statement = Db.Prepare(sql))
            {
                statement.BindText(1, categoryId ?? "");
                while (statement.Step()) { list.Add(Todo.FromRow(statement)); }
            }
            return list;
        }

        // ================================================================= todos
        public List<Todo> LoadTodos()
        {
            List<Todo> list = new List<Todo>();
            string sql = "SELECT " + Join(Todo.Columns) + " FROM todos ORDER BY sort_order, created_at;";
            using (SqliteStatement statement = Db.Prepare(sql))
            {
                while (statement.Step()) { list.Add(Todo.FromRow(statement)); }
            }
            AttachTags(list);
            return list;
        }

        private void AttachTags(List<Todo> todos)
        {
            if (todos.Count == 0) { return; }
            Dictionary<string, Todo> index = new Dictionary<string, Todo>(StringComparer.Ordinal);
            for (int i = 0; i < todos.Count; i++) { index[todos[i].Id] = todos[i]; }

            using (SqliteStatement statement = Db.Prepare("SELECT todo_id, competition_id FROM todo_competitions;"))
            {
                while (statement.Step())
                {
                    Todo todo;
                    if (index.TryGetValue(statement.GetString(0) ?? "", out todo))
                    {
                        todo.CompetitionIds.Add(statement.GetString(1));
                    }
                }
            }
            using (SqliteStatement statement = Db.Prepare("SELECT todo_id, milestone_id FROM todo_milestones;"))
            {
                while (statement.Step())
                {
                    Todo todo;
                    if (index.TryGetValue(statement.GetString(0) ?? "", out todo))
                    {
                        todo.MilestoneIds.Add(statement.GetString(1));
                    }
                }
            }
        }

        public Todo FindTodo(string id)
        {
            if (string.IsNullOrEmpty(id)) { return null; }
            string sql = "SELECT " + Join(Todo.Columns) + " FROM todos WHERE id = ?;";
            using (SqliteStatement statement = Db.Prepare(sql))
            {
                statement.BindText(1, id);
                if (!statement.Step()) { return null; }
                Todo todo = Todo.FromRow(statement);
                List<Todo> single = new List<Todo>();
                single.Add(todo);
                AttachTags(single);
                todo.Attachments = LoadAttachments(AttachmentOwner.Todo, todo.Id);
                return todo;
            }
        }

        public string SaveTodo(Todo todo, string activityMessage)
        {
            if (todo == null) { throw new ArgumentNullException("todo"); }
            if (string.IsNullOrEmpty(todo.Id)) { todo.Id = IsoTime.ShortId("todo"); }
            if (string.IsNullOrEmpty(todo.CreatedAt)) { todo.CreatedAt = IsoTime.Now(); }
            todo.UpdatedAt = IsoTime.Now();
            if (string.IsNullOrEmpty(todo.Status)) { todo.Status = TodoStatus.Open; }
            if (string.IsNullOrEmpty(todo.CategoryId)) { todo.CategoryId = Category.CreateUncategorized().Id; }

            bool isNew = Db.ExecuteScalarLong("SELECT COUNT(*) FROM todos WHERE id = ?;", todo.Id) == 0;

            Db.Transaction(delegate
            {
                string sql = "INSERT INTO todos (" + Join(Todo.Columns) + ") VALUES (" + Marks(Todo.Columns.Length) + ") " +
                             "ON CONFLICT(id) DO UPDATE SET title = excluded.title, category_id = excluded.category_id, " +
                             "status = excluded.status, priority = excluded.priority, progress = excluded.progress, " +
                             "due_at = excluded.due_at, start_at = excluded.start_at, completed_at = excluded.completed_at, " +
                             "updated_at = excluded.updated_at, note_markdown = excluded.note_markdown, " +
                             "note_updated_at = excluded.note_updated_at, lead_minutes = excluded.lead_minutes, " +
                             "repeat_rule = excluded.repeat_rule, sort_order = excluded.sort_order, " +
                             "color_override = excluded.color_override, tags = excluded.tags, steps = excluded.steps;";
                Db.ExecuteNonQuery(sql, todo.ToRow());

                Db.ExecuteNonQuery("DELETE FROM todo_competitions WHERE todo_id = ?;", todo.Id);
                for (int i = 0; i < todo.CompetitionIds.Count; i++)
                {
                    string refId = todo.CompetitionIds[i];
                    if (string.IsNullOrEmpty(refId)) { continue; }
                    Db.ExecuteNonQuery("INSERT OR IGNORE INTO todo_competitions(todo_id, competition_id) VALUES(?, ?);",
                        todo.Id, refId);
                }

                Db.ExecuteNonQuery("DELETE FROM todo_milestones WHERE todo_id = ?;", todo.Id);
                for (int i = 0; i < todo.MilestoneIds.Count; i++)
                {
                    if (string.IsNullOrEmpty(todo.MilestoneIds[i])) { continue; }
                    Db.ExecuteNonQuery("INSERT OR IGNORE INTO todo_milestones(todo_id, milestone_id) VALUES(?, ?);",
                        todo.Id, todo.MilestoneIds[i]);
                }
            });

            _session.MirrorNote(AttachmentOwner.Todo, todo.Id, todo.NoteMarkdown);
            if (!string.IsNullOrEmpty(activityMessage))
            {
                Log(isNew ? "todo.create" : "todo.update", "todo", todo.Id, activityMessage);
            }
            return todo.Id;
        }

        /// <summary>Fast toggle used by the widget check box.</summary>
        public void SetTodoStatus(string todoId, string status)
        {
            Todo todo = FindTodo(todoId);
            if (todo == null) { return; }
            todo.Status = status;
            if (status == TodoStatus.Done)
            {
                todo.CompletedAt = IsoTime.Now();
                if (todo.Progress < 1) { todo.Progress = 1; }
            }
            else
            {
                todo.CompletedAt = "";
                if (todo.Progress >= 1) { todo.Progress = 0.99; }
            }
            todo.UpdatedAt = IsoTime.Now();
            Db.ExecuteNonQuery("UPDATE todos SET status = ?, completed_at = ?, progress = ?, updated_at = ? WHERE id = ?;",
                todo.Status, todo.CompletedAt, todo.Progress, todo.UpdatedAt, todo.Id);
            Log(status == TodoStatus.Done ? "todo.done" : "todo.reopen", "todo", todo.Id,
                (status == TodoStatus.Done ? "完成：" : "重新打开：") + todo.Title);
        }

        public void DeleteTodoCascade(string todoId)
        {
            Db.ExecuteNonQuery("DELETE FROM todos WHERE id = ?;", todoId);
        }

        public void DeleteTodo(string todoId)
        {
            Todo todo = FindTodo(todoId);
            Db.Transaction(delegate { DeleteTodoCascade(todoId); });
            RemoveAttachmentFolder(AttachmentOwner.Todo, todoId);
            if (todo != null) { Log("todo.delete", "todo", todoId, "删除待办：" + todo.Title); }
        }

        public void SetTodoCategory(string todoId, string categoryId)
        {
            Db.ExecuteNonQuery("UPDATE todos SET category_id = ?, updated_at = ? WHERE id = ?;",
                categoryId ?? "", IsoTime.Now(), todoId);
        }

        public void SetTodoPriority(string todoId, int priority)
        {
            Db.ExecuteNonQuery("UPDATE todos SET priority = ?, updated_at = ? WHERE id = ?;",
                (long)priority, IsoTime.Now(), todoId);
        }

        public void SetTodoProgress(string todoId, double progress)
        {
            if (progress < 0) { progress = 0; }
            if (progress > 1) { progress = 1; }
            Db.ExecuteNonQuery("UPDATE todos SET progress = ?, updated_at = ? WHERE id = ?;",
                progress, IsoTime.Now(), todoId);
        }

        public void SetTodoOrder(string todoId, int order)
        {
            Db.ExecuteNonQuery("UPDATE todos SET sort_order = ?, updated_at = ? WHERE id = ?;",
                (long)order, IsoTime.Now(), todoId);
        }

        /// <summary>Persist a reordered list (drag and drop in the task list).</summary>
        public void SaveTodoOrder(List<string> orderedIds)
        {
            if (orderedIds == null) { return; }
            Db.Transaction(delegate
            {
                for (int i = 0; i < orderedIds.Count; i++)
                {
                    Db.ExecuteNonQuery("UPDATE todos SET sort_order = ? WHERE id = ?;", (long)i, orderedIds[i]);
                }
            });
        }

        // ================================================================= attachments
        public List<Attachment> LoadAttachments(string ownerKind, string ownerId)
        {
            List<Attachment> list = new List<Attachment>();
            string sql = "SELECT " + Join(Attachment.Columns) + " FROM attachments WHERE owner_kind = ? AND owner_id = ? " +
                         "ORDER BY sort_order, created_at;";
            using (SqliteStatement statement = Db.Prepare(sql))
            {
                statement.BindText(1, ownerKind);
                statement.BindText(2, ownerId);
                while (statement.Step()) { list.Add(Attachment.FromRow(statement)); }
            }
            return list;
        }

        /// <summary>All attachments grouped by owner, for batch loads.</summary>
        public Dictionary<string, List<Attachment>> LoadAllAttachments()
        {
            Dictionary<string, List<Attachment>> map = new Dictionary<string, List<Attachment>>(StringComparer.Ordinal);
            string sql = "SELECT " + Join(Attachment.Columns) + " FROM attachments ORDER BY sort_order, created_at;";
            using (SqliteStatement statement = Db.Prepare(sql))
            {
                while (statement.Step())
                {
                    Attachment a = Attachment.FromRow(statement);
                    string key = a.OwnerKind + "|" + a.OwnerId;
                    List<Attachment> bucket;
                    if (!map.TryGetValue(key, out bucket))
                    {
                        bucket = new List<Attachment>();
                        map[key] = bucket;
                    }
                    bucket.Add(a);
                }
            }
            return map;
        }

        /// <summary>
        /// Copy a file (or a whole folder) into the container and register it.  Files always land
        /// in attachments/&lt;ownerKind&gt;/&lt;ownerId&gt;/ so the container stays self contained and the
        /// database only ever stores a relative path.
        /// </summary>
        public Attachment ImportAttachment(string ownerKind, string ownerId, string sourcePath, string category, string preferredName)
        {
            if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
            {
                throw new FileNotFoundException("找不到要导入的文件：", sourcePath);
            }

            bool isFolder = Directory.Exists(sourcePath);
            string targetFolder = _session.AttachmentsFolderFor(ownerKind, ownerId);
            Directory.CreateDirectory(targetFolder);

            string originalName = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string desiredName = string.IsNullOrEmpty(preferredName) ? originalName : preferredName;
            string safeName = AppPaths.SafeFileName(desiredName, isFolder ? "folder" : "file");
            string destination = MakeUnique(Path.Combine(targetFolder, safeName), isFolder);

            if (isFolder)
            {
                CopyDirectory(sourcePath, destination);
            }
            else
            {
                File.Copy(sourcePath, destination, true);
            }

            Attachment attachment = new Attachment();
            attachment.Id = IsoTime.ShortId("att");
            attachment.OwnerKind = ownerKind;
            attachment.OwnerId = ownerId;
            attachment.FileName = Path.GetFileName(destination);
            attachment.RelativePath = _session.ToRelative(destination);
            attachment.SizeBytes = isFolder ? FolderSize(destination) : new FileInfo(destination).Length;
            attachment.Mime = isFolder ? "" : GuessMime(destination);
            attachment.Category = category ?? "";
            attachment.IsFolder = isFolder;
            attachment.CreatedAt = IsoTime.Now();
            attachment.SortOrder = (int)Db.ExecuteScalarLong(
                "SELECT COALESCE(MAX(sort_order), -1) + 1 FROM attachments WHERE owner_kind = ? AND owner_id = ?;",
                ownerKind, ownerId);

            Db.ExecuteNonQuery(
                "INSERT INTO attachments (" + Join(Attachment.Columns) + ") VALUES (" + Marks(Attachment.Columns.Length) + ");",
                attachment.ToRow());
            Log("attachment.add", ownerKind, ownerId, "导入作业文件：" + attachment.FileName);
            return attachment;
        }

        private static string MakeUnique(string path, bool isFolder)
        {
            if (isFolder) { if (!Directory.Exists(path)) { return path; } }
            else { if (!File.Exists(path)) { return path; } }

            string directory = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            for (int i = 2; i < 1000; i++)
            {
                string candidate = Path.Combine(directory, name + " (" + i + ")" + extension);
                if (isFolder) { if (!Directory.Exists(candidate)) { return candidate; } }
                else { if (!File.Exists(candidate)) { return candidate; } }
            }
            return Path.Combine(directory, name + "-" + IsoTime.ShortId("").Substring(0, 5) + extension);
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            string[] files = Directory.GetFiles(source);
            for (int i = 0; i < files.Length; i++)
            {
                File.Copy(files[i], Path.Combine(destination, Path.GetFileName(files[i])), true);
            }
            string[] folders = Directory.GetDirectories(source);
            for (int i = 0; i < folders.Length; i++)
            {
                CopyDirectory(folders[i], Path.Combine(destination, Path.GetFileName(folders[i])));
            }
        }

        private static long FolderSize(string folder)
        {
            long total = 0;
            try
            {
                string[] files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    try { total += new FileInfo(files[i]).Length; } catch { }
                }
            }
            catch
            {
            }
            return total;
        }

        private static string GuessMime(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            switch (extension)
            {
                case ".pdf": return "application/pdf";
                case ".doc": return "application/msword";
                case ".docx": return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                case ".xls": return "application/vnd.ms-excel";
                case ".xlsx": return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                case ".ppt": return "application/vnd.ms-powerpoint";
                case ".pptx": return "application/vnd.openxmlformats-officedocument.presentationml.presentation";
                case ".txt": return "text/plain";
                case ".md": return "text/markdown";
                case ".png": return "image/png";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".gif": return "image/gif";
                case ".webp": return "image/webp";
                case ".zip": return "application/zip";
                case ".rar": return "application/vnd.rar";
                case ".7z": return "application/x-7z-compressed";
                case ".py": return "text/x-python";
                case ".cs": return "text/x-csharp";
                case ".cpp":
                case ".cc": return "text/x-c++";
                case ".c": return "text/x-c";
                case ".java": return "text/x-java";
                case ".json": return "application/json";
                case ".csv": return "text/csv";
                default: return "application/octet-stream";
            }
        }

        /// <summary>Rename an attachment inside the container; the extension is preserved.</summary>
        public Attachment RenameAttachment(string attachmentId, string newName, bool keepExtension)
        {
            Attachment attachment = FindAttachment(attachmentId);
            if (attachment == null) { throw new InvalidOperationException("附件不存在。"); }

            string current = _session.ResolveRelative(attachment.RelativePath);
            string directory = Path.GetDirectoryName(current);
            string extension = keepExtension ? Path.GetExtension(attachment.FileName) : "";
            string safe = AppPaths.SafeFileName(newName, "attachment");
            if (keepExtension && !safe.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                safe = Path.GetFileNameWithoutExtension(safe) + extension;
            }
            string target = Path.Combine(directory, safe);
            if (string.Equals(target, current, StringComparison.OrdinalIgnoreCase))
            {
                attachment.FileName = Path.GetFileName(target);
                SaveAttachment(attachment);
                return attachment;
            }
            target = MakeUnique(target, attachment.IsFolder);

            if (attachment.IsFolder) { Directory.Move(current, target); }
            else { File.Move(current, target); }

            attachment.FileName = Path.GetFileName(target);
            attachment.RelativePath = _session.ToRelative(target);
            SaveAttachment(attachment);
            Log("attachment.rename", attachment.OwnerKind, attachment.OwnerId, "重命名作业文件：" + attachment.FileName);
            return attachment;
        }

        public void SaveAttachment(Attachment attachment)
        {
            Db.ExecuteNonQuery(
                "INSERT INTO attachments (" + Join(Attachment.Columns) + ") VALUES (" + Marks(Attachment.Columns.Length) + ") " +
                "ON CONFLICT(id) DO UPDATE SET file_name = excluded.file_name, relative_path = excluded.relative_path, " +
                "size_bytes = excluded.size_bytes, mime = excluded.mime, category = excluded.category, " +
                "note = excluded.note, is_folder = excluded.is_folder, sort_order = excluded.sort_order;",
                attachment.ToRow());
        }

        public Attachment FindAttachment(string attachmentId)
        {
            if (string.IsNullOrEmpty(attachmentId)) { return null; }
            string sql = "SELECT " + Join(Attachment.Columns) + " FROM attachments WHERE id = ?;";
            using (SqliteStatement statement = Db.Prepare(sql))
            {
                statement.BindText(1, attachmentId);
                if (statement.Step()) { return Attachment.FromRow(statement); }
            }
            return null;
        }

        public void DeleteAttachment(string attachmentId, bool deleteFile)
        {
            Attachment attachment = FindAttachment(attachmentId);
            if (attachment == null) { return; }
            if (deleteFile)
            {
                try
                {
                    string path = _session.ResolveRelative(attachment.RelativePath);
                    if (attachment.IsFolder) { if (Directory.Exists(path)) { Directory.Delete(path, true); } }
                    else { if (File.Exists(path)) { File.Delete(path); } }
                }
                catch
                {
                }
            }
            Db.ExecuteNonQuery("DELETE FROM attachments WHERE id = ?;", attachmentId);
            Log("attachment.delete", attachment.OwnerKind, attachment.OwnerId, "移除作业文件：" + attachment.FileName);
        }

        /// <summary>Remove every attachment row and the folder for a deleted owner.</summary>
        public void RemoveAttachmentFolder(string ownerKind, string ownerId)
        {
            Db.ExecuteNonQuery("DELETE FROM attachments WHERE owner_kind = ? AND owner_id = ?;", ownerKind, ownerId ?? "");
            try
            {
                string folder = _session.AttachmentsFolderFor(ownerKind, ownerId);
                if (Directory.Exists(folder)) { Directory.Delete(folder, true); }
            }
            catch
            {
            }
        }

        public void OpenAttachment(Attachment attachment)
        {
            if (attachment == null) { return; }
            string path = _session.ResolveRelative(attachment.RelativePath);
            if (!File.Exists(path) && !Directory.Exists(path)) { throw new FileNotFoundException("文件已不存在：", path); }
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        public void RevealAttachment(Attachment attachment)
        {
            if (attachment == null) { return; }
            string path = _session.ResolveRelative(attachment.RelativePath);
            if (!File.Exists(path) && !Directory.Exists(path)) { throw new FileNotFoundException("文件已不存在：", path); }
            Process.Start("explorer.exe", "/select,\"" + path + "\"");
        }

        // ================================================================= exams
        public List<Exam> LoadExams()
        {
            List<Exam> list = new List<Exam>();
            string sql = "SELECT " + Join(Exam.Columns) + " FROM exams ORDER BY start_at, sort_order;";
            using (SqliteStatement statement = Db.Prepare(sql))
            {
                while (statement.Step()) { list.Add(Exam.FromRow(statement)); }
            }
            return list;
        }

        public Exam FindExam(string id)
        {
            if (string.IsNullOrEmpty(id)) { return null; }
            using (SqliteStatement statement = Db.Prepare("SELECT " + Join(Exam.Columns) + " FROM exams WHERE id = ?;"))
            {
                statement.BindText(1, id);
                if (!statement.Step()) { return null; }
                Exam exam = Exam.FromRow(statement);
                exam.Attachments = LoadAttachments(AttachmentOwner.Exam, exam.Id);
                return exam;
            }
        }

        public string SaveExam(Exam exam)
        {
            if (exam == null) { throw new ArgumentNullException("exam"); }
            if (string.IsNullOrEmpty(exam.Id)) { exam.Id = IsoTime.ShortId("exam"); }
            if (string.IsNullOrEmpty(exam.CreatedAt)) { exam.CreatedAt = IsoTime.Now(); }
            exam.UpdatedAt = IsoTime.Now();
            bool isNew = Db.ExecuteScalarLong("SELECT COUNT(*) FROM exams WHERE id = ?;", exam.Id) == 0;

            string sql = "INSERT INTO exams (" + Join(Exam.Columns) + ") VALUES (" + Marks(Exam.Columns.Length) + ") " +
                         "ON CONFLICT(id) DO UPDATE SET title = excluded.title, course = excluded.course, " +
                         "category_id = excluded.category_id, start_at = excluded.start_at, end_at = excluded.end_at, " +
                         "location = excluded.location, seat = excluded.seat, status = excluded.status, " +
                         "note_markdown = excluded.note_markdown, remind_minutes = excluded.remind_minutes, " +
                         "pin_countdown = excluded.pin_countdown, score = excluded.score, updated_at = excluded.updated_at, " +
                         "sort_order = excluded.sort_order;";
            Db.ExecuteNonQuery(sql, exam.ToRow());
            _session.MirrorNote(AttachmentOwner.Exam, exam.Id, exam.NoteMarkdown);
            Log(isNew ? "exam.create" : "exam.update", "exam", exam.Id,
                (isNew ? "新增考试：" : "更新考试：") + exam.Title);
            return exam.Id;
        }

        public void DeleteExam(string examId)
        {
            Exam exam = FindExam(examId);
            Db.ExecuteNonQuery("DELETE FROM exams WHERE id = ?;", examId);
            RemoveAttachmentFolder(AttachmentOwner.Exam, examId);
            if (exam != null) { Log("exam.delete", "exam", examId, "删除考试：" + exam.Title); }
        }

        // ================================================================= competitions
        public List<Competition> LoadCompetitions(bool withChildren)
        {
            List<Competition> list = new List<Competition>();
            string sql = "SELECT " + Join(Competition.Columns) + " FROM competitions ORDER BY starred DESC, sort_order, name;";
            using (SqliteStatement statement = Db.Prepare(sql))
            {
                while (statement.Step()) { list.Add(Competition.FromRow(statement)); }
            }
            if (withChildren) { AttachCompetitionChildren(list); }
            return list;
        }

        private void AttachCompetitionChildren(List<Competition> competitions)
        {
            if (competitions.Count == 0) { return; }
            Dictionary<string, Competition> index = new Dictionary<string, Competition>(StringComparer.Ordinal);
            for (int i = 0; i < competitions.Count; i++) { index[competitions[i].Id] = competitions[i]; }

            using (SqliteStatement statement = Db.Prepare("SELECT " + Join(Milestone.Columns) + " FROM milestones ORDER BY sort_order, due_at;"))
            {
                while (statement.Step())
                {
                    Milestone m = Milestone.FromRow(statement);
                    Competition owner;
                    if (index.TryGetValue(m.CompetitionId ?? "", out owner)) { owner.Milestones.Add(m); }
                }
            }
            using (SqliteStatement statement = Db.Prepare("SELECT " + Join(CompetitionEvent.Columns) + " FROM competition_events ORDER BY sort_order, start_at;"))
            {
                while (statement.Step())
                {
                    CompetitionEvent e = CompetitionEvent.FromRow(statement);
                    Competition owner;
                    if (index.TryGetValue(e.CompetitionId ?? "", out owner)) { owner.Events.Add(e); }
                }
            }
            Dictionary<string, List<Attachment>> attachments = LoadAllAttachments();
            foreach (KeyValuePair<string, Competition> pair in index)
            {
                List<Attachment> bucket;
                if (attachments.TryGetValue(AttachmentOwner.Competition + "|" + pair.Key, out bucket))
                {
                    pair.Value.Attachments = bucket;
                }
            }
        }

        public Competition FindCompetition(string id)
        {
            if (string.IsNullOrEmpty(id)) { return null; }
            using (SqliteStatement statement = Db.Prepare("SELECT " + Join(Competition.Columns) + " FROM competitions WHERE id = ?;"))
            {
                statement.BindText(1, id);
                if (!statement.Step()) { return null; }
                Competition c = Competition.FromRow(statement);
                List<Competition> single = new List<Competition>();
                single.Add(c);
                AttachCompetitionChildren(single);
                c.LinkedTodos = LoadTodosForCompetition(c);
                return c;
            }
        }

        public Competition FindCompetitionByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) { return null; }
            using (SqliteStatement statement = Db.Prepare("SELECT " + Join(Competition.Columns) + " FROM competitions WHERE tag = ? OR name = ? LIMIT 1;"))
            {
                statement.BindText(1, tag);
                statement.BindText(2, tag);
                if (!statement.Step()) { return null; }
                return Competition.FromRow(statement);
            }
        }

        /// <summary>Todos referencing a competition by id (link table) or by tag.</summary>
        public List<Todo> LoadTodosForCompetition(Competition competition)
        {
            return LoadTodosForCompetition(competition, null);
        }

        /// <summary>
        /// Same as above but reuses an already loaded competition lookup, which keeps the
        /// dashboard and competition screens free of per-row queries.
        /// </summary>
        public List<Todo> LoadTodosForCompetition(Competition competition, Dictionary<string, Competition> lookup)
        {
            List<Todo> result = new List<Todo>();
            if (competition == null) { return result; }
            Dictionary<string, List<Attachment>> attachments = LoadAllAttachments();
            List<Todo> all = LoadTodos();
            for (int i = 0; i < all.Count; i++)
            {
                Todo todo = all[i];
                bool linked = false;
                for (int j = 0; j < todo.CompetitionIds.Count; j++)
                {
                    string reference = todo.CompetitionIds[j];
                    if (reference == competition.Id) { linked = true; break; }
                    if (lookup != null)
                    {
                        Competition other;
                        if (lookup.TryGetValue(reference, out other) && other.Id == competition.Id) { linked = true; break; }
                    }
                    else
                    {
                        Competition other = FindCompetition(reference);
                        if (other != null && other.Id == competition.Id) { linked = true; break; }
                    }
                }
                if (!linked && !string.IsNullOrEmpty(competition.Tag))
                {
                    for (int j = 0; j < todo.Tags.Count; j++)
                    {
                        if (string.Equals(todo.Tags[j], competition.Tag, StringComparison.OrdinalIgnoreCase)) { linked = true; break; }
                    }
                }
                if (linked)
                {
                    List<Attachment> bucket;
                    if (attachments.TryGetValue(AttachmentOwner.Todo + "|" + todo.Id, out bucket)) { todo.Attachments = bucket; }
                    result.Add(todo);
                }
            }
            return result;
        }

        public string SaveCompetition(Competition competition)
        {
            if (competition == null) { throw new ArgumentNullException("competition"); }
            if (string.IsNullOrEmpty(competition.Id)) { competition.Id = IsoTime.ShortId("comp"); }
            if (string.IsNullOrEmpty(competition.CreatedAt)) { competition.CreatedAt = IsoTime.Now(); }
            competition.UpdatedAt = IsoTime.Now();
            if (string.IsNullOrEmpty(competition.Color)) { competition.Color = "#8B5CF6"; }
            competition.Tag = BuildUniqueTag(competition.Id, competition.Tag, competition.Name);
            bool isNew = Db.ExecuteScalarLong("SELECT COUNT(*) FROM competitions WHERE id = ?;", competition.Id) == 0;

            string sql = "INSERT INTO competitions (" + Join(Competition.Columns) + ") VALUES (" + Marks(Competition.Columns.Length) + ") " +
                         "ON CONFLICT(id) DO UPDATE SET name = excluded.name, tag = excluded.tag, color = excluded.color, " +
                         "organizer = excluded.organizer, stage = excluded.stage, level = excluded.level, " +
                         "description = excluded.description, note_markdown = excluded.note_markdown, start_at = excluded.start_at, " +
                         "end_at = excluded.end_at, progress = excluded.progress, starred = excluded.starred, team = excluded.team, " +
                         "links = excluded.links, attachment_categories = excluded.attachment_categories, " +
                         "updated_at = excluded.updated_at, sort_order = excluded.sort_order;";
            Db.ExecuteNonQuery(sql, competition.ToRow());
            _session.MirrorNote(AttachmentOwner.Competition, competition.Id, competition.NoteMarkdown);
            Log(isNew ? "competition.create" : "competition.update", "competition", competition.Id,
                (isNew ? "创建竞赛：" : "更新竞赛：") + competition.Name);
            return competition.Id;
        }

        /// <summary>Tags must be unique because todos reference a competition through its tag.</summary>
        private string BuildUniqueTag(string competitionId, string tag, string name)
        {
            string candidate = string.IsNullOrEmpty(tag) ? name : tag;
            candidate = (candidate ?? "").Trim();
            if (candidate.Length == 0) { candidate = "competition"; }
            string baseTag = candidate;
            int suffix = 2;
            while (true)
            {
                string existingId = Db.ExecuteScalarString("SELECT id FROM competitions WHERE tag = ?;", candidate);
                if (string.IsNullOrEmpty(existingId) || existingId == competitionId) { return candidate; }
                candidate = baseTag + "-" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
                suffix++;
            }
        }

        public void DeleteCompetition(string competitionId, bool deleteLinkedTodos)
        {
            Competition competition = FindCompetition(competitionId);
            List<Todo> linked = competition != null ? LoadTodosForCompetition(competition) : new List<Todo>();

            Db.Transaction(delegate
            {
                if (deleteLinkedTodos)
                {
                    for (int i = 0; i < linked.Count; i++) { DeleteTodoCascade(linked[i].Id); }
                }
                else
                {
                    Db.ExecuteNonQuery("DELETE FROM todo_competitions WHERE competition_id = ?;", competitionId);
                }
                Db.ExecuteNonQuery("DELETE FROM competitions WHERE id = ?;", competitionId);
            });

            if (deleteLinkedTodos)
            {
                for (int i = 0; i < linked.Count; i++) { RemoveAttachmentFolder(AttachmentOwner.Todo, linked[i].Id); }
            }
            RemoveAttachmentFolder(AttachmentOwner.Competition, competitionId);
            if (competition != null) { Log("competition.delete", "competition", competitionId, "删除竞赛：" + competition.Name); }
        }

        // ================================================================= milestones
        public List<Milestone> LoadMilestones(string competitionId)
        {
            List<Milestone> list = new List<Milestone>();
            string sql = "SELECT " + Join(Milestone.Columns) + " FROM milestones WHERE competition_id = ? ORDER BY sort_order, due_at;";
            using (SqliteStatement statement = Db.Prepare(sql))
            {
                statement.BindText(1, competitionId);
                while (statement.Step()) { list.Add(Milestone.FromRow(statement)); }
            }
            return list;
        }

        public string SaveMilestone(Milestone milestone)
        {
            if (milestone == null) { throw new ArgumentNullException("milestone"); }
            if (string.IsNullOrEmpty(milestone.Id)) { milestone.Id = IsoTime.ShortId("ms"); }
            if (string.IsNullOrEmpty(milestone.CreatedAt)) { milestone.CreatedAt = IsoTime.Now(); }
            milestone.UpdatedAt = IsoTime.Now();
            if (milestone.Weight <= 0) { milestone.Weight = 1; }
            bool isNew = Db.ExecuteScalarLong("SELECT COUNT(*) FROM milestones WHERE id = ?;", milestone.Id) == 0;

            string sql = "INSERT INTO milestones (" + Join(Milestone.Columns) + ") VALUES (" + Marks(Milestone.Columns.Length) + ") " +
                         "ON CONFLICT(id) DO UPDATE SET competition_id = excluded.competition_id, title = excluded.title, " +
                         "detail = excluded.detail, note_markdown = excluded.note_markdown, due_at = excluded.due_at, " +
                         "completed_at = excluded.completed_at, is_done = excluded.is_done, weight = excluded.weight, " +
                         "updated_at = excluded.updated_at, sort_order = excluded.sort_order;";
            Db.ExecuteNonQuery(sql, milestone.ToRow());
            _session.MirrorNote(AttachmentOwner.Milestone, milestone.Id, milestone.NoteMarkdown);
            Log(isNew ? "milestone.create" : "milestone.update", "milestone", milestone.Id, "里程碑：" + milestone.Title);
            return milestone.Id;
        }

        public void SetMilestoneDone(string milestoneId, bool done)
        {
            Db.ExecuteNonQuery("UPDATE milestones SET is_done = ?, completed_at = ?, updated_at = ? WHERE id = ?;",
                done ? 1L : 0L, done ? IsoTime.Now() : "", IsoTime.Now(), milestoneId);
        }

        public void DeleteMilestone(string milestoneId)
        {
            Db.ExecuteNonQuery("DELETE FROM milestones WHERE id = ?;", milestoneId);
            Db.ExecuteNonQuery("DELETE FROM attachments WHERE owner_kind = ? AND owner_id = ?;", AttachmentOwner.Milestone, milestoneId);
        }

        // ================================================================= competition events
        public List<CompetitionEvent> LoadEvents(string competitionId)
        {
            List<CompetitionEvent> list = new List<CompetitionEvent>();
            string sql = "SELECT " + Join(CompetitionEvent.Columns) + " FROM competition_events WHERE competition_id = ? ORDER BY sort_order, start_at;";
            using (SqliteStatement statement = Db.Prepare(sql))
            {
                statement.BindText(1, competitionId);
                while (statement.Step()) { list.Add(CompetitionEvent.FromRow(statement)); }
            }
            return list;
        }

        public string SaveEvent(CompetitionEvent entry)
        {
            if (entry == null) { throw new ArgumentNullException("entry"); }
            if (string.IsNullOrEmpty(entry.Id)) { entry.Id = IsoTime.ShortId("evt"); }
            if (string.IsNullOrEmpty(entry.CreatedAt)) { entry.CreatedAt = IsoTime.Now(); }
            entry.UpdatedAt = IsoTime.Now();
            bool isNew = Db.ExecuteScalarLong("SELECT COUNT(*) FROM competition_events WHERE id = ?;", entry.Id) == 0;

            string sql = "INSERT INTO competition_events (" + Join(CompetitionEvent.Columns) + ") VALUES (" + Marks(CompetitionEvent.Columns.Length) + ") " +
                         "ON CONFLICT(id) DO UPDATE SET competition_id = excluded.competition_id, title = excluded.title, " +
                         "kind = excluded.kind, start_at = excluded.start_at, end_at = excluded.end_at, " +
                         "location = excluded.location, detail = excluded.detail, note_markdown = excluded.note_markdown, " +
                         "all_day = excluded.all_day, updated_at = excluded.updated_at, sort_order = excluded.sort_order;";
            Db.ExecuteNonQuery(sql, entry.ToRow());
            _session.MirrorNote(AttachmentOwner.Event, entry.Id, entry.NoteMarkdown);
            Log(isNew ? "event.create" : "event.update", "event", entry.Id, "重要活动：" + entry.Title);
            return entry.Id;
        }

        public void DeleteEvent(string eventId)
        {
            Db.ExecuteNonQuery("DELETE FROM competition_events WHERE id = ?;", eventId);
            Db.ExecuteNonQuery("DELETE FROM attachments WHERE owner_kind = ? AND owner_id = ?;", AttachmentOwner.Event, eventId);
        }

        // ================================================================= activity
        public void Log(string kind, string refKind, string refId, string message)
        {
            ActivityEntry entry = new ActivityEntry();
            entry.Id = IsoTime.ShortId("act");
            entry.At = IsoTime.Now();
            entry.Kind = kind ?? "info";
            entry.RefKind = refKind ?? "";
            entry.RefId = refId ?? "";
            entry.Message = message ?? "";
            Db.ExecuteNonQuery("INSERT INTO activity_log (" + Join(ActivityEntry.Columns) + ") VALUES (" + Marks(ActivityEntry.Columns.Length) + ");",
                entry.ToRow());
            PruneActivity();
        }

        private void PruneActivity()
        {
            try
            {
                Db.ExecuteNonQuery("DELETE FROM activity_log WHERE id NOT IN (" +
                                   "SELECT id FROM activity_log ORDER BY at DESC LIMIT 800);");
            }
            catch
            {
            }
        }

        public List<ActivityEntry> LoadActivity(int limit)
        {
            List<ActivityEntry> list = new List<ActivityEntry>();
            string sql = "SELECT " + Join(ActivityEntry.Columns) + " FROM activity_log ORDER BY at DESC LIMIT ?;";
            using (SqliteStatement statement = Db.Prepare(sql))
            {
                statement.BindInt(1, limit);
                while (statement.Step()) { list.Add(ActivityEntry.FromRow(statement)); }
            }
            return list;
        }

        // ================================================================= helpers
        internal static string Join(string[] columns)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < columns.Length; i++)
            {
                if (i > 0) { sb.Append(", "); }
                sb.Append(columns[i]);
            }
            return sb.ToString();
        }

        internal static string Marks(int count)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < count; i++)
            {
                if (i > 0) { sb.Append(", "); }
                sb.Append('?');
            }
            return sb.ToString();
        }
    }
}
