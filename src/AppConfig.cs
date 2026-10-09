using System;
using System.Collections.Generic;
using System.IO;
using Learnest.Core;

namespace Learnest
{
    /// <summary>
    /// Application level configuration.  This is the *only* thing Learnest writes outside the
    /// user's container folder: it lives in %LocalAppData%\Learnest\appsettings.json and records
    /// nothing but the last container path, the recent container list and a couple of
    /// machine-scoped UI preferences.  All business data lives inside the container.
    /// </summary>
    public sealed class AppConfig
    {
        public const string FileName = "appsettings.json";

        public string LastContainer = "";
        public List<string> RecentContainers = new List<string>();
        public string LanguageOverride = "";      // machine wide fallback; container wins
        public string ThemeOverride = "system";   // system | light | dark
        public string AccentFallback = "";        // used only before a container is open
        public bool ShowSetupWizard = true;
        public string LastUpdateCheck = "";
        public bool PortableMode;
        public bool FirstRunCompleted;

        public static string Directory
        {
            get
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(local)) { local = Path.GetTempPath(); }
                return Path.Combine(local, AppPaths.AppName);
            }
        }

        public static string FilePath { get { return Path.Combine(Directory, FileName); } }

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    JsonValue parsed;
                    if (JsonValue.TryParse(Utf8.ReadAllText(FilePath), out parsed) && parsed.IsObject)
                    {
                        return FromJson(parsed);
                    }
                }
            }
            catch
            {
                // A broken app config must never stop Learnest from starting.
            }
            return new AppConfig();
        }

        public void Save()
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                Utf8.WriteAllText(FilePath, ToJson().ToPrettyString());
            }
            catch
            {
            }
        }

        public JsonValue ToJson()
        {
            JsonObject o = new JsonObject();
            o.SetIfNotEmpty("lastContainer", LastContainer);
            JsonArray recents = new JsonArray();
            for (int i = 0; i < RecentContainers.Count; i++) { recents.Add(RecentContainers[i]); }
            o["recentContainers"] = JsonValue.Array(recents);
            o.SetIfNotEmpty("languageOverride", LanguageOverride);
            o["themeOverride"] = JsonValue.String(ThemeOverride);
            o.SetIfNotEmpty("accentFallback", AccentFallback);
            o["showSetupWizard"] = JsonValue.Bool(ShowSetupWizard);
            o["portableMode"] = JsonValue.Bool(PortableMode);
            o["firstRunCompleted"] = JsonValue.Bool(FirstRunCompleted);
            o.SetIfNotEmpty("lastUpdateCheck", LastUpdateCheck);
            o["app"] = JsonValue.String(AppPaths.AppName);
            o["format"] = JsonValue.Number(1);
            return JsonValue.Object(o);
        }

        public static AppConfig FromJson(JsonValue v)
        {
            AppConfig c = new AppConfig();
            c.LastContainer = v.Get("lastContainer").AsString("");
            c.RecentContainers = v.Get("recentContainers").AsStringList();
            c.LanguageOverride = v.Get("languageOverride").AsString("");
            c.ThemeOverride = v.Get("themeOverride").AsString("system");
            c.AccentFallback = v.Get("accentFallback").AsString("");
            c.ShowSetupWizard = v.Get("showSetupWizard").AsBool(true);
            c.PortableMode = v.Get("portableMode").AsBool(false);
            c.FirstRunCompleted = v.Get("firstRunCompleted").AsBool(false);
            c.LastUpdateCheck = v.Get("lastUpdateCheck").AsString("");
            return c;
        }

        public void RememberContainer(string path)
        {
            if (string.IsNullOrEmpty(path)) { return; }
            LastContainer = path;
            FirstRunCompleted = true;
            RecentContainers.RemoveAll(delegate(string p) { return string.Equals(p, path, StringComparison.OrdinalIgnoreCase); });
            RecentContainers.Insert(0, path);
            while (RecentContainers.Count > 8) { RecentContainers.RemoveAt(RecentContainers.Count - 1); }
        }
    }
}
