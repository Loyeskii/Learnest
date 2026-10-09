using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Learnest.Core;
using Learnest.Data;

namespace Learnest.Services
{
    /// <summary>
    /// Container backup: full profile export as a single zip archive, and import of such an
    /// archive into a new folder.  Export names follow the product contract
    /// "yyyy-MM-dd_HH-mm-ss.containername.learnest.zip".
    /// </summary>
    public static class BackupService
    {
        public const string ExportSuffix = ".learnest.zip";

        /// <summary>Timestamp + container name + suffix, sanitised for the file system.</summary>
        public static string BuildExportFileName(ContainerSession session, DateTime when)
        {
            string containerName = AppPaths.SafeExportSegment(
                session.GetMeta("containerName", session.Name), AppPaths.DefaultContainerFolderName);
            return when.ToString("yyyy-MM-dd_HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture) +
                   "." + containerName + ExportSuffix;
        }

        /// <summary>
        /// Export the whole container (database, attachments, notes, logs, backgrounds) into
        /// <paramref name="targetFolder"/>.  The live database is checkpointed first and the
        /// resulting file is opened read only so a running Learnest can export safely.
        /// </summary>
        public static string ExportContainer(ContainerSession session, string targetFolder, out string fileName)
        {
            if (session == null) { throw new ArgumentNullException("session"); }
            if (string.IsNullOrEmpty(targetFolder)) { targetFolder = Path.Combine(session.Root, ContainerLayout.BackupsFolder); }
            Directory.CreateDirectory(targetFolder);

            session.Flush();
            session.Database.Checkpoint();

            fileName = BuildExportFileName(session, DateTime.Now);
            string target = MakeUnique(Path.Combine(targetFolder, fileName));
            fileName = Path.GetFileName(target);

            // Snapshot the database through a temporary copy so the archive never contains a
            // half written WAL state.
            string temporaryDatabase = Path.Combine(Path.GetTempPath(), "learnest-export-" + Guid.NewGuid().ToString("N") + ".db");
            File.Copy(Path.Combine(session.Root, ContainerLayout.DatabaseFile), temporaryDatabase, true);

            try
            {
                if (File.Exists(target)) { File.Delete(target); }
                using (FileStream stream = new FileStream(target, FileMode.CreateNew, FileAccess.Write))
                using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    AddFile(archive, temporaryDatabase, ContainerLayout.DatabaseFile);

                    AddOptionalFile(archive, Path.Combine(session.Root, ContainerLayout.ManifestFile), ContainerLayout.ManifestFile);
                    AddOptionalFile(archive, Path.Combine(session.Root, "readme.txt"), "readme.txt");

                    AddDirectory(archive, Path.Combine(session.Root, ContainerLayout.AttachmentsFolder), ContainerLayout.AttachmentsFolder);
                    AddDirectory(archive, Path.Combine(session.Root, ContainerLayout.NotesFolder), ContainerLayout.NotesFolder);
                    AddDirectory(archive, Path.Combine(session.Root, ContainerLayout.BackgroundsFolder), ContainerLayout.BackgroundsFolder);
                    AddDirectory(archive, Path.Combine(session.Root, ContainerLayout.LanguageFolder), ContainerLayout.LanguageFolder);
                    AddDirectory(archive, Path.Combine(session.Root, "languages"), "languages");

                    // A tiny descriptor so a user can tell what is inside without extracting.
                    WriteEntry(archive, "LEARNEST-EXPORT.txt",
                        "Learnest container export\r\n" +
                        "container: " + session.GetMeta("containerName", session.Name) + "\r\n" +
                        "exported at: " + IsoTime.Now() + "\r\n" +
                        "app version: " + AppPaths.AppVersionString() + "\r\n" +
                        "database: " + ContainerLayout.DatabaseFile + "\r\n\r\n" +
                        "要将此备份恢复为可用容器，请在 Learnest 中选择「导入容器数据包」。\r\n" +
                        "To restore, choose \"Import container archive\" inside Learnest.\r\n");
                }
            }
            finally
            {
                try { File.Delete(temporaryDatabase); } catch { }
            }

            session.Settings.LastBackupAt = IsoTime.Now();
            session.SaveSettings();
            session.Repository.Log("backup.export", "container", "", "导出容器：" + fileName);
            PruneExportFolder(targetFolder, session.Settings.AutoBackupKeep <= 0 ? 10 : session.Settings.AutoBackupKeep);
            return target;
        }

        /// <summary>Convenience overload used by the self test.</summary>
        public static string ExportContainer(ContainerSession session, string targetFolder, out string fileName, out string error)
        {
            error = null;
            try
            {
                return ExportContainer(session, targetFolder, out fileName);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                fileName = "";
                return null;
            }
        }

        /// <summary>
        /// Extract an export archive into <paramref name="targetFolder"/>.  The folder must not
        /// already contain a container.
        /// </summary>
        public static bool ImportContainer(string archivePath, string targetFolder, out string error)
        {
            error = null;
            try
            {
                if (!File.Exists(archivePath)) { error = "备份文件不存在。"; return false; }
                if (string.IsNullOrEmpty(targetFolder)) { error = "请选择恢复位置。"; return false; }

                BackupSummary summary = InspectArchive(archivePath, out error);
                if (summary == null) { return false; }

                if (ContainerLayout.IsContainer(targetFolder))
                {
                    error = "目标文件夹中已经存在一个 Learnest 容器。";
                    return false;
                }
                if (!ContainerLayout.IsAvailable(targetFolder) &&
                    Directory.Exists(targetFolder) && Directory.GetFileSystemEntries(targetFolder).Length > 0)
                {
                    error = "目标文件夹不为空，请选择一个空文件夹。";
                    return false;
                }

                Directory.CreateDirectory(targetFolder);
                using (ZipArchive archive = ZipFile.OpenRead(archivePath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string relative = entry.FullName.Replace('\\', '/');
                        if (relative.EndsWith("/", StringComparison.Ordinal)) { continue; }
                        if (relative.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase)) { continue; }
                        if (relative.IndexOf("..", StringComparison.Ordinal) >= 0) { continue; }

                        string destination = Path.GetFullPath(Path.Combine(targetFolder, relative.Replace('/', Path.DirectorySeparatorChar)));
                        string rootFull = Path.GetFullPath(targetFolder);
                        if (!destination.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) { continue; }

                        string directory = Path.GetDirectoryName(destination);
                        if (!string.IsNullOrEmpty(directory)) { Directory.CreateDirectory(directory); }
                        if (File.Exists(destination)) { File.Delete(destination); }
                        entry.ExtractToFile(destination, true);
                    }
                }

                if (!File.Exists(Path.Combine(targetFolder, ContainerLayout.DatabaseFile)))
                {
                    error = "备份包中没有找到 " + ContainerLayout.DatabaseFile + "，可能不是 Learnest 容器导出包。";
                    return false;
                }

                ContainerLayout.EnsureFolders(targetFolder);
                using (ContainerSession session = ContainerSession.Open(targetFolder))
                {
                    session.Repository.Log("backup.import", "container", "",
                        "导入容器：" + Path.GetFileName(archivePath));
                    session.Flush();
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public sealed class BackupSummary
        {
            public string ContainerName = "";
            public string ExportedAt = "";
            public string AppVersion = "";
            public int FileCount;
            public long TotalBytes;
            public bool HasDatabase;
        }

        /// <summary>Read the descriptor out of an archive without extracting it.</summary>
        public static BackupSummary InspectArchive(string archivePath, out string error)
        {
            error = null;
            BackupSummary summary = new BackupSummary();
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(archivePath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        summary.FileCount++;
                        summary.TotalBytes += entry.Length;
                        if (string.Equals(entry.FullName, ContainerLayout.DatabaseFile, StringComparison.OrdinalIgnoreCase))
                        {
                            summary.HasDatabase = true;
                        }
                        if (string.Equals(entry.FullName, "LEARNEST-EXPORT.txt", StringComparison.OrdinalIgnoreCase))
                        {
                            using (StreamReader reader = new StreamReader(entry.Open(), Utf8.Encoding))
                            {
                                string content = reader.ReadToEnd();
                                summary.ContainerName = ExtractValue(content, "container:");
                                summary.ExportedAt = ExtractValue(content, "exported at:");
                                summary.AppVersion = ExtractValue(content, "app version:");
                            }
                        }
                    }
                }
                if (!summary.HasDatabase)
                {
                    error = "压缩包中没有 " + ContainerLayout.DatabaseFile + "，无法作为容器导入。";
                    return null;
                }
                if (string.IsNullOrEmpty(summary.ContainerName))
                {
                    summary.ContainerName = AppPaths.SafeExportSegment(
                        Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(archivePath)), "Container");
                }
                return summary;
            }
            catch (Exception ex)
            {
                error = "无法读取备份包：" + ex.Message;
                return null;
            }
        }

        private static string ExtractValue(string content, string key)
        {
            int index = content.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (index < 0) { return ""; }
            int start = index + key.Length;
            int end = content.IndexOf('\n', start);
            if (end < 0) { end = content.Length; }
            return content.Substring(start, end - start).Trim();
        }

        private static void AddFile(ZipArchive archive, string path, string entryName)
        {
            archive.CreateEntryFromFile(path, entryName, CompressionLevel.Optimal);
        }

        private static void AddOptionalFile(ZipArchive archive, string path, string entryName)
        {
            if (File.Exists(path)) { archive.CreateEntryFromFile(path, entryName, CompressionLevel.Optimal); }
        }

        private static void AddDirectory(ZipArchive archive, string folder, string entryPrefix)
        {
            if (!Directory.Exists(folder)) { return; }
            string[] files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
            string rootFull = Path.GetFullPath(folder);
            for (int i = 0; i < files.Length; i++)
            {
                string relative = files[i].Substring(rootFull.Length).TrimStart(Path.DirectorySeparatorChar);
                string entryName = entryPrefix + "/" + relative.Replace(Path.DirectorySeparatorChar, '/');
                try
                {
                    archive.CreateEntryFromFile(files[i], entryName, CompressionLevel.Optimal);
                }
                catch
                {
                    // A single locked file must not abort the whole export.
                }
            }
        }

        private static void WriteEntry(ZipArchive archive, string entryName, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using (Stream stream = entry.Open())
            using (StreamWriter writer = new StreamWriter(stream, Utf8.Encoding))
            {
                writer.Write(content);
            }
        }

        private static string MakeUnique(string path)
        {
            if (!File.Exists(path)) { return path; }
            string directory = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            for (int i = 2; i < 1000; i++)
            {
                string candidate = Path.Combine(directory, name + " (" + i + ")" + extension);
                if (!File.Exists(candidate)) { return candidate; }
            }
            return path;
        }

        private static void PruneExportFolder(string folder, int keep)
        {
            try
            {
                if (keep <= 0) { return; }
                DirectoryInfo info = new DirectoryInfo(folder);
                FileInfo[] files = info.GetFiles("*" + ExportSuffix);
                if (files.Length <= keep) { return; }
                Array.Sort(files, delegate(FileInfo a, FileInfo b) { return b.Name.CompareTo(a.Name); });
                for (int i = keep; i < files.Length; i++)
                {
                    try { files[i].Delete(); } catch { }
                }
            }
            catch
            {
            }
        }

        /// <summary>Write a CSV export of the current todo list (a small extra courtesy).</summary>
        public static string ExportTodosCsv(ContainerSession session, string targetFolder, IList<Todo> todos)
        {
            Directory.CreateDirectory(targetFolder);
            string fileName = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".todos.csv";
            string target = MakeUnique(Path.Combine(targetFolder, fileName));
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("id,title,category,status,priority,progress,due_at,completed_at,created_at,updated_at,competitions,tags");
            for (int i = 0; i < todos.Count; i++)
            {
                Todo t = todos[i];
                sb.Append(Csv(t.Id)).Append(',');
                sb.Append(Csv(t.Title)).Append(',');
                sb.Append(Csv(session.Repository.FindCategory(t.CategoryId) != null
                    ? session.Repository.FindCategory(t.CategoryId).Name : Category.UncategorizedName)).Append(',');
                sb.Append(Csv(t.Status)).Append(',');
                sb.Append(t.Priority).Append(',');
                sb.Append(t.Progress.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(',');
                sb.Append(Csv(t.DueAt)).Append(',');
                sb.Append(Csv(t.CompletedAt)).Append(',');
                sb.Append(Csv(t.CreatedAt)).Append(',');
                sb.Append(Csv(t.UpdatedAt)).Append(',');
                sb.Append(Csv(string.Join(" | ", t.CompetitionIds.ToArray()))).Append(',');
                sb.Append(Csv(string.Join(" | ", t.Tags.ToArray())));
                sb.AppendLine();
            }
            Utf8.WriteAllText(target, sb.ToString());
            return target;
        }

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value)) { return ""; }
            bool quote = value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0 || value.IndexOf('\n') >= 0;
            string escaped = value.Replace("\"", "\"\"");
            return quote ? "\"" + escaped + "\"" : escaped;
        }
    }
}
