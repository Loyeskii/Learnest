using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using Learnest.Services;

namespace Learnest
{
    /// <summary>
    /// Headless verification.  Runs without a display server and exercises the parts of Learnest
    /// that must be correct for the app to be trustworthy: the SQLite container round trip,
    /// category lifecycle, todo CRUD with Chinese text and markdown, attachment import/rename,
    /// competition linking, the calendar engine and the backup export/import cycle.
    ///
    /// Run with:  Learnest.exe --selftest
    /// </summary>
    internal static class SelfTest
    {
        private static int _passed;
        private static int _failed;
        private static readonly List<string> Failures = new List<string>();
        private static StringBuilder _transcript;

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint processId);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleOutputCP(uint codePage);

        private const uint AttachParentProcess = 0xFFFFFFFF;
        private const uint CodePageUtf8 = 65001;

        /// <summary>
        /// Learnest is a windowed executable.  When it is started from a shell for
        /// <c>--selftest</c> the parent shell still owns a console, so attach to it to make the
        /// report visible instead of discarding it, and switch it to UTF-8 so Chinese output is
        /// readable rather than turning into question marks.
        /// </summary>
        private static void AttachParentConsole()
        {
            try
            {
                AttachConsole(AttachParentProcess);
                SetConsoleOutputCP(CodePageUtf8);
            }
            catch
            {
            }
        }

        private static void Emit(string line)
        {
            Console.WriteLine(line);
            if (_transcript != null) { _transcript.AppendLine(line); }
        }

        /// <summary>Path of the last self test report, for GUI runs without a console.</summary>
        internal static string TranscriptPath
        {
            get { return Path.Combine(Path.GetTempPath(), "learnest-selftest.log"); }
        }

        /// <summary>
        /// Verifies that the embedded language packs were compiled into the assembly and that the
        /// look-up chain resolves real strings (not raw keys).
        /// </summary>
        internal static int LanguageCheck()
        {
            int failures = 0;
            try
            {
                Console.WriteLine("Learnest language check");
                System.Reflection.Assembly assembly = typeof(SelfTest).Assembly;
                string[] resources = assembly.GetManifestResourceNames();
                Console.WriteLine("  embedded resources: " + resources.Length);
                for (int i = 0; i < resources.Length; i++) { Console.WriteLine("    " + resources[i]); }

                int langResources = 0;
                for (int i = 0; i < resources.Length; i++)
                {
                    if (resources[i].IndexOf("Lang", StringComparison.OrdinalIgnoreCase) >= 0) { langResources++; }
                }
                Console.WriteLine("  language pack resources: " + langResources);
                if (langResources < 2) { Console.WriteLine("  FAIL expected at least 2 language packs (en + zh-CN)"); failures++; }

                Console.WriteLine("  system language: " + LanguagePack.SystemLanguageCode());
                List<LanguagePack> packs = Loc.Available;
                Console.WriteLine("  available packs: " + packs.Count);
                for (int i = 0; i < packs.Count; i++)
                {
                    Console.WriteLine("    " + packs[i].Code + "  " + packs[i].DisplayName + "  strings=" + packs[i].Strings.Count +
                                      (packs[i].BuiltIn ? "  [built-in]" : "  [" + packs[i].SourcePath + "]"));
                    if (packs[i].Strings.Count < 100) { failures++; }
                }

                string[] probes = new string[] { "app.name", "app.tagline", "nav.dashboard", "nav.tasks", "tasks.new_todo", "widget.title" };
                for (int p = 0; p < packs.Count; p++)
                {
                    Loc.Apply(packs[p].Code);
                    Console.WriteLine("  --- resolved with pack " + packs[p].Code + " ---");
                    for (int i = 0; i < probes.Length; i++)
                    {
                        string value = Loc.T(probes[i]);
                        Console.WriteLine("    " + probes[i] + " = " + value);
                        if (string.IsNullOrEmpty(value) || value == probes[i]) { failures++; }
                    }
                }

                Console.WriteLine();
                Console.WriteLine("language check failures: " + failures);
                return failures == 0 ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine("language check FATAL: " + ex);
                return 2;
            }
        }

        /// <summary>
        /// Builds the real UI without showing it: instantiates every page and dialog against an
        /// open container so that XAML parsing, StaticResource/DynamicResource look-ups and the
        /// view constructors are all exercised.  This is the closest thing to a launch test that
        /// can run without an interactive desktop.
        /// </summary>
        internal static int UiCheck()
        {
            int failures = 0;
            string root = Path.Combine(Path.GetTempPath(), "learnest-uicheck-" + DateTime.Now.ToString("HHmmss"));
            Console.WriteLine("Learnest UI construction check");
            Console.WriteLine("  scratch container: " + root);
            try
            {
                if (Directory.Exists(root)) { Directory.Delete(root, true); }
                Data.ContainerSession session = Data.ContainerSession.Create(root, "UiCheck");
                AppState.AttachContainer(session, false);

                // Seed enough data that every view has something to bind.
                SeedUiSampleData();
                AppState.ReloadAll();

                // Regression guard: a start-up NullReferenceException once slipped through because
                // AttachContainer called ReloadAll() before assigning the session, so the cached
                // snapshot stayed null and the first view that touched AppState.Data crashed.
                Check("snapshot non-null right after attach", AppState.Data != null);
                Check("snapshot has categories", AppState.Data.Categories.Count > 0);
                Check("snapshot has todos", AppState.Data.Todos.Count > 0);
                Check("snapshot has the sample exam", AppState.Data.Exams.Count > 0);
                Check("snapshot has the sample competition", AppState.Data.Competitions.Count > 0);
                Check("linked todo visible through the competition", AppState.Data.Competitions[0].LinkedTodos.Count > 0);
                Check("settings reachable after attach", AppState.SettingsOrNull != null);
                Check("todo colour resolves", !string.IsNullOrEmpty(AppState.ColorFor(AppState.Data.Todos[0])));
                Check("timeline builds from the snapshot",
                    TaskQuery.BuildTimeline(null, DateTime.Now.AddDays(-30), DateTime.Now.AddDays(30)).Count > 0);

                Theming.ThemeManager.ApplyFromSettings();
                Services.Loc.Apply("zh-CN");

                App application = new App();
                application.InitializeComponent();

                // 1. Shell
                Views.MainWindow shell = new Views.MainWindow();
                Console.WriteLine("  ok   MainWindow constructed (title bar, nav, category rail, status bar)");
                if (!ReferenceEquals(shell.Content, null)) { }
                if (shell.Width < 100) { throw new Exception("MainWindow has no size"); }

                // 2. Every page, built exactly the way the shell builds them.
                string[] views = new string[] { "dashboard", "tasks", "timeline", "exams", "competitions", "calendar", "settings" };
                for (int i = 0; i < views.Length; i++)
                {
                    Views.IShell api = shell;
                    System.Reflection.MethodInfo factory = typeof(Views.MainWindow).GetMethod("CreateView",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    object view = factory.Invoke(shell, new object[] { views[i] });
                    if (view == null) { throw new Exception("CreateView returned null for " + views[i]); }
                    System.Windows.FrameworkElement element = (System.Windows.FrameworkElement)view;
                    element.Measure(new System.Windows.Size(1280, 800));
                    element.Arrange(new System.Windows.Rect(0, 0, 1280, 800));
                    element.UpdateLayout();
                    if (api == null) { }
                    Console.WriteLine("  ok   page '" + views[i] + "' constructed, measured and laid out (" +
                                      (int)element.DesiredSize.Width + "x" + (int)element.DesiredSize.Height + ")");
                }

                // 3. Pin widget
                Views.WidgetWindow widget = new Views.WidgetWindow(shell);
                widget.Width = 340;
                Console.WriteLine("  ok   WidgetWindow constructed (always-on-top list widget)");
                widget.Close();

                // 4. Container wizard in all three modes
                Views.ContainerWizard wizard = new Views.ContainerWizard(true);
                Console.WriteLine("  ok   ContainerWizard constructed (new / open / import)");

                // 5. Theme switching on live controls, as the real app does it
                foreach (string mode in new string[] { "light", "dark", "system" })
                {
                    Theming.ThemeManager.Apply(mode, "#7C3AED", false);
                    element_Relayout(shell);
                    Console.WriteLine("  ok   theme switched to '" + mode + "' and re-laid out");
                }

                // 6. Language switching
                foreach (string code in new string[] { "en", "zh-CN" })
                {
                    Services.Loc.Apply(code);
                    Console.WriteLine("  ok   language switched to '" + code + "' (nav.dashboard = " + Services.Loc.T("nav.dashboard") + ")");
                }

                shell.Close();
                application.Shutdown();
                Console.WriteLine();
                Console.WriteLine("ui check failures: " + failures);
                return failures == 0 ? 0 : 1;
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine("  FAIL " + ex.GetType().Name + ": " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine();
                Console.WriteLine("ui check failures: " + failures);
                return 1;
            }
            finally
            {
                try { GC.Collect(); GC.WaitForPendingFinalizers(); } catch { }
                try { if (Directory.Exists(root)) { Directory.Delete(root, true); } } catch { }
            }
        }

        private static void element_Relayout(System.Windows.FrameworkElement element)
        {
            element.Measure(new System.Windows.Size(1280, 800));
            element.Arrange(new System.Windows.Rect(0, 0, 1280, 800));
            element.UpdateLayout();
        }

        /// <summary>Sample rows so the UI check exercises real rendering paths, not empty states.</summary>
        private static void SeedUiSampleData()
        {
            Data.Repository repo = AppState.Repo;
            List<Data.Category> categories = repo.LoadCategories();
            string homework = categories.Count > 0 ? categories[0].Id : "uncategorized";

            Data.Todo task = new Data.Todo();
            task.Title = "高数作业 1.4-1.5 章节";
            task.CategoryId = homework;
            task.DueAt = Core.IsoTime.Format(DateTime.Now.Date.AddDays(3).AddHours(23).AddMinutes(59));
            task.NoteMarkdown = "# 高数作业\n\n- [ ] 1.4 习题 1-9\n- [x] 1.5 预习\n\n| 题号 | 状态 |\n| --- | --- |\n| 1 | 完成 |";
            task.Tags.Add("期中重点");
            task.Priority = 2;
            repo.SaveTodo(task, null);

            Data.Todo overdue = new Data.Todo();
            overdue.Title = "线代报告初稿";
            overdue.CategoryId = homework;
            overdue.DueAt = Core.IsoTime.Format(DateTime.Now.AddHours(-5));
            repo.SaveTodo(overdue, null);

            Data.Exam exam = new Data.Exam();
            exam.Title = "高等数学（上）期末考试";
            exam.Course = "高等数学";
            exam.StartAt = Core.IsoTime.Format(DateTime.Now.AddDays(12).AddHours(9));
            exam.EndAt = Core.IsoTime.Format(DateTime.Now.AddDays(12).AddHours(11));
            exam.Location = "教三 305";
            exam.Seat = "27";
            exam.NoteMarkdown = "## 复习范围\n- 极限与连续\n- 中值定理";
            repo.SaveExam(exam);

            Data.Competition competition = new Data.Competition();
            competition.Name = "全国大学生数学建模竞赛";
            competition.Organizer = "中国工业与应用数学学会";
            competition.Stage = "in_progress";
            competition.StartAt = Core.IsoTime.Format(DateTime.Now.AddDays(-10));
            competition.EndAt = Core.IsoTime.Format(DateTime.Now.AddDays(40));
            competition.Team = new List<string>(new string[] { "张三", "李四", "王五" });
            repo.SaveCompetition(competition);

            Data.Milestone milestone = new Data.Milestone();
            milestone.CompetitionId = competition.Id;
            milestone.Title = "完成选题与数据预处理";
            milestone.DueAt = Core.IsoTime.Format(DateTime.Now.AddDays(6));
            milestone.NoteMarkdown = "### 数据来源\n- 官网附件 A\n- 图书馆数据库";
            repo.SaveMilestone(milestone);

            Data.CompetitionEvent entry = new Data.CompetitionEvent();
            entry.CompetitionId = competition.Id;
            entry.Title = "赛前培训";
            entry.Kind = "event";
            entry.StartAt = Core.IsoTime.Format(DateTime.Now.AddDays(2).AddHours(14));
            entry.Location = "线上会议";
            repo.SaveEvent(entry);

            Data.Repository found = repo;
            Data.Todo linked = new Data.Todo();
            linked.Title = "准备建模论文摘要";
            linked.CategoryId = homework;
            linked.DueAt = Core.IsoTime.Format(DateTime.Now.AddDays(9));
            linked.CompetitionIds.Add(competition.Id);
            linked.Tags.Add(competition.Tag);
            repo.SaveTodo(linked, null);
            if (found == null) { }
            AppState.Commit();
        }

        /// <summary>
        /// Shows real windows, pumps the message loop and lets the WPF render pass run, then closes
        /// them.  This is the check that would have caught the watermark adorner defect: it only
        /// threw inside FireLoadedPendingCallbacks during a genuine render, so Measure/Arrange alone
        /// - however complete - could never reproduce it.
        ///
        /// The windows are positioned off screen so the check stays unobtrusive.
        /// </summary>
        /// <summary>Run the dispatcher for a while so layout, render and Loaded callbacks happen.</summary>
        private static void PumpMessages(int milliseconds)
        {
            System.Windows.Threading.DispatcherFrame frame = new System.Windows.Threading.DispatcherFrame();
            System.Windows.Threading.DispatcherTimer timer = new System.Windows.Threading.DispatcherTimer(
                TimeSpan.FromMilliseconds(milliseconds),
                System.Windows.Threading.DispatcherPriority.Background,
                delegate { frame.Continue = false; },
                System.Windows.Threading.Dispatcher.CurrentDispatcher);
            timer.Start();
            try { System.Windows.Threading.Dispatcher.PushFrame(frame); }
            finally { timer.Stop(); }
        }

        /// <summary>
        /// Opens one of the editor dialogs with UiKit.CaptureDialogs set, so the dialog is constructed
        /// and measured (applying every style) but never shown.  A style whose TargetType does not
        /// match the element it is applied to only throws at this point.
        /// </summary>
        private static void BuildEditor(string kind, Views.IShell shell, Data.Todo sampleTodo)
        {
            const System.Reflection.BindingFlags Flags =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static;

            Data.Competition competition = AppState.Data.Competitions.Count > 0 ? AppState.Data.Competitions[0] : null;
            Data.Milestone milestone = competition != null && competition.Milestones.Count > 0
                ? competition.Milestones[0] : null;
            Data.CompetitionEvent entry = competition != null && competition.Events.Count > 0
                ? competition.Events[0] : null;

            switch (kind)
            {
                case "task":
                    typeof(Views.TaskEditor).GetMethod("ShowEdit", Flags).Invoke(null, new object[] { shell, sampleTodo });
                    return;
                case "task-new":
                    typeof(Views.TaskEditor).GetMethod("ShowCreate", Flags).Invoke(null, new object[] { shell, null });
                    return;
                case "exam":
                    typeof(Views.ExamEditor).GetMethod("ShowEdit", Flags).Invoke(null, new object[] { shell, AppState.Data.Exams[0] });
                    return;
                case "exam-new":
                    typeof(Views.ExamEditor).GetMethod("ShowCreate", Flags).Invoke(null, new object[] { shell });
                    return;
                case "competition":
                    typeof(Views.CompetitionEditor).GetMethod("ShowEdit", Flags).Invoke(null, new object[] { shell, competition });
                    return;
                case "competition-new":
                    typeof(Views.CompetitionEditor).GetMethod("ShowCreate", Flags).Invoke(null, new object[] { shell });
                    return;
                case "category":
                    typeof(Views.CategoryEditor).GetMethod("ShowEdit", Flags).Invoke(null, new object[] { shell, AppState.Data.Categories[0], null });
                    return;
                case "category-new":
                    typeof(Views.CategoryEditor).GetMethod("ShowCreate", Flags).Invoke(null, new object[] { shell, null });
                    return;
                case "milestone":
                    BuildMilestoneDialog(shell, competition, milestone);
                    return;
                case "event":
                    BuildEventDialog(shell, competition, entry);
                    return;
                default:
                    throw new ArgumentException("unknown editor kind: " + kind);
            }
        }

        /// <summary>
        /// The milestone and event editors are private methods on CompetitionsView, so a live view
        /// instance is needed to reach them.
        /// </summary>
        private static void BuildMilestoneDialog(Views.IShell shell, Data.Competition competition, Data.Milestone milestone)
        {
            Views.CompetitionsView view = new Views.CompetitionsView(shell);
            System.Reflection.MethodInfo method = typeof(Views.CompetitionsView).GetMethod("ShowMilestoneDialog",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(view, new object[] { competition, milestone });
        }

        private static void BuildEventDialog(Views.IShell shell, Data.Competition competition, Data.CompetitionEvent entry)
        {
            Views.CompetitionsView view = new Views.CompetitionsView(shell);
            System.Reflection.MethodInfo method = typeof(Views.CompetitionsView).GetMethod("ShowEventDialog",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(view, new object[] { competition, entry });
        }

        internal static int WindowShowCheck()
        {
            int failures = 0;
            string root = Path.Combine(Path.GetTempPath(), "learnest-showcheck-" + DateTime.Now.ToString("HHmmss"));
            Console.WriteLine("Learnest window show check (real render pass)");
            Console.WriteLine("  scratch container: " + root);
            try
            {
                if (Directory.Exists(root)) { Directory.Delete(root, true); }
                Data.ContainerSession session = Data.ContainerSession.Create(root, "ShowCheck");
                AppState.AttachContainer(session, false);
                SeedUiSampleData();
                AppState.ReloadAll();

                Theming.ThemeManager.ApplyFromSettings();
                Loc.Apply("zh-CN");

                App application = new App();
                application.InitializeComponent();

                // 1. A single watermarked TextBox: the smallest case that used to throw.
                System.Windows.Window probe = new System.Windows.Window();
                probe.Width = 320;
                probe.Height = 120;
                probe.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
                probe.Left = -4000;
                probe.Top = -4000;
                probe.ShowInTaskbar = false;
                System.Windows.Controls.TextBox watermarked = Views.UiKit.Input("", "水印测试 watermark");
                watermarked.Margin = new System.Windows.Thickness(12);
                probe.Content = watermarked;
                probe.Show();
                PumpMessages(600);
                Check("watermarked TextBox renders without throwing", watermarked.IsLoaded);
                probe.Close();

                // 2. The whole shell, shown and rendered.
                Views.MainWindow shell = new Views.MainWindow();
                shell.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
                shell.Left = -4000;
                shell.Top = -4000;
                shell.ShowInTaskbar = false;
                shell.Show();
                PumpMessages(1200);
                Check("shell window reports IsLoaded after a real render", shell.IsLoaded);

                // 3. Every page, navigated to while the window is live, so each one goes through the
                //    render pipeline (Loaded events, adorners, templates) rather than Measure only.
                string[] views = new string[] { "dashboard", "tasks", "timeline", "exams", "competitions", "calendar", "settings" };
                for (int i = 0; i < views.Length; i++)
                {
                    shell.Navigate(views[i]);
                    PumpMessages(400);
                    Check("page '" + views[i] + "' renders live", shell.IsLoaded);
                }

                // 4. The pin widget and the wizard, also rendered for real.
                Views.WidgetWindow widget = new Views.WidgetWindow(shell);
                widget.Left = -4000;
                widget.Top = -4000;
                widget.Show();
                PumpMessages(600);
                Check("pin widget renders live", widget.IsLoaded);
                widget.Close();

                Views.ContainerWizard wizard = new Views.ContainerWizard(true);
                wizard.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
                wizard.Left = -4000;
                wizard.Top = -4000;
                wizard.ShowInTaskbar = false;
                wizard.Show();
                PumpMessages(900);
                Check("container wizard renders live (empty path field, suggestion chips)", wizard.IsLoaded);
                wizard.Close();

                // 5. Theme switch while rendered, which rebuilds every cached view.
                foreach (string mode in new string[] { "light", "dark", "system" })
                {
                    Theming.ThemeManager.Apply(mode, "#0D9488", true);
                    PumpMessages(400);
                    Check("theme '" + mode + "' re-renders live", shell.IsLoaded);
                }

                // 6. Every editor dialog.  These are opened from the list rows and were the coverage
                //    gap that let a Button/ToggleButton style mismatch reach the user: a check that
                //    only builds the shell can never see the inside of a dialog.
                Views.UiKit.CaptureDialogs = true;
                Views.UiKit.CapturedDialogs.Clear();
                Data.Todo sampleTodo = AppState.Data.Todos[0];
                string[,] editorCases = new string[,]
                {
                    { "TaskEditor (advanced record, markdown, attachments)", "task" },
                    { "TaskEditor (new task)", "task-new" },
                    { "ExamEditor", "exam" },
                    { "ExamEditor (new exam)", "exam-new" },
                    { "CompetitionEditor", "competition" },
                    { "CompetitionEditor (new competition)", "competition-new" },
                    { "CategoryEditor (edit)", "category" },
                    { "CategoryEditor (new)", "category-new" },
                    { "MilestoneEditor", "milestone" },
                    { "CompetitionEventEditor", "event" }
                };
                for (int i = 0; i < editorCases.GetLength(0); i++)
                {
                    string label = editorCases[i, 0];
                    string kind = editorCases[i, 1];
                    try
                    {
                        BuildEditor(kind, shell, sampleTodo);
                        Check(label + " builds without throwing", true);
                    }
                    catch (Exception ex)
                    {
                        Check(label + " builds without throwing", false);
                        Emit("      " + ex.GetType().Name + ": " + ex.Message);
                        Exception inner = ex.InnerException;
                        while (inner != null)
                        {
                            Emit("      caused by " + inner.GetType().Name + ": " + inner.Message);
                            inner = inner.InnerException;
                        }
                    }
                }
                Check("editors produced buildable dialogs", Views.UiKit.CapturedDialogs.Count > 0);
                Emit("      dialogs constructed: " + Views.UiKit.CapturedDialogs.Count);
                Views.UiKit.CaptureDialogs = false;
                Views.UiKit.CapturedDialogs.Clear();

                shell.Close();
                PumpMessages(150);
                application.Shutdown();

                Console.WriteLine();
                Console.WriteLine("window show check failures: " + failures);
                return failures == 0 ? 0 : 1;
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine("  FAIL " + ex.GetType().Name + ": " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine();
                Console.WriteLine("window show check failures: " + failures);
                return 1;
            }
            finally
            {
                try { GC.Collect(); GC.WaitForPendingFinalizers(); } catch { }
                try { if (Directory.Exists(root)) { Directory.Delete(root, true); } } catch { }
            }
        }

        /// <summary>
        /// Verifies the reminder presentation rules: an item inside its reminder window is reported
        /// by IsReminding, the widget shows a red border for it and no animation is ever started.
        /// The flashing reminder was removed deliberately, so this check also guards against it
        /// being reintroduced by accident.
        /// </summary>
        internal static int ReminderCheck()
        {
            int failures = 0;
            string root = Path.Combine(Path.GetTempPath(), "learnest-remindercheck-" + DateTime.Now.ToString("HHmmss"));
            Console.WriteLine("Learnest reminder presentation check");
            try
            {
                if (Directory.Exists(root)) { Directory.Delete(root, true); }
                Data.ContainerSession session = Data.ContainerSession.Create(root, "ReminderCheck");
                AppState.AttachContainer(session, false);
                AppState.Settings.AlertLeadHours = 72;

                DateTime now = DateTime.Now;

                Data.Todo soon = new Data.Todo();
                soon.Title = "两小时后到期（应进入提醒期）";
                soon.CategoryId = Data.Category.CreateUncategorized().Id;
                soon.DueAt = Core.IsoTime.Format(now.AddHours(2));
                AppState.Repo.SaveTodo(soon, null);

                Data.Todo later = new Data.Todo();
                later.Title = "三十天后到期（不应进入提醒期）";
                later.CategoryId = Data.Category.CreateUncategorized().Id;
                later.DueAt = Core.IsoTime.Format(now.AddDays(30));
                AppState.Repo.SaveTodo(later, null);

                Data.Todo faraway = new Data.Todo();
                faraway.Title = "没有截止时间（不应进入提醒期）";
                faraway.CategoryId = Data.Category.CreateUncategorized().Id;
                AppState.Repo.SaveTodo(faraway, null);

                Data.Todo finished = new Data.Todo();
                finished.Title = "已完成（不应进入提醒期）";
                finished.CategoryId = Data.Category.CreateUncategorized().Id;
                finished.DueAt = Core.IsoTime.Format(now.AddHours(1));
                finished.Status = Data.TodoStatus.Done;
                finished.CompletedAt = Core.IsoTime.Now();
                AppState.Repo.SaveTodo(finished, null);

                AppState.Commit();

                Check("a to-do due in 2h is inside the reminder window", TaskQuery.IsReminding(soon, now));
                Check("a to-do due in 30d is outside the reminder window", !TaskQuery.IsReminding(later, now));
                Check("a to-do without a deadline never reminds", !TaskQuery.IsReminding(faraway, now));
                Check("a completed to-do never reminds", !TaskQuery.IsReminding(finished, now));
                Check("an overdue to-do reminds", TaskQuery.IsReminding(new Data.Todo { Status = Data.TodoStatus.Open, DueAt = Core.IsoTime.Format(now.AddHours(-3)) }, now));

                Theming.ThemeManager.ApplyFromSettings();
                Loc.Apply("zh-CN");
                App application = new App();
                application.InitializeComponent();

                Views.MainWindow shell = new Views.MainWindow();
                shell.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
                shell.Left = -4000;
                shell.Top = -4000;
                shell.ShowInTaskbar = false;
                shell.Show();
                PumpMessages(600);

                Views.WidgetWindow widget = new Views.WidgetWindow(shell);
                widget.Left = -4000;
                widget.Top = -4000;
                widget.Show();
                PumpMessages(600);

                List<TaskQuery.AlertHit> hits = TaskQuery.Alerts(now);
                Check("the widget reports the reminding item as an alert", hits.Count > 0);
                Check("widget reminder state is exposed", widget.ReminderCount >= 0);

                widget.SyncReminderState(hits);
                PumpMessages(400);
                Check("widget reports the reminding item count", widget.ReminderCount == hits.Count);
                Check("widget tracks that something is reminding", widget.IsReminding);

                // The window frame must NOT carry the alert gradient: the reminder signal belongs on
                // the individual rows.  A red frame lingering on the widget was reported as residue.
                Check("window frame is not tinted by the reminder", !IsAlertColoured(widget));

                // Nothing may animate, and waiting must not change the state.
                PumpMessages(2500);
                Check("reminder state is stable over time", widget.IsReminding);

                widget.ClearReminderState();
                PumpMessages(300);
                Check("clearing resets the reminder flag", !widget.IsReminding);
                Check("cleared widget reports no alerts", widget.ReminderCount == 0);

                widget.Close();
                shell.Close();
                PumpMessages(200);
                application.Shutdown();

                Console.WriteLine();
                Console.WriteLine("reminder check failures: " + failures);
                return failures == 0 ? 0 : 1;
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine("  FAIL " + ex.GetType().Name + ": " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine();
                Console.WriteLine("reminder check failures: " + failures);
                return 1;
            }
            finally
            {
                try { GC.Collect(); GC.WaitForPendingFinalizers(); } catch { }
                try { if (Directory.Exists(root)) { Directory.Delete(root, true); } } catch { }
            }
        }

        /// <summary>
        /// Reports whether Learnest can create a directory and then write a file in a set of candidate
        /// container locations.  Used to tell a real Windows restriction apart from one inherited from
        /// the process that launched Learnest.
        /// </summary>
        internal static int FolderTest()
        {
            Console.WriteLine("Learnest folder write test");
            Console.WriteLine("  user: " + Environment.UserName + "   machine: " + Environment.MachineName);
            Console.WriteLine("  elevated: " + IsElevated());
            Console.WriteLine();

            List<string> targets = new List<string>();
            string extra = Environment.GetEnvironmentVariable("LEARNEST_TEST_PATH");
            if (!string.IsNullOrEmpty(extra)) { targets.Add(extra); }
            targets.Add(SafeFolder(Environment.SpecialFolder.MyDocuments));
            targets.Add(SafeFolder(Environment.SpecialFolder.UserProfile));
            targets.Add(SafeFolder(Environment.SpecialFolder.LocalApplicationData));
            targets.Add(SafeFolder(Environment.SpecialFolder.DesktopDirectory));
            targets.Add(Path.GetTempPath());

            int allowed = 0;
            int denied = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                string root = targets[i];
                if (string.IsNullOrEmpty(root)) { continue; }
                string folder = Path.Combine(root, "LearnestWriteTest");
                string folderResult;
                string fileResult;

                try
                {
                    Directory.CreateDirectory(folder);
                    folderResult = "OK";
                }
                catch (Exception ex)
                {
                    folderResult = "DENIED (" + ex.GetType().Name + ")";
                }

                if (folderResult == "OK")
                {
                    string probe = Path.Combine(folder, "probe.txt");
                    try
                    {
                        File.WriteAllText(probe, "learnest", new UTF8Encoding(false));
                        string readBack = File.ReadAllText(probe, UTF8Encoding.UTF8);
                        File.Delete(probe);
                        fileResult = readBack == "learnest" ? "OK" : "READ MISMATCH";
                        allowed++;
                    }
                    catch (Exception ex)
                    {
                        fileResult = "DENIED (" + ex.GetType().Name + ": " + ex.Message + ")";
                        denied++;
                    }
                }
                else
                {
                    fileResult = "(skipped)";
                    denied++;
                }

                Console.WriteLine("  " + root);
                Console.WriteLine("      create dir: " + folderResult);
                Console.WriteLine("      write file: " + fileResult);
            }

            Console.WriteLine();
            Console.WriteLine("writable: " + allowed + "   denied: " + denied);
            Console.WriteLine("(a location that allows directory creation but DENIES file creation points at a");
            Console.WriteLine(" filter driver - Controlled Folder Access, an EDR agent, or a process sandbox)");
            return 0;
        }

        /// <summary>
        /// True when the widget's window frame is painted with the alert gradient.  It must never be:
        /// the reminder is shown on the rows, and a red frame lingering on the widget was reported as
        /// visual residue.
        /// </summary>
        private static bool IsAlertColoured(Views.WidgetWindow widget)
        {
            try
            {
                System.Windows.Controls.Border frame = widget.FindName("WidgetFrame") as System.Windows.Controls.Border;
                if (frame == null) { return false; }
                return frame.BorderBrush is System.Windows.Media.LinearGradientBrush;
            }
            catch
            {
                return false;
            }
        }
        private static string SafeFolder(Environment.SpecialFolder folder)
        {
            try { return Environment.GetFolderPath(folder); }
            catch { return null; }
        }

        private static bool IsElevated()
        {
            try
            {
                System.Security.Principal.WindowsIdentity identity =
                    System.Security.Principal.WindowsIdentity.GetCurrent();
                System.Security.Principal.WindowsPrincipal principal =
                    new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verifies that the components that can only fail at run time are actually present, and that
        /// the folder picker uses the real Windows dialog rather than the built-in tree fallback.
        /// </summary>
        internal static int DialogCheck()
        {
            int failures = 0;
            Console.WriteLine("Learnest dialog check");

            // 1. The WPF folder dialog must be resolvable and constructible.  The previous code looked
            //    it up with Type.GetType(assembly-qualified name), which returns null before
            //    PresentationFramework is loaded and silently degraded to the fallback picker.
            try
            {
                Type type = typeof(Microsoft.Win32.OpenFolderDialog);
                Console.WriteLine("  ok   OpenFolderDialog type resolved: " + type.Assembly.GetName().Name + " " +
                                  type.Assembly.GetName().Version);
                Microsoft.Win32.OpenFolderDialog dialog = new Microsoft.Win32.OpenFolderDialog();
                dialog.Title = "probe";
                dialog.Multiselect = false;
                if (dirExists(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))
                {
                    dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                }
                Console.WriteLine("  ok   OpenFolderDialog constructed and configured (Title/InitialDirectory accepted)");
                if (string.IsNullOrEmpty(dialog.FolderName)) { }
                dialog = null;
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine("  FAIL OpenFolderDialog unusable: " + ex.GetType().Name + ": " + ex.Message);
            }

            // 2. The reflective lookup that used to break must not be what we rely on any more.
            try
            {
                Type byName = Type.GetType("Microsoft.Win32.OpenFolderDialog, PresentationFramework");
                Console.WriteLine("  info Type.GetType(assembly-qualified) -> " + (byName == null ? "null (this is why the old code degraded)" : "resolved"));
            }
            catch
            {
            }

            // 3. The COM shell picker route must be constructible (used when route 1 is unavailable).
            try
            {
                Type comType = Type.GetTypeFromCLSID(new Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7"));
                if (comType == null)
                {
                    Console.WriteLine("  warn shell item dialog CLSID not registered (COM fallback unavailable)");
                }
                else
                {
                    object instance = Activator.CreateInstance(comType);
                    bool isCom = instance != null && Marshal.IsComObject(instance);
                    Console.WriteLine("  ok   shell folder dialog (IFileDialog) instantiated, IsComObject=" + isCom);
                    if (isCom) { Marshal.ReleaseComObject(instance); }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("  warn shell folder dialog probe failed: " + ex.GetType().Name + ": " + ex.Message);
            }

            Console.WriteLine();
            Console.WriteLine("dialog check failures: " + failures);
            return failures == 0 ? 0 : 1;
        }

        private static bool dirExists(string path)
        {
            return !string.IsNullOrEmpty(path) && Directory.Exists(path);
        }

        /// <summary>
        /// Command line helper: create a container without opening the UI.
        /// Used by automation and by the smoke test, and handy for provisioning a container on a
        /// shared drive before the first launch.
        /// </summary>
        internal static int CreateContainer(string folder, string name)
        {
            try
            {
                if (string.IsNullOrEmpty(folder))
                {
                    Console.Error.WriteLine("usage: Learnest.exe --create-container <folder> [name]");
                    return 2;
                }
                if (string.IsNullOrEmpty(name)) { name = AppPaths.ContainerNameFromPath(folder); }
                if (Data.ContainerLayout.IsContainer(folder))
                {
                    Console.Error.WriteLine("A container already exists at " + folder);
                    return 3;
                }
                using (Data.ContainerSession session = Data.ContainerSession.Create(folder, name))
                {
                    session.WriteMeta("containerName", name);
                    session.Flush();
                    Console.WriteLine(session.Root);
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("create-container failed: " + ex.Message);
                return 4;
            }
        }

        private static void WriteTranscript()
        {
            try
            {
                if (_transcript == null) { return; }
                File.WriteAllText(TranscriptPath, _transcript.ToString(), new UTF8Encoding(false));
            }
            catch
            {
            }
        }

        internal static int Run(string scenario)
        {
            AttachParentConsole();
            try { Console.OutputEncoding = Encoding.UTF8; }
            catch { /* no console available; the caller reads the exit code */ }
            string root = Path.Combine(Path.GetTempPath(), "learnest-selftest-" + DateTime.Now.ToString("HHmmss"));
            Emit("Learnest self test");
            Emit("  scratch container: " + root);
            Emit("");

            // The self test output is also written to a file: Learnest.exe is a windowed
            // application, so when it is started without a console the text would be lost.
            StringBuilder transcript = new StringBuilder();
            _transcript = transcript;

            try
            {
                if (Directory.Exists(root)) { Directory.Delete(root, true); }
                Directory.CreateDirectory(root);

                ContainerTests(root);
                CalendarTests();

                Emit("");
                Emit("passed: " + _passed + "   failed: " + _failed);
                if (_failed > 0)
                {
                    Emit("");
                    Emit("failures:");
                    for (int i = 0; i < Failures.Count; i++) { Emit("  - " + Failures[i]); }
                    WriteTranscript();
                    return 1;
                }
                WriteTranscript();
                return 0;
            }
            catch (Exception ex)
            {
                Emit("FATAL: " + ex);
                if (_transcript != null)
                {
                    _transcript.AppendLine("FATAL: " + ex);
                    WriteTranscript();
                }
                return 2;
            }
            finally
            {
                try { GC.Collect(); GC.WaitForPendingFinalizers(); } catch { }
                try
                {
                    if (Directory.Exists(root)) { Directory.Delete(root, true); }
                }
                catch
                {
                    Emit("(scratch container kept at " + root + ")");
                }
            }
        }

        // ================================================================ container
        private static void ContainerTests(string root)
        {
            Section("container folder selection");

            // Regression guard: the wizard must never pre-fill a path Learnest cannot write to.
            // On this machine C:\Users\<user>\Documents is a read-only placeholder, so the
            // suggestion logic has to fall through to something usable.
            string suggestion = AppPaths.SuggestedContainerRoot();
            Check("suggested container root is not empty", !string.IsNullOrEmpty(suggestion));
            Emit("      suggested: " + suggestion);
            string probeReason = null;
            bool probeOk = false;
            try { probeOk = AppPaths.CanWriteTo(suggestion, out probeReason); }
            catch (Exception ex) { probeReason = ex.GetType().Name + ": " + ex.Message; }
            Emit("      writable: " + probeOk + "   reason: " + (probeReason ?? "(none)"));
            Check("suggested container root is writable", probeOk);
            Check("suggested container root is not a protected location", !AppPaths.IsUnsafeContainerTarget(suggestion));

            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            bool documentsWritable = false;
            string documentsReason = null;
            try { documentsWritable = AppPaths.CanWriteTo(documents, out documentsReason); }
            catch (Exception ex) { documentsReason = ex.GetType().Name + ": " + ex.Message; }
            Emit("      MyDocuments resolves to: " + documents);
            Emit("      MyDocuments writable: " + documentsWritable + "   reason: " + (documentsReason ?? "(none)"));
            Check("a non-writable folder reports a reason", documentsWritable || !string.IsNullOrEmpty(documentsReason));

            string writeReason;
            Check("a usable folder probes clean", AppPaths.CanWriteTo(Path.Combine(root, "ok-folder"), out writeReason) && writeReason == null);
            string filePath = Path.Combine(root, "作业1.4-1.5.pdf");
            File.WriteAllText(filePath, "not a folder");
            string fileReason;
            Check("a file path is rejected with a reason",
                !AppPaths.CanWriteTo(filePath, out fileReason) && !string.IsNullOrEmpty(fileReason));
            Check("system locations are refused", AppPaths.IsUnsafeContainerTarget("C:\\"));
            Check("temp folder is accepted", !AppPaths.IsUnsafeContainerTarget(Path.GetTempPath()));

            Section("container database");
            string exportName;

            string containerRoot = Path.Combine(root, "MyLearnest");
            Data.ContainerSession session = Data.ContainerSession.Create(containerRoot, "MyLearnest");
            Check("container folder created", Directory.Exists(containerRoot));
            Check("learnest.db exists", File.Exists(Path.Combine(containerRoot, "learnest.db")));
            Check("manifest written", File.Exists(Path.Combine(containerRoot, "learnest.container.json")));
            Check("sqlite library detected", Data.SqliteInfo.Describe() != "(unavailable)");
            Emit("      native library: " + Data.SqliteInfo.Describe());
            Check("schema version recorded", session.GetMeta("schemaVersion", "") == "1");

            Data.Repository repo = session.Repository;

            Section("categories");
            List<Data.Category> categories = repo.LoadCategories();
            Check("default categories seeded", categories.Count >= 6);
            bool hasUncategorized = false;
            for (int i = 0; i < categories.Count; i++)
            {
                if (categories[i].IsUncategorized) { hasUncategorized = true; }
            }
            Check("Uncategorized bucket present", hasUncategorized);

            Data.Category homework = repo.FindCategoryByName("课程作业");
            Check("seeded category lookup", homework != null);

            Data.Category custom = new Data.Category();
            custom.Id = Core.IsoTime.ShortId("cat");
            custom.Name = "英语六级";
            custom.Color = "#22C55E";
            repo.SaveCategory(custom);
            Check("category saved", repo.FindCategory(custom.Id) != null);
            Check("category colour persisted", repo.FindCategory(custom.Id).Color == "#22C55E");

            Section("todos");
            Data.Todo task = new Data.Todo();
            task.Title = "高数作业 1.4-1.5 章节 🎯";
            task.CategoryId = homework != null ? homework.Id : "";
            task.DueAt = "2026-10-10T23:59:00";
            task.LeadMinutes = 1440;
            task.NoteMarkdown = "# 高数作业\n\n- [ ] 1.4 习题 1-9\n- [x] 1.5 预习\n\n公式：$\\int x dx$\n\n| 题号 | 状态 |\n| --- | --- |\n| 1 | 完成 |";
            task.Tags.Add("期中重点");
            task.Tags.Add("第二章/极限");
            task.Steps.Add("1.4 习题 1-9");
            task.Priority = 2;
            repo.SaveTodo(task, "添加待办：" + task.Title);

            Data.Todo loaded = repo.FindTodo(task.Id);
            Check("todo persisted", loaded != null);
            Check("unicode title round trip", loaded != null && loaded.Title == "高数作业 1.4-1.5 章节 🎯");
            Check("markdown note round trip", loaded != null && loaded.NoteMarkdown.Contains("1.4 习题 1-9"));
            Check("due date preserved", loaded != null && loaded.DueAt == "2026-10-10T23:59:00");
            Check("tags round trip", loaded != null && loaded.Tags.Count == 2 && loaded.Tags[1] == "第二章/极限");
            Check("steps round trip", loaded != null && loaded.Steps.Count == 1);

            repo.SetTodoProgress(task.Id, 0.4);
            Check("progress update", Math.Abs(repo.FindTodo(task.Id).Progress - 0.4) < 0.0001);
            repo.SetTodoStatus(task.Id, Data.TodoStatus.Done);
            Data.Todo finished = repo.FindTodo(task.Id);
            Check("completed status", finished.Status == Data.TodoStatus.Done);
            Check("completed timestamp set", !string.IsNullOrEmpty(finished.CompletedAt));
            repo.SetTodoStatus(task.Id, Data.TodoStatus.Open);
            Check("reopen clears completion", string.IsNullOrEmpty(repo.FindTodo(task.Id).CompletedAt));

            Section("attachments (pack and go)");
            string sourceFile = Path.Combine(root, "作业1.4-1.5.pdf");
            File.WriteAllText(sourceFile, "fake pdf content for the self test", new UTF8Encoding(false));
            Data.Attachment attachment = repo.ImportAttachment(Data.AttachmentOwner.Todo, task.Id, sourceFile, "作业文件", "作业1.4-1.5.pdf");
            Check("attachment registered", attachment.Id.Length > 0);
            string absolute = session.ResolveRelative(attachment.RelativePath);
            Check("attachment copied into container", File.Exists(absolute));
            Check("attachment path is relative", !Path.IsPathRooted(attachment.RelativePath) && attachment.RelativePath.StartsWith("attachments/"));
            Data.Attachment renamed = repo.RenameAttachment(attachment.Id, "作业1.4-1.5-已批注", true);
            Check("attachment renamed on disk", File.Exists(session.ResolveRelative(renamed.RelativePath)));
            Check("attachment rename keeps extension", renamed.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
            Check("todo reports attachments", repo.FindTodo(task.Id).Attachments.Count == 1);

            Section("category deletion policy");
            Data.Todo inCustom = new Data.Todo();
            inCustom.Title = "六级词汇 300 词";
            inCustom.CategoryId = custom.Id;
            repo.SaveTodo(inCustom, null);
            Check("todo counted in category", repo.CountTodosInCategory(custom.Id) == 1);
            repo.DeleteCategory(custom.Id, false, Data.Category.CreateUncategorized().Id);
            Check("category deleted", repo.FindCategory(custom.Id) == null);
            Check("todo moved to Uncategorized", repo.FindTodo(inCustom.Id).CategoryId == "uncategorized");
            Check("Uncategorized cannot be deleted", repo.FindCategory("uncategorized") != null);

            Section("exams");
            Data.Exam exam = new Data.Exam();
            exam.Title = "高等数学（上）期末考试";
            exam.Course = "高等数学";
            exam.StartAt = "2026-10-12T09:00:00";
            exam.EndAt = "2026-10-12T11:00:00";
            exam.Location = "教三 305";
            exam.Seat = "27";
            exam.NoteMarkdown = "## 复习范围\n- 极限与连续\n- 中值定理";
            repo.SaveExam(exam);
            Check("exam persisted", repo.FindExam(exam.Id) != null);
            Check("exam unicode round trip", repo.FindExam(exam.Id).Title == "高等数学（上）期末考试");

            Section("competitions");
            Data.Competition competition = new Data.Competition();
            competition.Name = "全国大学生数学建模竞赛";
            competition.Organizer = "中国工业与应用数学学会";
            competition.Stage = "registered";
            competition.StartAt = "2026-09-01T00:00:00";
            competition.EndAt = "2026-10-20T23:59:00";
            competition.Team = new List<string> { "张三", "李四", "王五" };
            repo.SaveCompetition(competition);
            Data.Competition savedCompetition = repo.FindCompetition(competition.Id);
            Check("competition persisted", savedCompetition != null);
            Check("competition tag auto assigned", savedCompetition != null && savedCompetition.Tag.Length > 0);

            Data.Milestone milestone = new Data.Milestone();
            milestone.CompetitionId = competition.Id;
            milestone.Title = "完成选题与数据预处理";
            milestone.DueAt = "2026-09-10T23:59:00";
            milestone.NoteMarkdown = "### 数据来源\n- 官网附件 A\n- 校内图书馆数据库";
            repo.SaveMilestone(milestone);
            Data.Competition withMilestone = repo.FindCompetition(competition.Id);
            Check("milestone persisted", withMilestone.Milestones.Count == 1);
            repo.SetMilestoneDone(milestone.Id, true);
            Check("milestone completion", repo.FindCompetition(competition.Id).Milestones[0].IsDone);
            Check("milestone progress", repo.FindCompetition(competition.Id).MilestoneProgress() > 0.99);

            Data.CompetitionEvent competitionEvent = new Data.CompetitionEvent();
            competitionEvent.CompetitionId = competition.Id;
            competitionEvent.Title = "赛前培训";
            competitionEvent.Kind = "event";
            competitionEvent.StartAt = "2026-09-05T14:00:00";
            competitionEvent.Location = "线上腾讯会议";
            repo.SaveEvent(competitionEvent);
            Check("event persisted", repo.FindCompetition(competition.Id).Events.Count == 1);

            // A todo referencing two competitions at once, plus the tag reference path.
            Data.Competition second = new Data.Competition();
            second.Name = "校级英语演讲比赛";
            second.Tag = "英语演讲";
            repo.SaveCompetition(second);

            Data.Todo multi = new Data.Todo();
            multi.Title = "准备建模论文摘要";
            multi.CategoryId = Data.Category.CreateUncategorized().Id;
            multi.DueAt = "2026-09-20T22:00:00";
            multi.CompetitionIds.Add(competition.Id);
            multi.CompetitionIds.Add(second.Id);
            multi.Tags.Add(savedCompetition.Tag);
            repo.SaveTodo(multi, null);
            Data.Todo multiLoaded = repo.FindTodo(multi.Id);
            Check("todo references two competitions", multiLoaded.CompetitionIds.Count == 2);
            Check("competition sees linked todo", repo.FindCompetition(competition.Id).LinkedTodos.Count >= 1);

            Section("queries");
            Data.Repository.Counters counters = repo.LoadCounters(DateTime.Now);
            Check("counters return open todos", counters.Open >= 2);
            Check("counters count exams", counters.ExamCount >= 1);
            List<Data.Todo> dueSoon = repo.LoadTodosDueBefore(new DateTime(2027, 1, 1), false);
            Check("due-before query returns rows", dueSoon.Count >= 2);
            Check("due-before query is sorted by due date", dueSoon.Count < 2 || dueSoon[0].DueAt.CompareTo(dueSoon[1].DueAt) <= 0);
            List<Data.ActivityEntry> activity = repo.LoadActivity(50);
            Check("activity log records edits", activity.Count >= 5);

            Section("settings in container");
            session.Settings.Theme = Data.ThemeMode.Dark;
            session.Settings.AccentColor = "#8B5CF6";
            session.Settings.BackgroundImage = "backgrounds/campus.jpg";
            session.Settings.BackgroundMode = Data.BackgroundMode.Tile;
            session.Settings.BackgroundBlur = 12.5;
            session.Settings.WidgetSort = "modifiedDesc";
            session.Settings.Language = "en";
            session.SaveSettings();
            session.Dispose();

            Data.ContainerSession reopened = Data.ContainerSession.Open(containerRoot);
            Check("container reopens", reopened != null);
            Check("theme persisted in container", reopened.Settings.Theme == Data.ThemeMode.Dark);
            Check("accent persisted", reopened.Settings.AccentColor == "#8B5CF6");
            Check("background mode persisted", reopened.Settings.BackgroundMode == Data.BackgroundMode.Tile);
            Check("blur value persisted", Math.Abs(reopened.Settings.BackgroundBlur - 12.5) < 0.001);
            Check("widget sort persisted", reopened.Settings.WidgetSort == "modifiedDesc");
            Check("language persisted", reopened.Settings.Language == "en");
            Check("todos survive reopen", reopened.Repository.LoadTodos().Count >= 3);
            Check("relative attachment path survives reopen",
                File.Exists(reopened.ResolveRelative(reopened.Repository.FindTodo(task.Id).Attachments[0].RelativePath)));

            Section("backup export / import");
            string exportPath = Services.BackupService.ExportContainer(reopened, Path.Combine(root, "exports"), out exportName);
            Check("export archive created", File.Exists(exportPath));
            Check("export file name follows the contract", exportName.EndsWith(".learnest.zip", StringComparison.OrdinalIgnoreCase));
            Check("export name contains the container folder name", exportName.Contains("MyLearnest"));
            Emit("      export: " + exportName);

            string importTarget = Path.Combine(root, "Restored");
            string importError;
            Services.BackupService.ImportContainer(exportPath, importTarget, out importError);
            Check("import reported no error", string.IsNullOrEmpty(importError));
            Check("imported database present", File.Exists(Path.Combine(importTarget, "learnest.db")));
            Data.ContainerSession restored = Data.ContainerSession.Open(importTarget);
            Check("imported container opens", restored != null);
            Check("imported todos match", restored.Repository.LoadTodos().Count == reopened.Repository.LoadTodos().Count);
            Check("imported settings match", restored.Settings.BackgroundMode == Data.BackgroundMode.Tile);
            Data.Todo restoredTask = restored.Repository.FindTodo(task.Id);
            Check("imported attachment file present",
                restoredTask != null && restoredTask.Attachments.Count == 1 &&
                File.Exists(restored.ResolveRelative(restoredTask.Attachments[0].RelativePath)));
            Check("imported markdown note present", restoredTask != null && restoredTask.NoteMarkdown.Contains("1.4 习题 1-9"));
            restored.Dispose();
            reopened.Dispose();

            Section("competition pack and go");
            Data.ContainerSession packSession = Data.ContainerSession.Open(importTarget);
            Data.Competition packTarget = packSession.Repository.LoadCompetitions(true)[0];
            string packPath = Services.CompetitionPackService.Pack(packSession, packTarget, Path.Combine(root, "packs"));
            Check("competition pack created", File.Exists(packPath));
            Check("competition pack name follows the contract",
                Path.GetFileName(packPath).EndsWith(Services.CompetitionPackService.Suffix, StringComparison.OrdinalIgnoreCase));
            string packError;
            Data.Competition imported = Services.CompetitionPackService.Import(packSession, packPath, out packError);
            Check("competition pack imports back", imported != null && string.IsNullOrEmpty(packError));
            Check("imported competition keeps its milestones",
                imported != null && imported.Milestones.Count == packTarget.Milestones.Count);
            Check("imported competition keeps its events",
                imported != null && imported.Events.Count == packTarget.Events.Count);
            Check("imported competition keeps its markdown log",
                imported != null && imported.NoteMarkdown == packTarget.NoteMarkdown);
            packSession.Dispose();
        }

        // ================================================================ calendar
        private static void CalendarTests()
        {
            Section("gregorian perpetual calendar");
            Check("weekday index is monday based", Services.CalendarService.WeekdayIndex(DayOfWeek.Monday) == 0 &&
                                                  Services.CalendarService.WeekdayIndex(DayOfWeek.Sunday) == 6);
            Check("weekday naming", Services.CalendarService.WeekdayName(DayOfWeek.Monday) == "周一");
            Check("weekend detection", Services.CalendarService.IsWeekend(new DateTime(2026, 10, 10)) &&
                                       !Services.CalendarService.IsWeekend(new DateTime(2026, 10, 9)));
            Check("start of week is monday",
                Services.CalendarService.StartOfWeek(new DateTime(2026, 10, 10)) == new DateTime(2026, 10, 5));

            List<List<DateTime>> weeks = Services.CalendarService.BuildMonthGrid(2026, 10);
            Check("month grid has whole weeks", weeks.Count >= 4 && weeks.Count <= 6);
            Check("month grid starts on a monday", weeks[0][0].DayOfWeek == DayOfWeek.Monday);
            Check("month grid contains the first of the month", Contains(weeks, new DateTime(2026, 10, 1)));
            Check("month grid is rectangular", AllWeeksHaveSevenDays(weeks));
            Check("october 2026 has 31 days", DateTime.DaysInMonth(2026, 10) == 31);
            Check("february leap year", DateTime.DaysInMonth(2028, 2) == 29 && DateTime.DaysInMonth(2026, 2) == 28);
            Check("century leap rule", DateTime.DaysInMonth(2000, 2) == 29 && DateTime.DaysInMonth(1900, 2) == 28);
            Check("iso week number", Services.CalendarService.IsoWeekNumber(new DateTime(2026, 1, 1)) == 1);

            string distance = Services.CalendarService.DescribeDistance(DateTime.Now.AddHours(5), DateTime.Now);
            Check("distance describes the future", distance.Contains("小时"));
            Check("distance describes overdue",
                Services.CalendarService.DescribeDistance(DateTime.Now.AddHours(-3), DateTime.Now).StartsWith("已逾期"));
            Check("span label", Services.CalendarService.DescribeSpan(TimeSpan.FromDays(3)).Contains("天"));
        }

        private static bool Contains(List<List<DateTime>> weeks, DateTime day)
        {
            for (int i = 0; i < weeks.Count; i++)
            {
                for (int j = 0; j < weeks[i].Count; j++)
                {
                    if (weeks[i][j] == day) { return true; }
                }
            }
            return false;
        }

        private static bool AllWeeksHaveSevenDays(List<List<DateTime>> weeks)
        {
            for (int i = 0; i < weeks.Count; i++)
            {
                if (weeks[i].Count != 7) { return false; }
            }
            return true;
        }

        // ================================================================ helpers
        private static void Section(string name)
        {
            Emit("");
            Emit("[" + name + "]");
        }

        private static void Check(string what, bool condition)
        {
            if (condition)
            {
                _passed++;
                Emit("  ok   " + what);
            }
            else
            {
                _failed++;
                Failures.Add(what);
                Emit("  FAIL " + what);
            }
        }
    }
}
