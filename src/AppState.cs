using System;
using System.Collections.Generic;
using Learnest.Core;
using Learnest.Data;

namespace Learnest
{
    /// <summary>
    /// Process wide state: the machine scoped app config, the currently open container and an
    /// in-memory snapshot of its data.  Every screen reads from the snapshot and mutates through
    /// the repository, then calls Commit() so the whole UI refreshes consistently.
    ///
    /// A container can be swapped at runtime (open / import / create), which raises
    /// ContainerChanged so open windows can reload their lists.
    /// </summary>
    public static class AppState
    {
        private static readonly object Sync = new object();
        private static AppConfig _config;
        private static ContainerSession _session;
        private static Snapshot _snapshot;

        public static event EventHandler ContainerChanged;

        // ---------------------------------------------------------------- config
        public static AppConfig Config
        {
            get
            {
                lock (Sync)
                {
                    if (_config == null) { _config = AppConfig.Load(); }
                    return _config;
                }
            }
        }

        public static void SaveConfig()
        {
            lock (Sync)
            {
                if (_config != null) { _config.Save(); }
            }
        }

        // ---------------------------------------------------------------- container
        public static bool HasContainer { get { return _session != null; } }

        public static ContainerSession Session
        {
            get
            {
                if (_session == null)
                {
                    throw new InvalidOperationException(
                        "No container is open. Learnest must resolve a container before any data is accessed.");
                }
                return _session;
            }
        }

        public static ContainerSession SessionOrNull { get { return _session; } }

        public static Repository Repo { get { return Session.Repository; } }

        public static AppSettings Settings
        {
            get
            {
                ContainerSession session = _session;
                if (session == null)
                {
                    throw new InvalidOperationException("No container is open, so there are no settings yet.");
                }
                return session.Settings;
            }
        }

        /// <summary>Settings when a container is open, otherwise null (safe for early start-up).</summary>
        public static AppSettings SettingsOrNull
        {
            get
            {
                ContainerSession session = _session;
                return session != null ? session.Settings : null;
            }
        }

        public static string ContainerRoot { get { return _session != null ? _session.Root : ""; } }

        public static string ContainerName { get { return _session != null ? _session.Name : ""; } }

        /// <summary>
        /// Attach a container and rebuild every cached list.
        ///
        /// Note the ordering: the session must be assigned *before* ReloadAll() runs, because
        /// ReloadAll() reads _session (and returns immediately when it is still null).  Getting this
        /// wrong left Snapshot null and made the first window that touched AppState.Data throw a
        /// NullReferenceException during start-up.
        /// </summary>
        public static void AttachContainer(ContainerSession session, bool remember)
        {
            ContainerSession previous;
            lock (Sync)
            {
                previous = _session;
                _session = session;          // assign first
                _snapshot = null;
            }

            if (previous != null && !ReferenceEquals(previous, session))
            {
                try { previous.Dispose(); } catch { }
            }

            if (remember && session != null)
            {
                Config.RememberContainer(session.Root);
                Config.Save();
            }

            ReloadAll();                     // now _session is visible to it

            EventHandler handler = ContainerChanged;
            if (handler != null) { handler(null, EventArgs.Empty); }
        }

        public static void DetachContainer()
        {
            lock (Sync)
            {
                if (_session != null)
                {
                    try { _session.Dispose(); } catch { }
                    _session = null;
                }
                _snapshot = null;
            }
            EventHandler handler = ContainerChanged;
            if (handler != null) { handler(null, EventArgs.Empty); }
        }

        // ---------------------------------------------------------------- snapshot
        /// <summary>The working set: everything the UI needs, loaded in one transaction.</summary>
        public sealed class Snapshot
        {
            public List<Category> Categories = new List<Category>();
            public List<Todo> Todos = new List<Todo>();
            public List<Exam> Exams = new List<Exam>();
            public List<Competition> Competitions = new List<Competition>();
            public List<ActivityEntry> Activity = new List<ActivityEntry>();
            public DateTime LoadedAt = DateTime.Now;

            public Category FindCategory(string id)
            {
                if (string.IsNullOrEmpty(id)) { return null; }
                for (int i = 0; i < Categories.Count; i++)
                {
                    if (Categories[i].Id == id) { return Categories[i]; }
                }
                return null;
            }

            public Category CategoryOrUncategorized(string id)
            {
                Category c = FindCategory(id);
                if (c != null) { return c; }
                for (int i = 0; i < Categories.Count; i++)
                {
                    if (Categories[i].IsUncategorized) { return Categories[i]; }
                }
                return null;
            }

            public Todo FindTodo(string id)
            {
                if (string.IsNullOrEmpty(id)) { return null; }
                for (int i = 0; i < Todos.Count; i++)
                {
                    if (Todos[i].Id == id) { return Todos[i]; }
                }
                return null;
            }

            public Exam FindExam(string id)
            {
                if (string.IsNullOrEmpty(id)) { return null; }
                for (int i = 0; i < Exams.Count; i++)
                {
                    if (Exams[i].Id == id) { return Exams[i]; }
                }
                return null;
            }

            public Competition FindCompetition(string id)
            {
                if (string.IsNullOrEmpty(id)) { return null; }
                for (int i = 0; i < Competitions.Count; i++)
                {
                    if (Competitions[i].Id == id) { return Competitions[i]; }
                }
                return null;
            }

            public Competition FindCompetitionByTag(string tag)
            {
                if (string.IsNullOrEmpty(tag)) { return null; }
                for (int i = 0; i < Competitions.Count; i++)
                {
                    Competition c = Competitions[i];
                    if (string.Equals(c.Tag, tag, StringComparison.OrdinalIgnoreCase)) { return c; }
                    if (string.Equals(c.Name, tag, StringComparison.OrdinalIgnoreCase)) { return c; }
                }
                return null;
            }

            /// <summary>Category colour, falling back to the Uncategorized colour.</summary>
            public string ColorOf(string categoryId)
            {
                Category c = CategoryOrUncategorized(categoryId);
                return c != null ? c.Color : Category.UncategorizedColor;
            }

            public string NameOf(string categoryId)
            {
                Category c = CategoryOrUncategorized(categoryId);
                return c != null ? c.Name : Category.UncategorizedName;
            }

            /// <summary>Competition that gives a todo its colour, when the todo is tagged with one.</summary>
            public Competition PrimaryCompetitionFor(Todo todo)
            {
                if (todo == null || todo.CompetitionIds.Count == 0) { return null; }
                for (int i = 0; i < todo.CompetitionIds.Count; i++)
                {
                    Competition c = FindCompetition(todo.CompetitionIds[i]);
                    if (c == null) { c = FindCompetitionByTag(todo.CompetitionIds[i]); }
                    if (c != null) { return c; }
                }
                return null;
            }

            public List<Competition> CompetitionsFor(Todo todo)
            {
                List<Competition> list = new List<Competition>();
                if (todo == null) { return list; }
                for (int i = 0; i < todo.CompetitionIds.Count; i++)
                {
                    Competition c = FindCompetition(todo.CompetitionIds[i]);
                    if (c == null) { c = FindCompetitionByTag(todo.CompetitionIds[i]); }
                    if (c != null && !list.Contains(c)) { list.Add(c); }
                }
                return list;
            }
        }

        public static Snapshot Data
        {
            get
            {
                Snapshot current = _snapshot;
                if (current != null) { return current; }
                ReloadAll();
                current = _snapshot;
                // Never hand out null: a view that touches Data before a container is open should see
                // empty lists rather than crash with a NullReferenceException.
                if (current == null)
                {
                    current = new Snapshot();
                    _snapshot = current;
                }
                return current;
            }
        }

        /// <summary>Reload every cached list from the database.</summary>
        public static void ReloadAll()
        {
            ContainerSession session = _session;
            if (session == null) { return; }
            Snapshot fresh = new Snapshot();
            Repository repo = session.Repository;
            fresh.Categories = repo.LoadCategories();
            fresh.Todos = repo.LoadTodos();
            fresh.Exams = repo.LoadExams();
            fresh.Competitions = repo.LoadCompetitions(true);
            fresh.Activity = repo.LoadActivity(120);

            // Attachments for todos are loaded lazily per item; counts come from the table.
            Dictionary<string, List<Attachment>> attachments = repo.LoadAllAttachments();
            for (int i = 0; i < fresh.Todos.Count; i++)
            {
                List<Attachment> bucket;
                if (attachments.TryGetValue(AttachmentOwner.Todo + "|" + fresh.Todos[i].Id, out bucket))
                {
                    fresh.Todos[i].Attachments = bucket;
                }
            }
            for (int i = 0; i < fresh.Exams.Count; i++)
            {
                List<Attachment> bucket;
                if (attachments.TryGetValue(AttachmentOwner.Exam + "|" + fresh.Exams[i].Id, out bucket))
                {
                    fresh.Exams[i].Attachments = bucket;
                }
            }
            // Link the todo side of competition relationships for the UI.
            for (int i = 0; i < fresh.Competitions.Count; i++)
            {
                Competition competition = fresh.Competitions[i];
                for (int j = 0; j < fresh.Todos.Count; j++)
                {
                    Todo todo = fresh.Todos[j];
                    for (int k = 0; k < todo.CompetitionIds.Count; k++)
                    {
                        if (todo.CompetitionIds[k] == competition.Id)
                        {
                            if (!competition.LinkedTodos.Contains(todo)) { competition.LinkedTodos.Add(todo); }
                            break;
                        }
                    }
                    if (!string.IsNullOrEmpty(competition.Tag))
                    {
                        for (int k = 0; k < todo.Tags.Count; k++)
                        {
                            if (string.Equals(todo.Tags[k], competition.Tag, StringComparison.OrdinalIgnoreCase))
                            {
                                if (!competition.LinkedTodos.Contains(todo)) { competition.LinkedTodos.Add(todo); }
                                break;
                            }
                        }
                    }
                }
            }
            _snapshot = fresh;
        }

        /// <summary>Persist pending changes and refresh the caches + all views.</summary>
        public static void Commit()
        {
            ReloadAll();
            RaiseChanged();
        }

        /// <summary>Refresh the caches and views without touching the database.</summary>
        public static void RaiseChanged()
        {
            if (_session != null)
            {
                try { _session.SaveSettings(); } catch { }
            }
            EventHandler handler = Changed;
            if (handler != null) { handler(null, EventArgs.Empty); }
        }

        public static event EventHandler Changed;

        // ---------------------------------------------------------------- helpers
        public static void Log(string kind, string refKind, string refId, string message)
        {
            if (_session == null) { return; }
            try { _session.Repository.Log(kind, refKind, refId, message); }
            catch { }
        }

        /// <summary>Colour used to draw a todo on the timeline and in lists.</summary>
        public static string ColorFor(Todo todo)
        {
            if (todo == null) { return Category.UncategorizedColor; }
            if (!string.IsNullOrEmpty(todo.ColorOverride)) { return todo.ColorOverride; }
            Competition competition = Data.PrimaryCompetitionFor(todo);
            if (competition != null && !string.IsNullOrEmpty(competition.Color)) { return competition.Color; }
            return Data.ColorOf(todo.CategoryId);
        }
    }
}
