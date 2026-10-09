using System;
using System.Collections.Generic;
using System.IO;

namespace Learnest
{
    /// <summary>
    /// Application identity plus the few paths Learnest needs outside a container.
    ///
    /// Data layout contract:
    ///   * %LocalAppData%\Learnest\appsettings.json  - machine scoped, records only the last
    ///     container path, the recent container list and machine wide UI fallbacks.
    ///   * &lt;container&gt;\...                         - every piece of business data: the SQLite
    ///     database, attachments, exported notes, logs, backups and backgrounds.  All references
    ///     inside the container are stored as container relative paths.
    /// </summary>
    public static class AppPaths
    {
        public const string AppName = "Learnest";
        public const string Tagline = "A gateway to everyday management";
        public const string DefaultContainerFolderName = "LearnestContainer";

        /// <summary>Human readable version taken from the assembly, e.g. "1.0.0".</summary>
        public static string AppVersionString()
        {
            try
            {
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetEntryAssembly();
                if (assembly == null) { assembly = typeof(AppPaths).Assembly; }
                System.Diagnostics.FileVersionInfo info = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
                if (info != null && !string.IsNullOrEmpty(info.FileVersion)) { return info.FileVersion; }
                Version version = assembly.GetName().Version;
                if (version != null) { return version.ToString(3); }
            }
            catch
            {
            }
            return "1.0.0";
        }

        /// <summary>User profile when available, otherwise the temp folder.  Used as a picker start.</summary>
        public static string UserProfileOrTemp()
        {
            string profile = SafeSpecialFolder(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(profile) && Directory.Exists(profile)) { return profile; }
            return Path.GetTempPath();
        }

        /// <summary>
        /// Where Windows suggests a container should live by default.
        ///
        /// This is picked by actually testing writability rather than trusting
        /// <see cref="Environment.SpecialFolder.MyDocuments"/>: on machines where the Documents
        /// folder has been redirected (OneDrive, a moved profile, or a group policy) the special
        /// folder can resolve to a read-only placeholder, and a wizard that pre-fills an unwritable
        /// path fails with "Access to the path ... is denied" the moment the user presses Next.
        ///
        /// The wizard does not pre-fill any path any more; it uses these as one click suggestions.
        /// </summary>
        public static string SuggestedContainerRoot()
        {
            List<string> candidates = SuggestedContainerRoots();
            for (int i = 0; i < candidates.Count; i++)
            {
                string candidate = candidates[i];
                if (IsUnsafeContainerTarget(candidate)) { continue; }
                if (CanWriteTo(candidate)) { return candidate; }
            }
            // Nothing in the candidate list worked; a temp folder always exists, so the wizard can
            // still show something and explain the problem when the user presses Next.
            return Path.Combine(Path.GetTempPath(), AppName);
        }

        /// <summary>Every place worth offering, best first.</summary>
        public static List<string> SuggestedContainerRoots()
        {
            List<string> candidates = new List<string>();

            // 1. Documents as Windows reports it (usual case).
            AddCandidate(candidates, SafeSpecialFolder(Environment.SpecialFolder.MyDocuments), AppName);
            // 2. Documents as the user shell folder registry value reports it.  On redirected
            //    profiles the special folder can be a read-only placeholder while this value points
            //    at the real (OneDrive) folder.
            AddCandidate(candidates, DocumentsFromRegistry(), AppName);
            // 3. The user profile root and the per-user app data folder.
            AddCandidate(candidates, SafeSpecialFolder(Environment.SpecialFolder.UserProfile), AppName);
            AddCandidate(candidates, SafeSpecialFolder(Environment.SpecialFolder.LocalApplicationData), AppName);
            // 4. A short, obvious folder on the root of every fixed drive (D:\Learnest ...).
            List<string> drives = FixedDrives();
            for (int i = 0; i < drives.Count; i++)
            {
                AddCandidate(candidates, drives[i], AppName);
            }
            // 5. Always available fallback.
            AddCandidate(candidates, Path.GetTempPath(), AppName);
            return candidates;
        }

        /// <summary>Read the real Documents path from the shell folder registry value.</summary>
        private static string DocumentsFromRegistry()
        {
            try
            {
                object value = Microsoft.Win32.Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders",
                    "Personal", null);
                string text = value as string;
                if (string.IsNullOrEmpty(text)) { return null; }
                return Environment.ExpandEnvironmentVariables(text);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Roots of fixed (non removable, non network) drives.</summary>
        private static List<string> FixedDrives()
        {
            List<string> drives = new List<string>();
            try
            {
                System.IO.DriveInfo[] all = System.IO.DriveInfo.GetDrives();
                for (int i = 0; i < all.Length; i++)
                {
                    try
                    {
                        if (all[i].DriveType != System.IO.DriveType.Fixed) { continue; }
                        if (!all[i].IsReady) { continue; }
                        drives.Add(all[i].RootDirectory.FullName);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
            return drives;
        }

        private static string SafeSpecialFolder(Environment.SpecialFolder folder)
        {
            try { return Environment.GetFolderPath(folder); }
            catch { return null; }
        }

        private static void AddCandidate(List<string> candidates, string parent, string leaf)
        {
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) { return; }
            string path;
            try { path = Path.Combine(parent, leaf); }
            catch { return; }
            for (int i = 0; i < candidates.Count; i++)
            {
                if (string.Equals(candidates[i], path, StringComparison.OrdinalIgnoreCase)) { return; }
            }
            candidates.Add(path);
        }

        /// <summary>
        /// True when Learnest can create files inside <paramref name="folder"/>.  The folder and its
        /// missing parents are created when needed; a probe file is written and removed to be sure,
        /// because a directory can exist yet still be read-only.
        /// </summary>
        public static bool CanWriteTo(string folder, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(folder))
            {
                reason = "请选择一个文件夹。";
                return false;
            }

            string full;
            try { full = Path.GetFullPath(folder); }
            catch (Exception ex)
            {
                reason = "路径无效：" + ex.Message;
                return false;
            }

            try
            {
                if (File.Exists(full))
                {
                    reason = "该路径是一个文件，不是文件夹。";
                    return false;
                }
                Directory.CreateDirectory(full);

                string probe = Path.Combine(full, ".learnest-write-test");
                File.WriteAllText(probe, "probe", Learnest.Core.Utf8.Encoding);
                File.Delete(probe);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                reason = "没有写入权限：" + full + "\r\n（该文件夹可能是只读的，或位于受保护位置。）";
                return false;
            }
            catch (Exception ex)
            {
                reason = "无法使用该文件夹：" + ex.Message + "\r\n" + full;
                return false;
            }
        }

        /// <summary>Convenience overload for callers that only need a yes/no answer.</summary>
        public static bool CanWriteTo(string folder)
        {
            string ignored;
            return CanWriteTo(folder, out ignored);
        }

        /// <summary>True for folders that must never host a container.</summary>
        public static bool IsUnsafeContainerTarget(string folder)
        {
            if (string.IsNullOrEmpty(folder)) { return true; }
            try
            {
                string full = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (full.Length <= 3) { return true; }                       // "C:\"
                string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                if (!string.IsNullOrEmpty(windows) &&
                    full.StartsWith(Path.GetFullPath(windows).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (!string.IsNullOrEmpty(programFiles) &&
                    full.StartsWith(Path.GetFullPath(programFiles).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                return false;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>Folder that holds the executable, used to find bundled language packs.</summary>
        public static string ApplicationDirectory
        {
            get
            {
                try
                {
                    string location = System.Reflection.Assembly.GetEntryAssembly() != null
                        ? System.Reflection.Assembly.GetEntryAssembly().Location
                        : typeof(AppPaths).Assembly.Location;
                    string directory = Path.GetDirectoryName(location);
                    if (!string.IsNullOrEmpty(directory)) { return directory; }
                }
                catch
                {
                }
                return Directory.GetCurrentDirectory();
            }
        }

        /// <summary>A short, readable container name derived from the folder name.</summary>
        public static string ContainerNameFromPath(string folder)
        {
            try
            {
                DirectoryInfo info = new DirectoryInfo(folder);
                if (!string.IsNullOrEmpty(info.Name)) { return info.Name; }
            }
            catch
            {
            }
            return DefaultContainerFolderName;
        }

        /// <summary>Turn arbitrary text into something safe to use as a file name.</summary>
        public static string SafeFileName(string name, string fallback)
        {
            if (string.IsNullOrEmpty(name)) { return fallback; }
            char[] invalid = Path.GetInvalidFileNameChars();
            System.Text.StringBuilder sb = new System.Text.StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool bad = false;
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (invalid[j] == c) { bad = true; break; }
                }
                sb.Append(bad ? '_' : c);
            }
            string result = sb.ToString().Trim().Trim('.');
            if (result.Length == 0) { return fallback; }
            if (result.Length > 120) { result = result.Substring(0, 120); }
            return result;
        }

        /// <summary>Sanitise text for use inside an export file name (no path separators).</summary>
        public static string SafeExportSegment(string name, string fallback)
        {
            string safe = SafeFileName(name, fallback);
            safe = safe.Replace(' ', '_');
            safe = safe.Replace(':', '_').Replace('*', '_').Replace('?', '_');
            safe = safe.Replace('"', '_').Replace('<', '_').Replace('>', '_').Replace('|', '_');
            safe = safe.Replace('/', '_').Replace('\\', '_');
            return string.IsNullOrEmpty(safe) ? fallback : safe;
        }
    }
}
