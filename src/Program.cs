using System;
using System.Windows;

namespace Learnest
{
    /// <summary>
    /// Application entry point.  The WPF generated entry point is disabled so that Learnest can
    /// decide between the interactive GUI, the headless self test and its maintenance commands
    /// before any window is created.
    /// </summary>
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            bool selfTest = false;
            string scenario = null;
            string createContainer = null;
            string containerName = null;
            bool langCheck = false;
            bool uiCheck = false;
            bool showCheck = false;
            bool dialogCheck = false;
            bool folderTest = false;
            bool reminderCheck = false;

            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i];
                    if (string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(a, "/selftest", StringComparison.OrdinalIgnoreCase))
                    {
                        selfTest = true;
                    }
                    else if (string.Equals(a, "--scenario", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        scenario = args[++i];
                    }
                    else if (string.Equals(a, "--container", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        // Passed through the environment so App can pick it up without a second parser.
                        Environment.SetEnvironmentVariable("LEARNEST_CONTAINER", args[++i]);
                    }
                    else if (string.Equals(a, "--trace", StringComparison.OrdinalIgnoreCase))
                    {
                        VerboseTrace = true;
                    }
                    else if (string.Equals(a, "--langcheck", StringComparison.OrdinalIgnoreCase))
                    {
                        langCheck = true;
                    }
                    else if (string.Equals(a, "--uicheck", StringComparison.OrdinalIgnoreCase))
                    {
                        uiCheck = true;
                    }
                    else if (string.Equals(a, "--showcheck", StringComparison.OrdinalIgnoreCase))
                    {
                        showCheck = true;
                    }
                    else if (string.Equals(a, "--dialogcheck", StringComparison.OrdinalIgnoreCase))
                    {
                        dialogCheck = true;
                    }
                    else if (string.Equals(a, "--foldertest", StringComparison.OrdinalIgnoreCase))
                    {
                        folderTest = true;
                    }
                    else if (string.Equals(a, "--remindercheck", StringComparison.OrdinalIgnoreCase))
                    {
                        reminderCheck = true;
                    }
                    else if (string.Equals(a, "--create-container", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        createContainer = args[++i];
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                        {
                            containerName = args[++i];
                        }
                    }
                }
            }

            Trace("Main enter");

            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                Trace("AppDomain unhandled: " + Describe(e.ExceptionObject as Exception));
                App.LogCrash(e.ExceptionObject as Exception, "AppDomain");
            };

            if (selfTest)
            {
                return SelfTest.Run(scenario);
            }

            if (!string.IsNullOrEmpty(createContainer))
            {
                return SelfTest.CreateContainer(createContainer, containerName);
            }

            if (langCheck)
            {
                return SelfTest.LanguageCheck();
            }

            if (uiCheck)
            {
                return SelfTest.UiCheck();
            }

            if (showCheck)
            {
                return SelfTest.WindowShowCheck();
            }

            if (dialogCheck)
            {
                return SelfTest.DialogCheck();
            }

            if (folderTest)
            {
                return SelfTest.FolderTest();
            }

            if (reminderCheck)
            {
                return SelfTest.ReminderCheck();
            }

            // The GUI path is wrapped stage by stage: a crash before the first window appears used to
            // be reported only through a dialog, which made it impossible to find the failing line.
            App app = null;
            try
            {
                Trace("constructing App");
                app = new App();
                Trace("App constructed");
                app.InitializeComponent();
                Trace("App.xaml loaded (theme dictionaries merged)");
            }
            catch (Exception ex)
            {
                Trace("FATAL while creating the application: " + Describe(ex));
                App.LogCrash(ex, "Program.App");
                MessageBox.Show(
                    AppPaths.AppName + " 启动失败 / failed to start\r\n\r\n" + Describe(ex) +
                    "\r\n\r\n" + StartupTracePath,
                    AppPaths.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                return 10;
            }

            try
            {
                Trace("entering the message loop");
                app.Run();
                Trace("message loop exited normally");
            }
            catch (Exception ex)
            {
                Trace("FATAL in the message loop: " + Describe(ex));
                App.LogCrash(ex, "Program.Run");
                return 11;
            }
            return 0;
        }

        internal static bool VerboseTrace;

        private static string _startupTracePath;

        /// <summary>
        /// Start-up trace file.  Written next to the executable and, failing that, in the temp folder,
        /// so a start-up failure always leaves a readable artefact behind.
        /// </summary>
        internal static string StartupTracePath
        {
            get
            {
                if (_startupTracePath != null) { return _startupTracePath; }
                string candidate = null;
                try
                {
                    candidate = System.IO.Path.Combine(AppPaths.ApplicationDirectory, "learnest-startup.log");
                    System.IO.File.AppendAllText(candidate, "", System.Text.Encoding.UTF8);
                }
                catch
                {
                    try { candidate = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "learnest-startup.log"); }
                    catch { candidate = "learnest-startup.log"; }
                }
                _startupTracePath = candidate;
                return _startupTracePath;
            }
        }

        internal static void Trace(string message)
        {
            try
            {
                string line = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message + Environment.NewLine;
                System.IO.File.AppendAllText(StartupTracePath, line, new System.Text.UTF8Encoding(false));
            }
            catch
            {
            }
        }

        /// <summary>Full type, message and stack trace, so a report is actionable on its own.</summary>
        internal static string Describe(Exception ex)
        {
            if (ex == null) { return "(null exception)"; }
            return ex.GetType().FullName + ": " + ex.Message + Environment.NewLine + ex.StackTrace;
        }
    }
}
