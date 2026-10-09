using System;
using System.Collections.Generic;
using System.IO;
using Learnest.Core;

namespace Learnest.Services
{
    /// <summary>
    /// Runtime localisation.  Look-ups fall back in this order:
    ///   selected pack -> same-language family -> English -> the key itself,
    /// and the built-in packs live inside the executable so the UI can always be rendered.
    /// </summary>
    public static class Loc
    {
        private static readonly object Sync = new object();
        private static LanguagePack _current;
        private static List<LanguagePack> _packs;

        public static event EventHandler Changed;

        public static LanguagePack Current
        {
            get
            {
                EnsureLoaded();
                return _current;
            }
        }

        /// <summary>Code currently in use: an explicit pack code, or "system".</summary>
        public static string SelectedCode { get; private set; }

        public static List<LanguagePack> Available
        {
            get
            {
                EnsureLoaded();
                return _packs;
            }
        }

        private static void EnsureLoaded()
        {
            lock (Sync)
            {
                if (_packs != null) { return; }
                _packs = new List<LanguagePack>();
                LanguagePack[] builtIn = LanguagePack.BuiltInPacks();
                for (int i = 0; i < builtIn.Length; i++) { _packs.Add(builtIn[i]); }
                ScanExternalPacks(_packs);
                _current = PickPack(null);
                SelectedCode = "system";
            }
        }

        /// <summary>Language packs may live beside the executable or inside the container.</summary>
        private static void ScanExternalPacks(List<LanguagePack> packs)
        {
            List<string> folders = new List<string>();
            try { folders.Add(Path.Combine(AppPaths.ApplicationDirectory, "Lang")); } catch { }
            try { folders.Add(AppPaths.ApplicationDirectory); } catch { }
            try
            {
                if (AppState.SessionOrNull != null)
                {
                    folders.Add(Path.Combine(AppState.SessionOrNull.Root, Data.ContainerLayout.LanguageFolder));
                    folders.Add(AppState.SessionOrNull.Root);
                }
            }
            catch
            {
            }

            for (int f = 0; f < folders.Count; f++)
            {
                string folder = folders[f];
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) { continue; }
                string[] files;
                try { files = Directory.GetFiles(folder, "*.json"); }
                catch { continue; }
                for (int i = 0; i < files.Length; i++)
                {
                    LanguagePack pack = LanguagePack.FromFile(files[i]);
                    if (pack == null || string.IsNullOrEmpty(pack.Code)) { continue; }
                    if (pack.Strings.Count < 20) { continue; }   // ignore unrelated json files
                    ReplacePack(packs, pack);
                }
            }
        }

        private static void ReplacePack(List<LanguagePack> packs, LanguagePack pack)
        {
            for (int i = 0; i < packs.Count; i++)
            {
                if (string.Equals(packs[i].Code, pack.Code, StringComparison.OrdinalIgnoreCase))
                {
                    bool wasBuiltIn = packs[i].BuiltIn;
                    pack.BuiltIn = wasBuiltIn;
                    packs[i] = pack;
                    return;
                }
            }
            packs.Add(pack);
        }

        /// <summary>Re-scan the language folders (after importing a pack).</summary>
        public static void Rescan()
        {
            lock (Sync)
            {
                _packs = null;
                _current = null;
            }
            EnsureLoaded();
            Apply(SelectedCode);
        }

        /// <summary>Select a language: "system" or a pack code.  Returns true when it resolved.</summary>
        public static bool Apply(string code)
        {
            EnsureLoaded();
            LanguagePack pack = PickPack(code);
            SelectedCode = string.IsNullOrEmpty(code) ? "system" : code;
            bool changed = !ReferenceEquals(pack, _current) ||
                           (_current != null && !string.Equals(pack.Code, _current.Code, StringComparison.OrdinalIgnoreCase));
            _current = pack;
            if (changed)
            {
                EventHandler handler = Changed;
                if (handler != null) { handler(null, EventArgs.Empty); }
            }
            return pack != null;
        }

        /// <summary>Resolve the pack for a preference value, honouring the system language.</summary>
        public static LanguagePack PickPack(string preference)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(preference) || preference == "system")
            {
                return MatchLanguage(LanguagePack.SystemLanguageCode());
            }
            for (int i = 0; i < _packs.Count; i++)
            {
                if (string.Equals(_packs[i].Code, preference, StringComparison.OrdinalIgnoreCase)) { return _packs[i]; }
            }
            return MatchLanguage(preference);
        }

        private static LanguagePack MatchLanguage(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode)) { languageCode = "en"; }
            for (int i = 0; i < _packs.Count; i++)
            {
                if (string.Equals(_packs[i].Code, languageCode, StringComparison.OrdinalIgnoreCase)) { return _packs[i]; }
            }
            // zh-Hans / zh-TW / en-GB should land on a pack of the same family.
            string family = LanguagePack.PrimarySubtag(languageCode);
            for (int i = 0; i < _packs.Count; i++)
            {
                if (string.Equals(LanguagePack.PrimarySubtag(_packs[i].Code), family, StringComparison.OrdinalIgnoreCase))
                {
                    return _packs[i];
                }
            }
            for (int i = 0; i < _packs.Count; i++)
            {
                if (string.Equals(_packs[i].Code, "en", StringComparison.OrdinalIgnoreCase)) { return _packs[i]; }
            }
            return _packs.Count > 0 ? _packs[0] : null;
        }

        // ---------------------------------------------------------------- lookup
        /// <summary>Translate a key; returns the key itself when it is missing everywhere.</summary>
        public static string T(string key)
        {
            if (string.IsNullOrEmpty(key)) { return ""; }
            LanguagePack pack = Current;
            string value;
            if (pack != null && pack.Strings.TryGetValue(key, out value) && !string.IsNullOrEmpty(value)) { return value; }
            string english = EnglishValue(key);
            if (english != null) { return english; }
            return key;
        }

        public static string T(string key, params object[] args)
        {
            string format = T(key);
            if (args == null || args.Length == 0) { return format; }
            try { return string.Format(format, args); }
            catch (FormatException) { return format; }
        }

        private static string EnglishValue(string key)
        {
            EnsureLoaded();
            for (int i = 0; i < _packs.Count; i++)
            {
                if (!string.Equals(_packs[i].Code, "en", StringComparison.OrdinalIgnoreCase)) { continue; }
                string value;
                if (_packs[i].Strings.TryGetValue(key, out value) && !string.IsNullOrEmpty(value)) { return value; }
            }
            return null;
        }

        // ---------------------------------------------------------------- importing
        /// <summary>
        /// Copy a language pack into the container (or beside the executable when no container is
        /// open yet) and make it available immediately.
        /// </summary>
        public static LanguagePack Import(string sourcePath, out string error)
        {
            error = null;
            try
            {
                LanguagePack parsedPack = LanguagePack.FromFile(sourcePath);
                if (parsedPack == null || parsedPack.Strings.Count < 20)
                {
                    error = "语言包格式不正确或内容过少。";
                    return null;
                }

                string folder;
                if (AppState.SessionOrNull != null)
                {
                    folder = Path.Combine(AppState.SessionOrNull.Root, Data.ContainerLayout.LanguageFolder);
                }
                else
                {
                    folder = Path.Combine(AppPaths.ApplicationDirectory, "Lang");
                }
                Directory.CreateDirectory(folder);
                string target = Path.Combine(folder, AppPaths.SafeFileName(parsedPack.Code, "language") + ".json");
                File.Copy(sourcePath, target, true);
                parsedPack.SourcePath = target;

                EnsureLoaded();
                ReplacePack(_packs, parsedPack);
                return parsedPack;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        /// <summary>Path of the folder users can drop extra language packs into.</summary>
        public static string LanguageFolder()
        {
            if (AppState.SessionOrNull != null)
            {
                return Path.Combine(AppState.SessionOrNull.Root, Data.ContainerLayout.LanguageFolder);
            }
            return Path.Combine(AppPaths.ApplicationDirectory, "Lang");
        }
    }
}
