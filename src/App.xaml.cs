using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Learnest.Data;
using Learnest.Services;
using Learnest.Theming;
using Learnest.Views;

namespace Learnest
{
    public partial class App : Application
    {
        /// <summary>The main window, kept so the widget and wizard can talk to the shell.</summary>
        public static MainWindow AppWindow { get; private set; }

        public App()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Program.Trace("OnStartup begin");

            try
            {
                // 1. Appearance first: the setup wizard and every window read theme resources, and the
                //    machine scoped appsettings.json provides a fallback before a container is open.
                Program.Trace("step 1: ThemeManager.ApplyFromSettings");
                ThemeManager.ApplyFromSettings();
                Program.Trace("step 1 done (dark=" + ThemeManager.IsDark + ", palette=" + (ThemeManager.Palette != null) + ")");

                Program.Trace("step 2: StartSystemWatcher");
                ThemeManager.StartSystemWatcher();
                Program.Trace("step 2 done");

                // 2. Container: remembered path, otherwise the first-run wizard.
                Program.Trace("step 3: ResolveContainer");
                if (!ResolveContainer())
                {
                    Program.Trace("no container chosen, shutting down");
                    Shutdown();
                    return;
                }
                Program.Trace("step 3 done, container=" + AppState.ContainerRoot);

                // 3. Language from the container (falls back to the system language).
                AppSettings settings = AppState.SettingsOrNull;
                Program.Trace("step 4: Loc.Apply('" + (settings != null ? settings.Language : "") + "')");
                Loc.Apply(settings != null ? settings.Language : "");
                Program.Trace("step 4 done, pack=" + (Loc.Current != null ? Loc.Current.Code : "(null)"));
                if (settings != null)
                {
                    Program.Trace("step 5: ThemeManager.Apply(" + settings.Theme + ", " + settings.AccentColor + ")");
                    ThemeManager.Apply(settings.Theme, settings.AccentColor, false);
                    Program.Trace("step 5 done");
                }

                Program.Trace("step 6: new MainWindow()");
                AppWindow = new MainWindow();
                Program.Trace("step 6 done");
                MainWindow = AppWindow;
                Program.Trace("step 7: Show()");
                AppWindow.Show();
                Program.Trace("step 7 done - window visible");
            }
            catch (Exception ex)
            {
                // Start-up failures must produce a readable report instead of a bare dispatcher
                // exception, and the trace file path must be included so it is easy to find.
                Program.Trace("FATAL in OnStartup: " + Program.Describe(ex));
                LogCrash(ex, "Startup");
                MessageBox.Show(
                    Loc.T("error.generic") + "\r\n\r\n" + Program.Describe(ex) + "\r\n\r\n" +
                    "详细日志 / trace file:\r\n" + Program.StartupTracePath,
                    AppPaths.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        /// <summary>
        /// Try the remembered container, then the recent list, then ask the user.  Returns false when
        /// the user closed the wizard without choosing (the app should exit).
        /// </summary>
        private bool ResolveContainer()
        {
            // --container <path> opens a specific container without touching the remembered path
            // (used for automation/smoke tests and for "open with" style launches).
            string forced = ContainerOverride();
            if (!string.IsNullOrEmpty(forced))
            {
                if (!Directory.Exists(forced) || !ContainerLayout.IsContainer(forced))
                {
                    MessageBox.Show(
                        Loc.T("container.not_a_container") + "\r\n" + forced,
                        AppPaths.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                ContainerSession forcedSession = ContainerSession.Open(forced);
                AppState.AttachContainer(forcedSession, false);
                return true;
            }

            AppConfig config = AppState.Config;

            string candidate = config.LastContainer;
            if (!string.IsNullOrEmpty(candidate) && Directory.Exists(candidate) && ContainerLayout.IsContainer(candidate))
            {
                try
                {
                    ContainerSession session = ContainerSession.Open(candidate);
                    AppState.AttachContainer(session, true);
                    return true;
                }
                catch (Exception ex)
                {
                    LogCrash(ex, "OpenLastContainer");
                    MessageBox.Show(
                        Loc.T("error.container_missing") + "\r\n" + candidate + "\r\n\r\n" + ex.Message,
                        AppPaths.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            List<string> recents = config.RecentContainers;
            for (int i = 0; i < recents.Count; i++)
            {
                string path = recents[i];
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path) || !ContainerLayout.IsContainer(path)) { continue; }
                if (string.Equals(path, candidate, StringComparison.OrdinalIgnoreCase)) { continue; }
                try
                {
                    ContainerSession session = ContainerSession.Open(path);
                    AppState.AttachContainer(session, true);
                    return true;
                }
                catch (Exception ex)
                {
                    LogCrash(ex, "OpenRecentContainer");
                }
            }

            ContainerWizard wizard = new ContainerWizard(false);
            bool? result = wizard.ShowDialog();
            return result == true && !string.IsNullOrEmpty(wizard.ResultPath);
        }

        /// <summary>Read a "container=&lt;path&gt;" style argument from the environment or command line.</summary>
        private static string ContainerOverride()
        {
            try
            {
                string fromEnvironment = Environment.GetEnvironmentVariable("LEARNEST_CONTAINER");
                if (!string.IsNullOrEmpty(fromEnvironment)) { return fromEnvironment; }

                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length; i++)
                {
                    if (string.Equals(args[i], "--container", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        return args[i + 1];
                    }
                    if (args[i].StartsWith("--container=", StringComparison.OrdinalIgnoreCase))
                    {
                        return args[i].Substring("--container=".Length);
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                AppState.SaveConfig();
                if (AppState.SessionOrNull != null)
                {
                    AppState.SessionOrNull.Flush();
                    AppState.SessionOrNull.Dispose();
                }
            }
            catch
            {
            }
            base.OnExit(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // The full stack must reach the trace file: a dialog alone is not enough to find a line.
            Program.Trace("DISPATCHER EXCEPTION: " + Program.Describe(e.Exception));
            LogCrash(e.Exception, "Dispatcher");
            MessageBox.Show(
                "Learnest 遇到一个内部错误，但你的数据已经安全保存。\r\n\r\n" + Describe(e.Exception) +
                "\r\n\r\n" + Program.Describe(e.Exception) +
                "\r\n\r\n详细日志 / trace file:\r\n" + Program.StartupTracePath,
                AppPaths.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        internal static string Describe(Exception ex)
        {
            if (ex == null) { return "(unknown)"; }
            return ex.GetType().Name + ": " + ex.Message;
        }

        /// <summary>Crash log lives inside the container when one is open, else in %LocalAppData%.</summary>
        internal static void LogCrash(Exception ex, string source)
        {
            try
            {
                string directory = LogDirectory();
                Directory.CreateDirectory(directory);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("----- " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + source + "] -----");
                sb.AppendLine(ex == null ? "(null exception)" : ex.ToString());
                File.AppendAllText(Path.Combine(directory, "learnest.log"), sb.ToString(), new UTF8Encoding(false));
            }
            catch
            {
                // Logging must never take the application down.
            }
        }

        internal static string LogDirectory()
        {
            try
            {
                if (AppState.SessionOrNull != null)
                {
                    return Path.Combine(AppState.SessionOrNull.Root, ContainerLayout.LogsFolder);
                }
            }
            catch
            {
            }
            return AppConfig.Directory;
        }
    }
}
