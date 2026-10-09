using System;
using System.Collections.Generic;
using System.IO;
using Learnest.Core;

namespace Learnest.Data
{
    /// <summary>Describes the container folder layout that Learnest recognises.</summary>
    public static class ContainerLayout
    {
        public const string DatabaseFile = "learnest.db";
        public const string ManifestFile = "learnest.container.json";
        public const string AttachmentsFolder = "attachments";
        public const string NotesFolder = "notes";
        public const string BackupsFolder = "backups";
        public const string LogsFolder = "logs";
        public const string LanguageFolder = "languages";
        public const string BackgroundsFolder = "backgrounds";
        public const string MarkerExtension = ".learnest";

        /// <summary>True when the folder already holds a Learnest container.</summary>
        public static bool IsContainer(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) { return false; }
            if (File.Exists(Path.Combine(folder, DatabaseFile))) { return true; }
            if (File.Exists(Path.Combine(folder, ManifestFile))) { return true; }
            return false;
        }

        /// <summary>True when the folder is empty enough to become a new container.</summary>
        public static bool IsAvailable(string folder)
        {
            if (string.IsNullOrEmpty(folder)) { return false; }
            if (!Directory.Exists(folder)) { return true; }
            try
            {
                string[] entries = Directory.GetFileSystemEntries(folder);
                for (int i = 0; i < entries.Length; i++)
                {
                    string name = Path.GetFileName(entries[i]);
                    if (name == "desktop.ini" || name == ".DS_Store") { continue; }
                    return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void EnsureFolders(string root)
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, AttachmentsFolder));
            Directory.CreateDirectory(Path.Combine(root, NotesFolder));
            Directory.CreateDirectory(Path.Combine(root, BackupsFolder));
            Directory.CreateDirectory(Path.Combine(root, LogsFolder));
            Directory.CreateDirectory(Path.Combine(root, LanguageFolder));
            Directory.CreateDirectory(Path.Combine(root, BackgroundsFolder));
        }
    }

    /// <summary>
    /// An open container: folder + SQLite database + settings + repository.  This is the object
    /// the whole UI talks to, and it can be swapped at runtime when the user opens another
    /// container.
    /// </summary>
    public sealed class ContainerSession : IDisposable
    {
        public string Root { get; private set; }
        public string Name { get; private set; }
        public SqliteDatabase Database { get; private set; }
        public AppSettings Settings { get; set; }
        public Repository Repository { get; private set; }
        public bool IsNew { get; private set; }
        public string CreatedAt { get; private set; }

        private ContainerSession(string root, SqliteDatabase database, bool isNew)
        {
            Root = root;
            Name = new DirectoryInfo(root).Name;
            Database = database;
            IsNew = isNew;
            Settings = AppSettings.LoadFrom(database);
            Repository = new Repository(this);
            CreatedAt = GetMeta("createdAt", IsoTime.Now());
            TouchOpened();
        }

        // -------------------------------------------------------------- creation
        /// <summary>Create a brand new container in <paramref name="folder"/>.</summary>
        public static ContainerSession Create(string folder, string displayName)
        {
            if (string.IsNullOrEmpty(folder)) { throw new ArgumentException("A container folder is required.", "folder"); }
            folder = Path.GetFullPath(folder);
            if (File.Exists(folder)) { throw new IOException("The selected path is a file, not a folder: " + folder); }

            ContainerLayout.EnsureFolders(folder);
            string databasePath = Path.Combine(folder, ContainerLayout.DatabaseFile);
            if (File.Exists(databasePath))
            {
                throw new IOException("This folder already contains a Learnest container. Use \"open existing container\" instead.");
            }

            SqliteDatabase database = SqliteDatabase.Open(databasePath);
            database.Transaction(delegate
            {
                for (int i = 0; i < Schema.Statements.Length; i++)
                {
                    database.Execute(Schema.Statements[i]);
                }
            });

            ContainerSession session = new ContainerSession(folder, database, true);
            session.WriteMeta("schemaVersion", Schema.Version.ToString(System.Globalization.CultureInfo.InvariantCulture));
            session.WriteMeta("appName", AppPaths.AppName);
            session.WriteMeta("containerName", string.IsNullOrEmpty(displayName) ? session.Name : displayName);
            session.WriteMeta("createdAt", IsoTime.Now());
            session.WriteMeta("createdBy", AppPaths.AppVersionString());
            session.WriteManifest();
            session.Repository.SeedDefaults();
            session.Settings.SaveTo(database);
            session.Repository.Log("info", "container", "", "创建容器：" + session.Name);
            database.Checkpoint();
            return session;
        }

        // -------------------------------------------------------------- opening
        public static ContainerSession Open(string folder)
        {
            if (string.IsNullOrEmpty(folder)) { throw new ArgumentException("A container folder is required.", "folder"); }
            folder = Path.GetFullPath(folder);
            if (!Directory.Exists(folder)) { throw new DirectoryNotFoundException("Container folder not found: " + folder); }

            string databasePath = Path.Combine(folder, ContainerLayout.DatabaseFile);
            if (!File.Exists(databasePath))
            {
                throw new FileNotFoundException(
                    "The folder does not contain a Learnest container (learnest.db is missing). " +
                    "Choose \"create new container\" if this is an empty folder.", databasePath);
            }

            ContainerLayout.EnsureFolders(folder);
            SqliteDatabase database = SqliteDatabase.Open(databasePath);
            Migrations.Apply(database);
            ContainerSession session = new ContainerSession(folder, database, false);
            session.Repository.SeedDefaults();
            return session;
        }

        /// <summary>Quick health check used by the "open container" dialog.</summary>
        public static string ValidateContainer(string folder)
        {
            if (string.IsNullOrEmpty(folder)) { return "请选择一个文件夹。"; }
            if (!Directory.Exists(folder)) { return "文件夹不存在。"; }
            string databasePath = Path.Combine(folder, ContainerLayout.DatabaseFile);
            if (!File.Exists(databasePath))
            {
                if (ContainerLayout.IsAvailable(folder)) { return null; }  // usable as a new container
                return "该文件夹既不是 Learnest 容器，也不为空。";
            }
            try
            {
                using (SqliteDatabase probe = SqliteDatabase.Open(databasePath))
                {
                    string version = probe.ExecuteScalarString("SELECT value FROM app_meta WHERE key='schemaVersion';");
                    if (version == null)
                    {
                        // A database without our meta table cannot be one of ours.
                        probe.Execute("SELECT 1 FROM sqlite_master WHERE type='table' AND name='todos';");
                    }
                }
            }
            catch (Exception ex)
            {
                return "无法读取容器数据库：" + ex.Message;
            }
            return null;
        }

        // -------------------------------------------------------------- meta
        public string GetMeta(string key, string fallback)
        {
            try
            {
                string value = Database.ExecuteScalarString("SELECT value FROM app_meta WHERE key = ?;", key);
                return string.IsNullOrEmpty(value) ? fallback : value;
            }
            catch
            {
                return fallback;
            }
        }

        public void WriteMeta(string key, string value)
        {
            Database.ExecuteNonQuery(
                "INSERT INTO app_meta(key, value) VALUES(?, ?) " +
                "ON CONFLICT(key) DO UPDATE SET value = excluded.value;", key, value ?? "");
        }

        private void TouchOpened()
        {
            WriteMeta("lastOpenedAt", IsoTime.Now());
            WriteMeta("lastOpenedBy", AppPaths.AppVersionString());
            WriteMeta("lastOpenedMachine", Environment.MachineName);
        }

        // -------------------------------------------------------------- manifest
        /// <summary>
        /// learnest.container.json is a tiny, human readable descriptor so a container can be
        /// identified even without opening the database.
        /// </summary>
        public void WriteManifest()
        {
            JsonObject o = new JsonObject();
            o["app"] = JsonValue.String(AppPaths.AppName);
            o["tagline"] = JsonValue.String(AppPaths.Tagline);
            o["format"] = JsonValue.Number(1);
            o["schemaVersion"] = JsonValue.Number(Schema.Version);
            o["containerName"] = JsonValue.String(GetMeta("containerName", Name));
            o["createdAt"] = JsonValue.String(GetMeta("createdAt", IsoTime.Now()));
            o["lastOpenedAt"] = JsonValue.String(IsoTime.Now());
            o["appVersion"] = JsonValue.String(AppPaths.AppVersionString());
            o["database"] = JsonValue.String(ContainerLayout.DatabaseFile);
            JsonArray folders = new JsonArray();
            folders.Add(ContainerLayout.AttachmentsFolder);
            folders.Add(ContainerLayout.NotesFolder);
            folders.Add(ContainerLayout.BackupsFolder);
            folders.Add(ContainerLayout.LogsFolder);
            folders.Add(ContainerLayout.BackgroundsFolder);
            o["folders"] = JsonValue.Array(folders);
            try
            {
                Utf8.WriteAllText(Path.Combine(Root, ContainerLayout.ManifestFile), JsonValue.Object(o).ToPrettyString());
            }
            catch
            {
                // The manifest is a convenience only.
            }
        }

        // -------------------------------------------------------------- paths
        /// <summary>Turn a container relative path into an absolute one, guarding traversal.</summary>
        public string ResolveRelative(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) { return Root; }
            string combined = Path.GetFullPath(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string rootFull = Path.GetFullPath(Root);
            if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Path escapes the container folder: " + relativePath);
            }
            return combined;
        }

        public string ToRelative(string absolutePath)
        {
            string full = Path.GetFullPath(absolutePath);
            string rootFull = Path.GetFullPath(Root) + Path.DirectorySeparatorChar;
            if (full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            {
                return full.Substring(rootFull.Length).Replace(Path.DirectorySeparatorChar, '/');
            }
            return full.Replace(Path.DirectorySeparatorChar, '/');
        }

        public string AttachmentsFolderFor(string ownerKind, string ownerId)
        {
            return Path.Combine(Root, ContainerLayout.AttachmentsFolder, ownerKind, AppPaths.SafeFileName(ownerId, "item"));
        }

        public string NotesFolderFor(string ownerKind)
        {
            return Path.Combine(Root, ContainerLayout.NotesFolder, ownerKind);
        }

        public string NotePathFor(string ownerKind, string ownerId)
        {
            return Path.Combine(NotesFolderFor(ownerKind), AppPaths.SafeFileName(ownerId, "note") + ".md");
        }

        /// <summary>Write the markdown body both to the database and to notes/ for easy backup.</summary>
        public void MirrorNote(string ownerKind, string ownerId, string markdown)
        {
            try
            {
                string path = NotePathFor(ownerKind, ownerId);
                if (string.IsNullOrEmpty(markdown))
                {
                    if (File.Exists(path)) { File.Delete(path); }
                    return;
                }
                Utf8.WriteAllText(path, markdown);
            }
            catch
            {
                // Mirroring is best effort; the database is the source of truth.
            }
        }

        // -------------------------------------------------------------- lifecycle
        public void SaveSettings()
        {
            Settings.SaveTo(Database);
        }

        public void Flush()
        {
            try
            {
                SaveSettings();
                WriteManifest();
                Database.Checkpoint();
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            Flush();
            if (Database != null)
            {
                Database.Dispose();
                Database = null;
            }
        }
    }

    /// <summary>Schema upgrades.  Version 1 is the initial format.</summary>
    internal static class Migrations
    {
        internal static void Apply(SqliteDatabase db)
        {
            int current = 0;
            try
            {
                string value = db.ExecuteScalarString("SELECT value FROM app_meta WHERE key='schemaVersion';");
                int.TryParse(value, out current);
            }
            catch
            {
                current = 0;
            }

            if (current == 0)
            {
                // Either a pre-release container or a fresh database: (re)create everything.
                db.Transaction(delegate
                {
                    for (int i = 0; i < Schema.Statements.Length; i++)
                    {
                        db.Execute(Schema.Statements[i]);
                    }
                });
                current = Schema.Version;
            }

            if (current < Schema.Version)
            {
                // Future versions append ALTER TABLE statements here.
                for (int version = current + 1; version <= Schema.Version; version++)
                {
                    Upgrade(db, version);
                }
                current = Schema.Version;
            }

            db.ExecuteNonQuery(
                "INSERT INTO app_meta(key, value) VALUES('schemaVersion', ?) " +
                "ON CONFLICT(key) DO UPDATE SET value = excluded.value;",
                current.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private static void Upgrade(SqliteDatabase db, int version)
        {
            switch (version)
            {
                case 1:
                    for (int i = 0; i < Schema.Statements.Length; i++) { db.Execute(Schema.Statements[i]); }
                    break;
                default:
                    break;
            }
        }
    }
}
