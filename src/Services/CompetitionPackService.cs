using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Learnest.Core;
using Learnest.Data;

namespace Learnest.Services
{
    /// <summary>
    /// "Pack and go" for a single competition.  Produces one zip that contains the project brief
    /// (markdown + json), every attachment in its original relative layout and a manifest, so the
    /// project can be handed to a team mate or carried to another machine even without the rest of
    /// the container.
    /// </summary>
    public static class CompetitionPackService
    {
        public const string Suffix = ".learnest-competition.zip";
        public const string BriefFile = "competition.md";
        public const string DataFile = "competition.json";
        public const string ManifestFile = "MANIFEST.txt";

        public static string BuildFileName(Competition competition, DateTime when)
        {
            string name = AppPaths.SafeExportSegment(competition.Name, "competition");
            return when.ToString("yyyy-MM-dd_HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture) + "." + name + Suffix;
        }

        public static string Pack(ContainerSession session, Competition competition, string targetFolder)
        {
            if (session == null) { throw new ArgumentNullException("session"); }
            if (competition == null) { throw new ArgumentNullException("competition"); }
            if (string.IsNullOrEmpty(targetFolder)) { targetFolder = session.Root; }
            Directory.CreateDirectory(targetFolder);

            string target = Path.Combine(targetFolder, BuildFileName(competition, DateTime.Now));
            if (File.Exists(target)) { File.Delete(target); }

            using (FileStream stream = new FileStream(target, FileMode.CreateNew, FileAccess.Write))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteText(archive, DataFile, competition.ToJson().ToPrettyString());
                WriteText(archive, BriefFile, BuildBrief(session, competition));
                WriteText(archive, ManifestFile, BuildManifest(session, competition));

                for (int i = 0; i < competition.Attachments.Count; i++)
                {
                    Attachment attachment = competition.Attachments[i];
                    try
                    {
                        string source = session.ResolveRelative(attachment.RelativePath);
                        if (attachment.IsFolder && Directory.Exists(source))
                        {
                            AddFolder(archive, source, "files/" + SafeSegment(attachment.FileName) + "/");
                        }
                        else if (File.Exists(source))
                        {
                            archive.CreateEntryFromFile(source, "files/" + SafeSegment(attachment.FileName), CompressionLevel.Optimal);
                        }
                    }
                    catch
                    {
                        // A single missing or locked file must not abort the pack.
                    }
                }
            }

            session.Repository.Log("competition.pack", "competition", competition.Id,
                "打包竞赛：" + Path.GetFileName(target));
            session.Flush();
            return target;
        }

        /// <summary>Human readable brief; also useful when the zip is read without Learnest.</summary>
        public static string BuildBrief(ContainerSession session, Competition competition)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("# " + competition.Name);
            sb.AppendLine();
            sb.AppendLine("- tag: `" + competition.Tag + "`");
            sb.AppendLine("- stage: " + competition.Stage);
            if (!string.IsNullOrEmpty(competition.Organizer)) { sb.AppendLine("- organizer: " + competition.Organizer); }
            if (!string.IsNullOrEmpty(competition.Level)) { sb.AppendLine("- level: " + competition.Level); }
            if (!string.IsNullOrEmpty(competition.StartAt)) { sb.AppendLine("- start: " + competition.StartAt); }
            if (!string.IsNullOrEmpty(competition.EndAt)) { sb.AppendLine("- end: " + competition.EndAt); }
            if (competition.Team.Count > 0) { sb.AppendLine("- team: " + string.Join(", ", competition.Team.ToArray())); }
            if (competition.Links.Count > 0)
            {
                sb.AppendLine("- links:");
                for (int i = 0; i < competition.Links.Count; i++) { sb.AppendLine("  - " + competition.Links[i]); }
            }
            sb.AppendLine("- progress: " + (competition.MilestoneProgress() * 100).ToString("0") + "%");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(competition.Description))
            {
                sb.AppendLine("## " + Loc.T("common.description"));
                sb.AppendLine();
                sb.AppendLine(competition.Description);
                sb.AppendLine();
            }

            sb.AppendLine("## " + Loc.T("comp.milestones"));
            sb.AppendLine();
            if (competition.Milestones.Count == 0) { sb.AppendLine("(none)"); }
            for (int i = 0; i < competition.Milestones.Count; i++)
            {
                Milestone milestone = competition.Milestones[i];
                sb.AppendLine("- [" + (milestone.IsDone ? "x" : " ") + "] " + milestone.Title +
                              (milestone.DueDate.HasValue ? "  (" + IsoTime.Format(milestone.DueDate.Value) + ")" : ""));
                if (!string.IsNullOrEmpty(milestone.Detail)) { sb.AppendLine("      " + milestone.Detail); }
                if (!string.IsNullOrEmpty(milestone.NoteMarkdown))
                {
                    sb.AppendLine();
                    sb.AppendLine(Indent(milestone.NoteMarkdown, "      "));
                    sb.AppendLine();
                }
            }

            sb.AppendLine();
            sb.AppendLine("## " + Loc.T("comp.events"));
            sb.AppendLine();
            if (competition.Events.Count == 0) { sb.AppendLine("(none)"); }
            for (int i = 0; i < competition.Events.Count; i++)
            {
                CompetitionEvent entry = competition.Events[i];
                sb.AppendLine("- " + entry.StartAt + "  " + entry.Title + "  [" + entry.Kind + "]" +
                              (string.IsNullOrEmpty(entry.Location) ? "" : "  @" + entry.Location));
                if (!string.IsNullOrEmpty(entry.Detail)) { sb.AppendLine("      " + entry.Detail); }
            }

            if (competition.Attachments.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## " + Loc.T("comp.files"));
                sb.AppendLine();
                sb.AppendLine("Files are stored under `files/` in this archive.");
                for (int i = 0; i < competition.Attachments.Count; i++)
                {
                    Attachment attachment = competition.Attachments[i];
                    sb.AppendLine("- " + attachment.FileName +
                                  (string.IsNullOrEmpty(attachment.Category) ? "" : "  [" + attachment.Category + "]") +
                                  "  (" + attachment.DisplaySize + ")");
                }
            }

            if (!string.IsNullOrEmpty(competition.NoteMarkdown))
            {
                sb.AppendLine();
                sb.AppendLine("## " + Loc.T("comp.worklog"));
                sb.AppendLine();
                sb.AppendLine(competition.NoteMarkdown);
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine(Loc.T("app.name") + " · " + AppPaths.Tagline);
            sb.AppendLine("container: " + session.GetMeta("containerName", session.Name) + "  |  packed at " + IsoTime.Now());
            return sb.ToString();
        }

        private static string BuildManifest(ContainerSession session, Competition competition)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("Learnest competition pack");
            sb.AppendLine("app: " + AppPaths.AppName + " " + AppPaths.AppVersionString());
            sb.AppendLine("container: " + session.GetMeta("containerName", session.Name));
            sb.AppendLine("competition: " + competition.Name);
            sb.AppendLine("tag: " + competition.Tag);
            sb.AppendLine("id: " + competition.Id);
            sb.AppendLine("packed at: " + IsoTime.Now());
            sb.AppendLine("attachments: " + competition.Attachments.Count);
            sb.AppendLine("milestones: " + competition.Milestones.Count);
            sb.AppendLine("events: " + competition.Events.Count);
            sb.AppendLine();
            sb.AppendLine("Import this pack into another Learnest container from");
            sb.AppendLine("Competitions Planner > Import competition pack.");
            return sb.ToString();
        }

        /// <summary>Re-import a pack into the current container as a new competition.</summary>
        public static Competition Import(ContainerSession session, string archivePath, out string error)
        {
            error = null;
            try
            {
                if (!File.Exists(archivePath)) { error = Loc.T("container.invalid_folder"); return null; }

                string json = null;
                Dictionary<string, string> files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string staging = Path.Combine(Path.GetTempPath(), "learnest-pack-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(staging);

                using (ZipArchive archive = ZipFile.OpenRead(archivePath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string relative = entry.FullName.Replace('\\', '/');
                        if (relative.IndexOf("..", StringComparison.Ordinal) >= 0) { continue; }
                        if (string.Equals(relative, DataFile, StringComparison.OrdinalIgnoreCase))
                        {
                            using (StreamReader reader = new StreamReader(entry.Open(), Utf8.Encoding))
                            {
                                json = reader.ReadToEnd();
                            }
                            continue;
                        }
                        if (!relative.StartsWith("files/", StringComparison.OrdinalIgnoreCase)) { continue; }
                        if (relative.EndsWith("/", StringComparison.Ordinal)) { continue; }
                        string destination = Path.Combine(staging, relative.Substring("files/".Length).Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        entry.ExtractToFile(destination, true);
                        files[relative.Substring("files/".Length)] = destination;
                    }
                }

                if (string.IsNullOrEmpty(json))
                {
                    error = Loc.T("error.import_invalid");
                    return null;
                }

                JsonValue parsed;
                if (!JsonValue.TryParse(json, out parsed))
                {
                    error = Loc.T("error.parse_failed", archivePath);
                    return null;
                }

                Competition competition = Competition.FromJson(parsed);
                competition.Id = IsoTime.ShortId("comp");
                competition.CreatedAt = IsoTime.Now();
                competition.UpdatedAt = competition.CreatedAt;
                competition.Name = competition.Name + " (imported)";
                if (string.IsNullOrEmpty(competition.Tag)) { competition.Tag = "imported"; }
                competition.Tag = competition.Tag + "-imported";
                for (int i = 0; i < competition.Milestones.Count; i++)
                {
                    competition.Milestones[i].Id = IsoTime.ShortId("ms");
                    competition.Milestones[i].CompetitionId = competition.Id;
                }
                for (int i = 0; i < competition.Events.Count; i++)
                {
                    competition.Events[i].Id = IsoTime.ShortId("evt");
                    competition.Events[i].CompetitionId = competition.Id;
                }

                List<Attachment> original = competition.Attachments;
                competition.Attachments = new List<Attachment>();
                session.Repository.SaveCompetition(competition);
                for (int i = 0; i < original.Count; i++)
                {
                    string source;
                    if (!files.TryGetValue(SafeSegment(original[i].FileName), out source))
                    {
                        // Fall back to a case insensitive match on the file name only.
                        foreach (KeyValuePair<string, string> pair in files)
                        {
                            if (string.Equals(Path.GetFileName(pair.Key), original[i].FileName, StringComparison.OrdinalIgnoreCase))
                            {
                                source = pair.Value;
                                break;
                            }
                        }
                    }
                    if (source == null) { continue; }
                    try
                    {
                        session.Repository.ImportAttachment(AttachmentOwner.Competition, competition.Id, source,
                            original[i].Category, original[i].FileName);
                    }
                    catch
                    {
                    }
                }
                session.Repository.Log("competition.import", "competition", competition.Id,
                    "导入竞赛包：" + Path.GetFileName(archivePath));
                session.Flush();
                try { Directory.Delete(staging, true); } catch { }
                return competition;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        private static void WriteText(ZipArchive archive, string entryName, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using (Stream stream = entry.Open())
            using (StreamWriter writer = new StreamWriter(stream, Utf8.Encoding))
            {
                writer.Write(content);
            }
        }

        private static void AddFolder(ZipArchive archive, string folder, string prefix)
        {
            string[] files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
            string rootFull = Path.GetFullPath(folder);
            for (int i = 0; i < files.Length; i++)
            {
                string relative = files[i].Substring(rootFull.Length).TrimStart(Path.DirectorySeparatorChar);
                try
                {
                    archive.CreateEntryFromFile(files[i], prefix + relative.Replace(Path.DirectorySeparatorChar, '/'),
                        CompressionLevel.Optimal);
                }
                catch
                {
                }
            }
        }

        private static string SafeSegment(string name)
        {
            return AppPaths.SafeFileName(name, "file");
        }

        private static string Indent(string text, string prefix)
        {
            string normalized = text.Replace("\r\n", "\n");
            string[] lines = normalized.Split('\n');
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                sb.Append(prefix).AppendLine(lines[i]);
            }
            return sb.ToString().TrimEnd();
        }
    }
}
