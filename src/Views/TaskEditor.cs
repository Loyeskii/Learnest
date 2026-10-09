using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Learnest.Data;
using Learnest.Services;

using Learnest.Core;
namespace Learnest.Views
{
    /// <summary>
    /// A 24 hour time field backed by two combo boxes.  Learnest ships its own instead of a
    /// DatePicker-with-time because deadlines are almost always "23:59" and a five minute step
    /// keeps the picker quick to use with the keyboard.
    /// </summary>
    internal sealed class TimeField : Grid
    {
        private readonly ComboBox _hour = new ComboBox();
        private readonly ComboBox _minute = new ComboBox();

        public TimeField(DateTime value, int minuteStep)
        {
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            List<KeyValuePair<string, string>> hours = new List<KeyValuePair<string, string>>();
            for (int h = 0; h < 24; h++)
            {
                hours.Add(new KeyValuePair<string, string>(h.ToString("00"), h.ToString("00")));
            }
            List<KeyValuePair<string, string>> minutes = new List<KeyValuePair<string, string>>();
            if (minuteStep <= 0) { minuteStep = 5; }
            for (int m = 0; m < 60; m += minuteStep)
            {
                minutes.Add(new KeyValuePair<string, string>(m.ToString("00"), m.ToString("00")));
            }

            _hour = UiKit.Combo(hours, value.Hour.ToString("00"));
            _minute = UiKit.Combo(minutes, (value.Minute - value.Minute % minuteStep).ToString("00"));

            Children.Add(_hour);
            TextBlock colon = UiKit.Body(":");
            colon.Margin = new Thickness(6, 0, 6, 0);
            colon.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(colon, 1);
            Children.Add(colon);
            Grid.SetColumn(_minute, 2);
            Children.Add(_minute);
        }

        public int Hour
        {
            get
            {
                string key = UiKit.SelectedKey(_hour);
                int hour;
                return int.TryParse(key, out hour) ? hour : 0;
            }
        }

        public int Minute
        {
            get
            {
                string key = UiKit.SelectedKey(_minute);
                int minute;
                return int.TryParse(key, out minute) ? minute : 0;
            }
        }

        public DateTime Compose(DateTime day)
        {
            return new DateTime(day.Year, day.Month, day.Day, Hour, Minute, 0);
        }
    }

    /// <summary>
    /// Create or edit a to-do.  The default shape is deliberately minimal - one line of text plus
    /// an optional deadline - and everything else (markdown advanced record, assignment files,
    /// competition references, tags, lead time) lives behind the "advanced record" area.
    /// </summary>
    internal sealed class TaskEditor
    {
        public static void ShowCreate(IShell shell, string presetCategoryId)
        {
            Show(shell, null, presetCategoryId, null);
        }

        public static void ShowEdit(IShell shell, Todo todo)
        {
            Show(shell, todo, null, null);
        }

        /// <summary>Open the editor directly on the advanced record tab (used by "高级记录").</summary>
        public static void ShowAdvanced(IShell shell, Todo todo)
        {
            Show(shell, todo, null, "advanced");
        }

        private static void Show(IShell shell, Todo existing, string presetCategoryId, string startSection)
        {
            Window owner = shell != null ? shell.Window : null;
            bool isNew = existing == null;
            Todo working = isNew ? new Todo() : existing.Clone();
            if (isNew)
            {
                working.Id = "";
                working.CreatedAt = "";
                working.UpdatedAt = "";
                working.LeadMinutes = 1440;
                working.Priority = 1;
                if (!string.IsNullOrEmpty(presetCategoryId)) { working.CategoryId = presetCategoryId; }
                if (string.IsNullOrEmpty(working.CategoryId))
                {
                    working.CategoryId = shell != null && !string.IsNullOrEmpty(shell.ActiveCategoryId)
                        ? shell.ActiveCategoryId : Category.CreateUncategorized().Id;
                }
            }
            List<Attachment> attachments = new List<Attachment>();
            for (int i = 0; i < working.Attachments.Count; i++) { attachments.Add(working.Attachments[i]); }

            // ------------------------------------------------------------ basic fields
            TextBox titleBox = UiKit.Input(working.Title, Loc.T("tasks.quick_add_placeholder"));

            List<KeyValuePair<string, string>> categoryChoices = new List<KeyValuePair<string, string>>();
            List<Category> categories = AppState.Data.Categories;
            for (int i = 0; i < categories.Count; i++)
            {
                categoryChoices.Add(new KeyValuePair<string, string>(categories[i].Id, categories[i].Name));
            }
            ComboBox categoryCombo = UiKit.Combo(categoryChoices, working.CategoryId);

            ComboBox priorityCombo = UiKit.Combo(new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("0", Loc.T("tasks.priority_low")),
                new KeyValuePair<string, string>("1", Loc.T("tasks.priority_normal")),
                new KeyValuePair<string, string>("2", Loc.T("tasks.priority_high")),
                new KeyValuePair<string, string>("3", Loc.T("tasks.priority_urgent"))
            }, working.Priority.ToString());

            DateTime dueSeed = working.DueDate.HasValue ? working.DueDate.Value : DateTime.Now.Date.AddDays(1).AddHours(23).AddMinutes(59);
            System.Windows.Controls.DatePicker dueDatePicker = new System.Windows.Controls.DatePicker();
            dueDatePicker.SelectedDate = working.DueDate.HasValue ? working.DueDate.Value.Date : (DateTime?)null;
            dueDatePicker.Style = UiKit.Style("DatePicker");
            TimeField dueTime = new TimeField(dueSeed, 5);

            CheckBox hasDeadline = new CheckBox();
            hasDeadline.Content = Loc.T("tasks.set_deadline");
            hasDeadline.IsChecked = working.DueDate.HasValue || isNew;
            hasDeadline.Margin = new Thickness(0, 2, 0, 6);

            Grid dueRow = new Grid();
            dueRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            dueRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            dueRow.Children.Add(dueDatePicker);
            Grid.SetColumn(dueTime, 1);
            dueTime.Margin = new Thickness(8, 0, 0, 0);
            dueRow.Children.Add(dueTime);

            StackPanel quickButtons = new StackPanel();
            quickButtons.Orientation = Orientation.Horizontal;
            quickButtons.Margin = new Thickness(0, 6, 0, 0);
            quickButtons.Children.Add(QuickDate(Loc.T("calendar.today"), dueDatePicker, dueTime, DateTime.Now.Date.AddHours(23).AddMinutes(59)));
            quickButtons.Children.Add(QuickDate(Loc.T("calendar.quick_tomorrow"), dueDatePicker, dueTime, DateTime.Now.Date.AddDays(1).AddHours(23).AddMinutes(59)));
            quickButtons.Children.Add(QuickDate(Loc.T("calendar.quick_weekend"), dueDatePicker, dueTime, NextWeekend()));
            quickButtons.Children.Add(QuickDate(Loc.T("calendar.quick_next_week"), dueDatePicker, dueTime, DateTime.Now.Date.AddDays(7).AddHours(23).AddMinutes(59)));

            hasDeadline.Checked += delegate { dueRow.IsEnabled = true; quickButtons.IsEnabled = true; };
            hasDeadline.Unchecked += delegate { dueRow.IsEnabled = false; quickButtons.IsEnabled = false; };
            dueRow.IsEnabled = hasDeadline.IsChecked == true;
            quickButtons.IsEnabled = hasDeadline.IsChecked == true;

            // Lead time for the flashing reminder.
            ComboBox leadCombo = UiKit.Combo(new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("60", "1 " + Loc.T("calendar.hour")),
                new KeyValuePair<string, string>("360", "6 " + Loc.T("calendar.hour")),
                new KeyValuePair<string, string>("1440", "1 " + Loc.T("timeline.day")),
                new KeyValuePair<string, string>("2880", "2 " + Loc.T("timeline.day")),
                new KeyValuePair<string, string>("4320", "3 " + Loc.T("timeline.day")),
                new KeyValuePair<string, string>("10080", "7 " + Loc.T("timeline.day"))
            }, working.LeadMinutes.ToString());

            // Progress (only meaningful once a task is under way).
            Slider progressSlider = new Slider();
            progressSlider.Minimum = 0;
            progressSlider.Maximum = 1;
            progressSlider.Value = working.Progress;
            progressSlider.TickFrequency = 0.25;
            TextBlock progressLabel = UiKit.Caption((working.Progress * 100).ToString("0") + "%");
            progressSlider.ValueChanged += delegate
            {
                progressLabel.Text = (progressSlider.Value * 100).ToString("0") + "%";
            };

            // ------------------------------------------------------------ tags & competitions
            TextBox tagsBox = UiKit.Input(string.Join(", ", working.Tags.ToArray()), "期中重点, 第二章");
            WrapPanel competitionChips = new WrapPanel();
            Dictionary<string, CheckBox> competitionChecks = new Dictionary<string, CheckBox>();
            List<Competition> competitions = AppState.Data.Competitions;
            for (int i = 0; i < competitions.Count; i++)
            {
                Competition competition = competitions[i];
                CheckBox chip = new CheckBox();
                chip.Content = string.IsNullOrEmpty(competition.Tag) ? competition.Name : competition.Tag;
                chip.Margin = new Thickness(0, 0, 14, 6);
                chip.Tag = competition.Id;
                chip.IsChecked = working.CompetitionIds.Contains(competition.Id);
                competitionChecks[competition.Id] = chip;
                competitionChips.Children.Add(chip);
            }
            if (competitions.Count == 0)
            {
                competitionChips.Children.Add(UiKit.Caption(Loc.T("comp.empty_hint")));
            }

            // ------------------------------------------------------------ advanced record
            Border previewHost = new Border();
            previewHost.Padding = new Thickness(14);
            previewHost.CornerRadius = new CornerRadius(11);
            previewHost.Background = UiKit.Brush("Brush.SurfaceContainer");
            previewHost.BorderBrush = UiKit.Brush("Brush.OutlineVariant");
            previewHost.BorderThickness = new Thickness(1);

            MarkdownEditor markdown = new MarkdownEditor(working.NoteMarkdown, previewHost, true);

            StackPanel attachmentList = new StackPanel();
            Label attachmentSummary = new Label();
            attachmentSummary.Padding = new Thickness(0);
            attachmentSummary.Foreground = UiKit.Brush("Brush.OnSurfaceVariant");
            attachmentSummary.FontSize = 12;

            TextBox categoryHintBox = UiKit.Input("", "作业文件 / 参考资料");
            TextBox renameBox = UiKit.Input("", Loc.T("tasks.rename_file_prompt"));

            Action rebuildAttachments = null;
            rebuildAttachments = delegate
            {
                attachmentList.Children.Clear();
                if (attachments.Count == 0)
                {
                    attachmentList.Children.Add(UiKit.Caption(Loc.T("tasks.no_attachments")));
                }
                for (int i = 0; i < attachments.Count; i++)
                {
                    attachmentList.Children.Add(AttachmentRow(owner, attachments[i], rebuildAttachments));
                }
                attachmentSummary.Content = Loc.T("common.total") + ": " + attachments.Count;
            };
            rebuildAttachments();

            Button importFiles = UiKit.Tonal(Loc.T("tasks.import_file"), delegate
            {
                string[] files = Dialogs.PickFiles(owner, Loc.T("tasks.import_file"), null, true);
                if (files.Length == 0) { return; }
                string category = (categoryHintBox.Text ?? "").Trim();
                // Files need an owner id; create the todo row first when it is brand new.
                EnsurePersisted(working, attachments, titleBox.Text);
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        attachments.Add(AppState.Repo.ImportAttachment(AttachmentOwner.Todo, working.Id, files[i], category, null));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(owner, Loc.T("error.attachment_copy", ex.Message),
                            Loc.T("error.generic"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                working.Attachments = attachments;
                rebuildAttachments();
            });

            Button importFolder = UiKit.Outlined(Loc.T("tasks.import_folder"), delegate
            {
                string folder = Dialogs.PickFolder(owner, Loc.T("tasks.import_folder"), null);
                if (string.IsNullOrEmpty(folder)) { return; }
                string category = (categoryHintBox.Text ?? "").Trim();
                EnsurePersisted(working, attachments, titleBox.Text);
                try
                {
                    attachments.Add(AppState.Repo.ImportAttachment(AttachmentOwner.Todo, working.Id, folder, category, null));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(owner, Loc.T("error.attachment_copy", ex.Message),
                        Loc.T("error.generic"), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                working.Attachments = attachments;
                rebuildAttachments();
            });

            StackPanel attachmentToolbar = new StackPanel();
            attachmentToolbar.Orientation = Orientation.Horizontal;
            attachmentToolbar.Children.Add(importFiles);
            importFolder.Margin = new Thickness(8, 0, 0, 0);
            attachmentToolbar.Children.Add(importFolder);

            StackPanel attachmentSection = new StackPanel();
            attachmentSection.Children.Add(UiKit.SectionHeader(Loc.T("tasks.attachments_title"), attachmentSummary));
            attachmentSection.Children.Add(UiKit.Field(Loc.T("comp.file_category"), categoryHintBox,
                Loc.T("comp.pack_and_go_hint")));
            attachmentSection.Children.Add(attachmentToolbar);
            attachmentSection.Children.Add(attachmentList);
            attachmentSection.Margin = new Thickness(0, 10, 0, 0);

            // ------------------------------------------------------------ layout
            TabControl tabs = new TabControl();
            tabs.MinHeight = 380;

            TabItem basics = new TabItem();
            basics.Header = Loc.T("common.summary");
            StackPanel basicPanel = new StackPanel();
            basicPanel.Margin = new Thickness(2, 12, 2, 2);
            basicPanel.Children.Add(UiKit.Field(Loc.T("tasks.title_label"), titleBox, null));

            Grid metaRow = new Grid();
            metaRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metaRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            StackPanel categoryField = UiKit.Field(Loc.T("tasks.category_label"), categoryCombo, null);
            Grid.SetColumn(categoryField, 0);
            metaRow.Children.Add(categoryField);
            StackPanel priorityField = UiKit.Field(Loc.T("tasks.priority_label"), priorityCombo, null);
            priorityField.Margin = new Thickness(10, 0, 0, 12);
            Grid.SetColumn(priorityField, 1);
            metaRow.Children.Add(priorityField);
            basicPanel.Children.Add(metaRow);

            StackPanel deadlineField = new StackPanel();
            deadlineField.Children.Add(hasDeadline);
            deadlineField.Children.Add(dueRow);
            deadlineField.Children.Add(quickButtons);
            basicPanel.Children.Add(UiKit.Field(Loc.T("tasks.deadline_label"), deadlineField, null));

            Grid twoCol = new Grid();
            twoCol.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            twoCol.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            StackPanel leadField = UiKit.Field(Loc.T("tasks.lead_time"), leadCombo, Loc.T("tasks.lead_time_hint"));
            Grid.SetColumn(leadField, 0);
            twoCol.Children.Add(leadField);
            StackPanel progressField = UiKit.Field(Loc.T("common.progress"), progressSlider, null);
            progressField.Margin = new Thickness(10, 0, 0, 12);
            progressSlider.Margin = new Thickness(0, 0, 6, 0);
            progressField.Children.Add(progressLabel);
            Grid.SetColumn(progressField, 1);
            twoCol.Children.Add(progressField);
            basicPanel.Children.Add(twoCol);

            basics.Content = new ScrollViewer { Content = basicPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            tabs.Items.Add(basics);

            TabItem advanced = new TabItem();
            advanced.Header = Loc.T("tasks.advanced_record");
            StackPanel advancedPanel = new StackPanel();
            advancedPanel.Margin = new Thickness(2, 12, 2, 2);
            advancedPanel.Children.Add(UiKit.Caption(Loc.T("tasks.markdown_hint")));
            advancedPanel.Children.Add(markdown);
            advancedPanel.Children.Add(attachmentSection);
            advanced.Content = new ScrollViewer { Content = advancedPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            tabs.Items.Add(advanced);

            TabItem links = new TabItem();
            links.Header = Loc.T("tasks.link_competition");
            StackPanel linkPanel = new StackPanel();
            linkPanel.Margin = new Thickness(2, 12, 2, 2);
            linkPanel.Children.Add(UiKit.Field(Loc.T("tasks.select_competitions"), competitionChips, Loc.T("tasks.link_competition_hint")));
            linkPanel.Children.Add(UiKit.Field(Loc.T("tasks.tags_label"), tagsBox, Loc.T("tasks.tags_label")));
            links.Content = new ScrollViewer { Content = linkPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            tabs.Items.Add(links);

            if (startSection == "advanced") { tabs.SelectedIndex = 1; }

            bool confirmed = UiKit.ShowDialog(owner, isNew ? Loc.T("tasks.new_todo") : Loc.T("common.edit"),
                tabs, Loc.T("common.save"), Loc.T("common.cancel"), delegate
                {
                    string title = (titleBox.Text ?? "").Trim();
                    if (title.Length == 0)
                    {
                        MessageBox.Show(owner, Loc.T("tasks.quick_add_placeholder"), Loc.T("error.generic"),
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        tabs.SelectedIndex = 0;
                        return false;
                    }
                    working.Title = title;
                    working.CategoryId = UiKit.SelectedKey(categoryCombo) ?? Category.CreateUncategorized().Id;
                    int priority;
                    if (int.TryParse(UiKit.SelectedKey(priorityCombo), out priority)) { working.Priority = priority; }
                    working.Progress = progressSlider.Value;
                    int lead;
                    if (int.TryParse(UiKit.SelectedKey(leadCombo), out lead)) { working.LeadMinutes = lead; }

                    if (hasDeadline.IsChecked == true)
                    {
                        DateTime day = dueDatePicker.SelectedDate.HasValue ? dueDatePicker.SelectedDate.Value : DateTime.Now.Date;
                        working.DueAt = IsoTime.Format(dueTime.Compose(day));
                    }
                    else
                    {
                        working.DueAt = "";
                    }

                    working.NoteMarkdown = markdown.Text;
                    if (!string.IsNullOrEmpty(working.NoteMarkdown)) { working.NoteUpdatedAt = IsoTime.Now(); }

                    working.Tags = SplitTags(tagsBox.Text);
                    working.CompetitionIds.Clear();
                    foreach (KeyValuePair<string, CheckBox> pair in competitionChecks)
                    {
                        if (pair.Value.IsChecked == true) { working.CompetitionIds.Add(pair.Key); }
                    }

                    if (working.Progress >= 1 && working.IsOpen)
                    {
                        working.Progress = 0.99;
                    }
                    AppState.Repo.SaveTodo(working, isNew
                        ? Loc.T("tasks.new_todo") + ": " + working.Title
                        : Loc.T("common.edit") + ": " + working.Title);
                    AppState.Commit();
                    return true;
                }, false);

            if (!confirmed)
            {
                // Roll back attachments that were imported for a todo the user then abandoned.
                if (isNew && !string.IsNullOrEmpty(working.Id))
                {
                    AppState.Repo.DeleteTodo(working.Id);
                    AppState.Commit();
                }
            }
        }

        /// <summary>
        /// Files can be imported before the user confirms, so the todo row must exist first.
        /// A draft todo is written immediately and removed if the dialog is cancelled.
        /// </summary>
        private static void EnsurePersisted(Todo working, List<Attachment> attachments, string title)
        {
            if (!string.IsNullOrEmpty(working.Id)) { return; }
            working.Title = string.IsNullOrEmpty(title) ? Loc.T("common.untitled") : title;
            working.CreatedAt = IsoTime.Now();
            AppState.Repo.SaveTodo(working, null);
        }

        private static Button QuickDate(string label, System.Windows.Controls.DatePicker picker, TimeField time, DateTime value)
        {
            // UiKit.Chip, not UiKit.Button("Button.Chip"): the Button.Chip style targets ToggleButton,
            // so using it on a Button throws InvalidOperationException the first time the editor opens.
            Button button = UiKit.Chip(label, null);
            button.Click += delegate
            {
                picker.SelectedDate = value.Date;
            };
            button.MinHeight = 28;
            return button;
        }

        private static DateTime NextWeekend()
        {
            DateTime day = DateTime.Now.Date;
            int guard = 0;
            while (day.DayOfWeek != DayOfWeek.Saturday && guard < 7)
            {
                day = day.AddDays(1);
                guard++;
            }
            return day.AddHours(23).AddMinutes(59);
        }

        private static List<string> SplitTags(string text)
        {
            List<string> tags = new List<string>();
            if (string.IsNullOrEmpty(text)) { return tags; }
            string[] parts = text.Split(new char[] { ',', '，', ';', '；' });
            for (int i = 0; i < parts.Length; i++)
            {
                string tag = parts[i].Trim();
                if (tag.Length > 0 && !tags.Contains(tag)) { tags.Add(tag); }
            }
            return tags;
        }

        /// <summary>One attachment row: icon, name, size, and rename / open / reveal / remove.</summary>
        private static UIElement AttachmentRow(Window owner, Attachment attachment, Action rebuild)
        {
            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Margin = new Thickness(0, 3, 0, 3);

            row.Children.Add(UiKit.Icon(attachment.IsFolder ? "folder" : "file", 16, UiKit.Brush("Brush.OnSurfaceVariant")));

            StackPanel text = new StackPanel();
            text.Margin = new Thickness(9, 0, 8, 0);
            TextBlock name = UiKit.Body(attachment.FileName);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            text.Children.Add(name);
            string meta = attachment.DisplaySize;
            if (!string.IsNullOrEmpty(attachment.Category)) { meta = attachment.Category + " · " + meta; }
            text.Children.Add(UiKit.Caption(meta));
            Grid.SetColumn(text, 1);
            row.Children.Add(text);

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.Children.Add(UiKit.IconButton("eye", Loc.T("common.open"), delegate
            {
                try { AppState.Repo.OpenAttachment(attachment); }
                catch (Exception ex)
                {
                    MessageBox.Show(owner, Loc.T("tasks.file_missing") + "\r\n" + ex.Message,
                        Loc.T("error.generic"), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }));
            actions.Children.Add(UiKit.IconButton("edit", Loc.T("common.rename"), delegate
            {
                string newName = Dialogs.PromptText(owner, Loc.T("tasks.rename_file"),
                    Loc.T("tasks.rename_file_prompt"), Path.GetFileNameWithoutExtension(attachment.FileName));
                if (string.IsNullOrEmpty(newName)) { return; }
                try
                {
                    Attachment renamed = AppState.Repo.RenameAttachment(attachment.Id, newName, !attachment.IsFolder);
                    attachment.FileName = renamed.FileName;
                    attachment.RelativePath = renamed.RelativePath;
                    AppState.Commit();
                    if (rebuild != null) { rebuild(); }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(owner, ex.Message, Loc.T("error.generic"), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }));
            actions.Children.Add(UiKit.IconButton("folder_open", Loc.T("tasks.reveal_in_explorer"), delegate
            {
                try { AppState.Repo.RevealAttachment(attachment); }
                catch { }
            }));
            actions.Children.Add(UiKit.IconButton("trash", Loc.T("tasks.remove_file"), delegate
            {
                if (!UiKit.Confirm(owner, Loc.T("common.delete"), attachment.FileName + "\r\n" + Loc.T("common.are_you_sure")))
                {
                    return;
                }
                AppState.Repo.DeleteAttachment(attachment.Id, true);
                AppState.Commit();
                if (rebuild != null) { rebuild(); }
            }));
            Grid.SetColumn(actions, 2);
            row.Children.Add(actions);
            return row;
        }
    }
}
