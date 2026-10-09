using System;

namespace Learnest.Data
{
    /// <summary>DDL for a brand new container.  Every statement is idempotent.</summary>
    internal static class Schema
    {
        internal const int Version = 1;

        internal static readonly string[] Statements = new string[]
        {
            "CREATE TABLE IF NOT EXISTS app_meta (" +
            "  key TEXT PRIMARY KEY," +
            "  value TEXT" +
            ");",

            "CREATE TABLE IF NOT EXISTS app_settings (" +
            "  key TEXT PRIMARY KEY," +
            "  value TEXT" +
            ");",

            "CREATE TABLE IF NOT EXISTS categories (" +
            "  id TEXT PRIMARY KEY," +
            "  name TEXT NOT NULL," +
            "  color TEXT NOT NULL DEFAULT '#3B82F6'," +
            "  description TEXT DEFAULT ''," +
            "  icon TEXT DEFAULT ''," +
            "  sort_order INTEGER NOT NULL DEFAULT 0," +
            "  is_system INTEGER NOT NULL DEFAULT 0," +
            "  created_at TEXT NOT NULL DEFAULT ''," +
            "  updated_at TEXT NOT NULL DEFAULT ''" +
            ");",

            "CREATE TABLE IF NOT EXISTS todos (" +
            "  id TEXT PRIMARY KEY," +
            "  title TEXT NOT NULL DEFAULT ''," +
            "  category_id TEXT DEFAULT ''," +
            "  status TEXT NOT NULL DEFAULT 'open'," +
            "  priority INTEGER NOT NULL DEFAULT 1," +
            "  progress REAL NOT NULL DEFAULT 0," +
            "  due_at TEXT DEFAULT ''," +
            "  start_at TEXT DEFAULT ''," +
            "  completed_at TEXT DEFAULT ''," +
            "  created_at TEXT NOT NULL DEFAULT ''," +
            "  updated_at TEXT NOT NULL DEFAULT ''," +
            "  note_markdown TEXT DEFAULT ''," +
            "  note_updated_at TEXT DEFAULT ''," +
            "  lead_minutes INTEGER NOT NULL DEFAULT 1440," +
            "  repeat_rule TEXT DEFAULT ''," +
            "  sort_order INTEGER NOT NULL DEFAULT 0," +
            "  color_override TEXT DEFAULT ''," +
            "  tags TEXT DEFAULT ''," +
            "  steps TEXT DEFAULT ''" +
            ");",

            "CREATE INDEX IF NOT EXISTS idx_todos_due ON todos(due_at);",
            "CREATE INDEX IF NOT EXISTS idx_todos_status ON todos(status);",
            "CREATE INDEX IF NOT EXISTS idx_todos_category ON todos(category_id);",
            "CREATE INDEX IF NOT EXISTS idx_todos_updated ON todos(updated_at);",

            "CREATE TABLE IF NOT EXISTS todo_competitions (" +
            "  todo_id TEXT NOT NULL," +
            "  competition_id TEXT NOT NULL," +
            "  PRIMARY KEY (todo_id, competition_id)," +
            "  FOREIGN KEY (todo_id) REFERENCES todos(id) ON DELETE CASCADE," +
            "  FOREIGN KEY (competition_id) REFERENCES competitions(id) ON DELETE CASCADE" +
            ");",

            "CREATE TABLE IF NOT EXISTS todo_milestones (" +
            "  todo_id TEXT NOT NULL," +
            "  milestone_id TEXT NOT NULL," +
            "  PRIMARY KEY (todo_id, milestone_id)," +
            "  FOREIGN KEY (todo_id) REFERENCES todos(id) ON DELETE CASCADE," +
            "  FOREIGN KEY (milestone_id) REFERENCES milestones(id) ON DELETE CASCADE" +
            ");",

            "CREATE TABLE IF NOT EXISTS attachments (" +
            "  id TEXT PRIMARY KEY," +
            "  owner_kind TEXT NOT NULL," +
            "  owner_id TEXT NOT NULL," +
            "  file_name TEXT NOT NULL," +
            "  relative_path TEXT NOT NULL," +
            "  size_bytes INTEGER NOT NULL DEFAULT 0," +
            "  mime TEXT DEFAULT ''," +
            "  category TEXT DEFAULT ''," +
            "  note TEXT DEFAULT ''," +
            "  is_folder INTEGER NOT NULL DEFAULT 0," +
            "  created_at TEXT NOT NULL DEFAULT ''," +
            "  sort_order INTEGER NOT NULL DEFAULT 0" +
            ");",

            "CREATE INDEX IF NOT EXISTS idx_attachments_owner ON attachments(owner_kind, owner_id);",

            "CREATE TABLE IF NOT EXISTS exams (" +
            "  id TEXT PRIMARY KEY," +
            "  title TEXT NOT NULL DEFAULT ''," +
            "  course TEXT DEFAULT ''," +
            "  category_id TEXT DEFAULT ''," +
            "  start_at TEXT NOT NULL DEFAULT ''," +
            "  end_at TEXT DEFAULT ''," +
            "  location TEXT DEFAULT ''," +
            "  seat TEXT DEFAULT ''," +
            "  status TEXT NOT NULL DEFAULT 'planned'," +
            "  note_markdown TEXT DEFAULT ''," +
            "  remind_minutes INTEGER NOT NULL DEFAULT 1440," +
            "  pin_countdown INTEGER NOT NULL DEFAULT 1," +
            "  score TEXT DEFAULT ''," +
            "  created_at TEXT NOT NULL DEFAULT ''," +
            "  updated_at TEXT NOT NULL DEFAULT ''," +
            "  sort_order INTEGER NOT NULL DEFAULT 0" +
            ");",

            "CREATE INDEX IF NOT EXISTS idx_exams_start ON exams(start_at);",

            "CREATE TABLE IF NOT EXISTS competitions (" +
            "  id TEXT PRIMARY KEY," +
            "  name TEXT NOT NULL DEFAULT ''," +
            "  tag TEXT DEFAULT ''," +
            "  color TEXT NOT NULL DEFAULT '#8B5CF6'," +
            "  organizer TEXT DEFAULT ''," +
            "  stage TEXT NOT NULL DEFAULT 'planned'," +
            "  level TEXT DEFAULT ''," +
            "  description TEXT DEFAULT ''," +
            "  note_markdown TEXT DEFAULT ''," +
            "  start_at TEXT DEFAULT ''," +
            "  end_at TEXT DEFAULT ''," +
            "  progress REAL NOT NULL DEFAULT 0," +
            "  starred INTEGER NOT NULL DEFAULT 0," +
            "  team TEXT DEFAULT ''," +
            "  links TEXT DEFAULT ''," +
            "  attachment_categories TEXT DEFAULT ''," +
            "  created_at TEXT NOT NULL DEFAULT ''," +
            "  updated_at TEXT NOT NULL DEFAULT ''," +
            "  sort_order INTEGER NOT NULL DEFAULT 0" +
            ");",

            "CREATE INDEX IF NOT EXISTS idx_competitions_tag ON competitions(tag);",

            "CREATE TABLE IF NOT EXISTS milestones (" +
            "  id TEXT PRIMARY KEY," +
            "  competition_id TEXT NOT NULL," +
            "  title TEXT NOT NULL DEFAULT ''," +
            "  detail TEXT DEFAULT ''," +
            "  note_markdown TEXT DEFAULT ''," +
            "  due_at TEXT DEFAULT ''," +
            "  completed_at TEXT DEFAULT ''," +
            "  is_done INTEGER NOT NULL DEFAULT 0," +
            "  weight REAL NOT NULL DEFAULT 1," +
            "  created_at TEXT NOT NULL DEFAULT ''," +
            "  updated_at TEXT NOT NULL DEFAULT ''," +
            "  sort_order INTEGER NOT NULL DEFAULT 0," +
            "  FOREIGN KEY (competition_id) REFERENCES competitions(id) ON DELETE CASCADE" +
            ");",

            "CREATE INDEX IF NOT EXISTS idx_milestones_competition ON milestones(competition_id);",

            "CREATE TABLE IF NOT EXISTS competition_events (" +
            "  id TEXT PRIMARY KEY," +
            "  competition_id TEXT NOT NULL," +
            "  title TEXT NOT NULL DEFAULT ''," +
            "  kind TEXT NOT NULL DEFAULT 'event'," +
            "  start_at TEXT NOT NULL DEFAULT ''," +
            "  end_at TEXT DEFAULT ''," +
            "  location TEXT DEFAULT ''," +
            "  detail TEXT DEFAULT ''," +
            "  note_markdown TEXT DEFAULT ''," +
            "  all_day INTEGER NOT NULL DEFAULT 0," +
            "  created_at TEXT NOT NULL DEFAULT ''," +
            "  updated_at TEXT NOT NULL DEFAULT ''," +
            "  sort_order INTEGER NOT NULL DEFAULT 0," +
            "  FOREIGN KEY (competition_id) REFERENCES competitions(id) ON DELETE CASCADE" +
            ");",

            "CREATE INDEX IF NOT EXISTS idx_events_competition ON competition_events(competition_id);",

            "CREATE TABLE IF NOT EXISTS activity_log (" +
            "  id TEXT PRIMARY KEY," +
            "  at TEXT NOT NULL DEFAULT ''," +
            "  kind TEXT NOT NULL DEFAULT 'info'," +
            "  ref_kind TEXT DEFAULT ''," +
            "  ref_id TEXT DEFAULT ''," +
            "  message TEXT NOT NULL DEFAULT ''" +
            ");",

            "CREATE INDEX IF NOT EXISTS idx_activity_at ON activity_log(at);",

            "CREATE TABLE IF NOT EXISTS custom_calendar_marks (" +
            "  id TEXT PRIMARY KEY," +
            "  day TEXT NOT NULL," +
            "  title TEXT NOT NULL DEFAULT ''," +
            "  color TEXT DEFAULT '#3B82F6'," +
            "  kind TEXT DEFAULT 'custom'," +
            "  note TEXT DEFAULT ''" +
            ");"
        };
    }
}
