using System;
using System.Collections.Generic;
using System.Globalization;

namespace Learnest.Services
{
    /// <summary>
    /// A language pack.  Packs are plain JSON files (see src\Lang\zh-CN.json) and can be swapped
    /// or added by the user, so Learnest can be localised without shipping a new build.
    /// </summary>
    public sealed class LanguagePack
    {
        public string Code = "";
        public string Name = "";
        public string EnglishName = "";
        public string Author = "";
        public string Version = "1.0.0";
        public bool RightToLeft;
        public string SourcePath = "";
        public bool BuiltIn;
        public Dictionary<string, string> Strings = new Dictionary<string, string>(StringComparer.Ordinal);

        public string DisplayName
        {
            get
            {
                if (string.IsNullOrEmpty(Name)) { return Code; }
                if (string.IsNullOrEmpty(EnglishName) || Name == EnglishName) { return Name; }
                return Name + " (" + EnglishName + ")";
            }
        }

        public static LanguagePack FromJson(Core.JsonValue root)
        {
            LanguagePack pack = new LanguagePack();
            pack.Code = root.Get("code").AsString("");
            pack.Name = root.Get("name").AsString("");
            pack.EnglishName = root.Get("englishName").AsString("");
            pack.Author = root.Get("author").AsString("");
            pack.Version = root.Get("version").AsString("1.0.0");
            pack.RightToLeft = root.Get("rtl").AsBool(false);
            Core.JsonValue strings = root.Get("strings");
            if (strings.IsObject)
            {
                foreach (KeyValuePair<string, Core.JsonValue> pair in strings.AsObject())
                {
                    pack.Strings[pair.Key] = pair.Value.AsString("");
                }
            }
            if (string.IsNullOrEmpty(pack.Code))
            {
                pack.Code = "und";
            }
            if (string.IsNullOrEmpty(pack.Name))
            {
                pack.Name = pack.Code;
            }
            return pack;
        }

        /// <summary>
        /// English and Simplified Chinese are compiled into the executable so the app always has a
        /// usable UI even if the user deletes every language file next to it.
        /// </summary>
        internal static LanguagePack[] BuiltInPacks()
        {
            LanguagePack[] packs = new LanguagePack[2];
            packs[0] = LoadEmbedded("Learnest.Lang.en.json");
            packs[1] = LoadEmbedded("Learnest.Lang.zh-CN.json");
            for (int i = 0; i < packs.Length; i++)
            {
                if (packs[i] == null)
                {
                    packs[i] = FallbackPack();
                }
            }
            return packs;
        }

        private static LanguagePack LoadEmbedded(string resourceName)
        {
            try
            {
                System.Reflection.Assembly assembly = typeof(LanguagePack).Assembly;
                string resolved = ResolveResourceName(assembly, resourceName);
                if (resolved == null) { return null; }
                using (System.IO.Stream stream = assembly.GetManifestResourceStream(resolved))
                {
                    if (stream == null) { return null; }
                    using (System.IO.StreamReader reader = new System.IO.StreamReader(stream, Core.Utf8.Encoding))
                    {
                        Core.JsonValue parsed;
                        if (!Core.JsonValue.TryParse(reader.ReadToEnd(), out parsed)) { return null; }
                        return FromJson(parsed);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// The embedded resource name depends on the toolchain (the SDK keeps the Lang folder in
        /// the name, the in-box framework compiler flattens it) so match on the file name.
        /// </summary>
        private static string ResolveResourceName(System.Reflection.Assembly assembly, string expected)
        {
            string[] names;
            try { names = assembly.GetManifestResourceNames(); }
            catch { return null; }

            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], expected, StringComparison.OrdinalIgnoreCase)) { return names[i]; }
            }
            string suffix = expected.Substring(expected.LastIndexOf('.', expected.Length - 7) + 1);
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) { return names[i]; }
            }
            return null;
        }

        public static LanguagePack FromFile(string path)
        {
            try
            {
                Core.JsonValue parsed;
                if (!Core.JsonValue.TryParse(Core.Utf8.ReadAllText(path), out parsed)) { return null; }
                LanguagePack pack = FromJson(parsed);
                pack.SourcePath = path;
                return pack;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Minimal safety net so the UI always has labels.</summary>
        private static LanguagePack FallbackPack()
        {
            LanguagePack pack = new LanguagePack();
            pack.Code = "en";
            pack.Name = "English";
            pack.EnglishName = "English";
            pack.BuiltIn = true;
            pack.Strings["app.name"] = AppPaths.AppName;
            pack.Strings["app.tagline"] = AppPaths.Tagline;
            return pack;
        }

        /// <summary>System language code, e.g. "zh-CN" or "en-US", falling back to "en".</summary>
        public static string SystemLanguageCode()
        {
            try
            {
                string name = CultureInfo.CurrentUICulture.Name;
                if (string.IsNullOrEmpty(name)) { name = CultureInfo.CurrentCulture.Name; }
                if (string.IsNullOrEmpty(name)) { return "en"; }
                return name;
            }
            catch
            {
                return "en";
            }
        }

        /// <summary>Language tag without the region, e.g. "zh" from "zh-CN".</summary>
        public static string PrimarySubtag(string code)
        {
            if (string.IsNullOrEmpty(code)) { return ""; }
            int dash = code.IndexOf('-');
            return dash > 0 ? code.Substring(0, dash) : code;
        }
    }
}
