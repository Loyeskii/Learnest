using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Learnest.Data;
using Learnest.Services;

using Learnest.Core;
namespace Learnest.Views
{
    /// <summary>Create or edit an examination record.</summary>
    internal sealed class ExamEditor
    {
        public static void ShowCreate(IShell shell)
        {
            Show(shell, null, DateTime.Now.Date.AddDays(7).AddHours(9));
        }

        public static void ShowCreateAt(IShell shell, DateTime when)
        {
            Show(shell, null, when);
        }

        public static void ShowEdit(IShell shell, Exam exam)
        {
            Show(shell, exam, exam != null ? exam.StartDate : DateTime.Now);
        }

        private static void Show(IShell shell, Exam existing, DateTime seed)
        {
            Window owner = shell != null ? shell.Window : null;
            bool isNew = existing == null;
            Exam working = isNew ? new Exam() : existing;

            TextBox titleBox = UiKit.Input(working.Title, Loc.T("exams.title"));
            TextBox courseBox = UiKit.Input(working.Course, Loc.T("exams.course"));
            TextBox locationBox = UiKit.Input(working.Location, Loc.T("exams.location"));
            TextBox seatBox = UiKit.Input(working.Seat, Loc.T("exams.seat"));

            List<KeyValuePair<string, string>> categoryChoices = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < AppState.Data.Categories.Count; i++)
            {
                Category category = AppState.Data.Categories[i];
                categoryChoices.Add(new KeyValuePair<string, string>(category.Id, category.Name));
            }
            ComboBox categoryCombo = UiKit.Combo(categoryChoices, working.CategoryId);

            DateTime startSeed = isNew ? seed : working.StartDate;
            System.Windows.Controls.DatePicker startDate = new System.Windows.Controls.DatePicker();
            startDate.SelectedDate = startSeed.Date;
            startDate.Style = UiKit.Style("DatePicker");
            TimeField startTime = new TimeField(startSeed, 5);

            DateTime endSeed = isNew ? seed.AddHours(2) : (working.EndDate.HasValue ? working.EndDate.Value : working.StartDate.AddHours(2));
            System.Windows.Controls.DatePicker endDate = new System.Windows.Controls.DatePicker();
            endDate.SelectedDate = endSeed.Date;
            endDate.Style = UiKit.Style("DatePicker");
            TimeField endTime = new TimeField(endSeed, 5);

            CheckBox hasEnd = new CheckBox();
            hasEnd.Content = Loc.T("common.end");
            hasEnd.IsChecked = !isNew || true;

            ComboBox statusCombo = UiKit.Combo(new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("planned", Loc.T("exams.status_planned")),
                new KeyValuePair<string, string>("done", Loc.T("exams.status_done")),
                new KeyValuePair<string, string>("cancelled", Loc.T("exams.status_cancelled"))
            }, working.Status);

            ComboBox remindCombo = UiKit.Combo(new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("60", "1 " + Loc.T("calendar.hour")),
                new KeyValuePair<string, string>("360", "6 " + Loc.T("calendar.hour")),
                new KeyValuePair<string, string>("1440", "1 " + Loc.T("timeline.day")),
                new KeyValuePair<string, string>("4320", "3 " + Loc.T("timeline.day")),
                new KeyValuePair<string, string>("10080", "7 " + Loc.T("timeline.day"))
            }, working.RemindMinutes.ToString());

            TextBox scoreBox = UiKit.Input(working.Score, Loc.T("exams.score"));

            Border previewHost = new Border();
            previewHost.Padding = new Thickness(12);
            previewHost.CornerRadius = new CornerRadius(11);
            previewHost.Background = UiKit.Brush("Brush.SurfaceContainer");
            previewHost.BorderThickness = new Thickness(1);
            previewHost.BorderBrush = UiKit.Brush("Brush.OutlineVariant");
            MarkdownEditor markdown = new MarkdownEditor(working.NoteMarkdown, previewHost, true);

            StackPanel attachments = new StackPanel();
            List<Attachment> files = new List<Attachment>();
            for (int i = 0; i < working.Attachments.Count; i++) { files.Add(working.Attachments[i]); }
            Action rebuild = null;
            rebuild = delegate
            {
                attachments.Children.Clear();
                if (files.Count == 0) { attachments.Children.Add(UiKit.Caption(Loc.T("tasks.no_attachments"))); }
                for (int i = 0; i < files.Count; i++)
                {
                    Attachment attachment = files[i];
                    Grid row = new Grid();
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.Children.Add(UiKit.Icon(attachment.IsFolder ? "folder" : "file", 15, UiKit.Brush("Brush.OnSurfaceVariant")));
                    StackPanel text = new StackPanel();
                    text.Margin = new Thickness(9, 0, 8, 0);
                    text.Children.Add(UiKit.Body(attachment.FileName));
                    text.Children.Add(UiKit.Caption(attachment.DisplaySize));
                    Grid.SetColumn(text, 1);
                    row.Children.Add(text);
                    StackPanel actions = new StackPanel();
                    actions.Orientation = Orientation.Horizontal;
                    actions.Children.Add(UiKit.IconButton("eye", Loc.T("common.open"), delegate
                    {
                        try { AppState.Repo.OpenAttachment(attachment); } catch { }
                    }));
                    actions.Children.Add(UiKit.IconButton("trash", Loc.T("common.delete"), delegate
                    {
                        AppState.Repo.DeleteAttachment(attachment.Id, true);
                        files.Remove(attachment);
                        AppState.Commit();
                        if (rebuild != null) { rebuild(); }
                    }));
                    Grid.SetColumn(actions, 2);
                    row.Children.Add(actions);
                    attachments.Children.Add(row);
                }
            };
            rebuild();

            Button importFiles = UiKit.Tonal(Loc.T("tasks.import_file"), delegate
            {
                string[] chosen = Dialogs.PickFiles(owner, Loc.T("tasks.import_file"), null, true);
                if (chosen.Length == 0) { return; }
                EnsurePersisted(working, titleBox.Text);
                for (int i = 0; i < chosen.Length; i++)
                {
                    try { files.Add(AppState.Repo.ImportAttachment(AttachmentOwner.Exam, working.Id, chosen[i], "复习资料", null)); }
                    catch (Exception ex)
                    {
                        MessageBox.Show(owner, ex.Message, Loc.T("error.generic"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                working.Attachments = files;
                rebuild();
            });

            // ------------------------------------------------------------ layout
            TabControl tabs = new TabControl();
            tabs.MinHeight = 380;

            TabItem basic = new TabItem();
            basic.Header = Loc.T("common.summary");
            StackPanel basicPanel = new StackPanel();
            basicPanel.Margin = new Thickness(2, 12, 2, 2);
            basicPanel.Children.Add(UiKit.Field(Loc.T("tasks.title_label"), titleBox, null));

            Grid courseRow = new Grid();
            courseRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            courseRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            StackPanel courseField = UiKit.Field(Loc.T("exams.course"), courseBox, null);
            Grid.SetColumn(courseField, 0);
            courseRow.Children.Add(courseField);
            StackPanel categoryField = UiKit.Field(Loc.T("common.category"), categoryCombo, null);
            categoryField.Margin = new Thickness(10, 0, 0, 12);
            Grid.SetColumn(categoryField, 1);
            courseRow.Children.Add(categoryField);
            basicPanel.Children.Add(courseRow);

            Grid startRow = new Grid();
            startRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            startRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            startRow.Children.Add(startDate);
            Grid.SetColumn(startTime, 1);
            startTime.Margin = new Thickness(8, 0, 0, 0);
            startRow.Children.Add(startTime);
            basicPanel.Children.Add(UiKit.Field(Loc.T("common.start"), startRow, null));

            Grid endRow = new Grid();
            endRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            endRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            endRow.Children.Add(endDate);
            Grid.SetColumn(endTime, 1);
            endTime.Margin = new Thickness(8, 0, 0, 0);
            endRow.Children.Add(endTime);
            basicPanel.Children.Add(UiKit.Field(Loc.T("common.end"), endRow, null));

            Grid placeRow = new Grid();
            placeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            placeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            StackPanel locationField = UiKit.Field(Loc.T("exams.location"), locationBox, null);
            Grid.SetColumn(locationField, 0);
            placeRow.Children.Add(locationField);
            StackPanel seatField = UiKit.Field(Loc.T("exams.seat"), seatBox, null);
            seatField.Margin = new Thickness(10, 0, 0, 12);
            Grid.SetColumn(seatField, 1);
            placeRow.Children.Add(seatField);
            basicPanel.Children.Add(placeRow);

            Grid stateRow = new Grid();
            stateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            stateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            StackPanel statusField = UiKit.Field(Loc.T("common.status"), statusCombo, null);
            Grid.SetColumn(statusField, 0);
            stateRow.Children.Add(statusField);
            StackPanel scoreField = UiKit.Field(Loc.T("exams.score"), scoreBox, null);
            scoreField.Margin = new Thickness(10, 0, 0, 12);
            Grid.SetColumn(scoreField, 1);
            stateRow.Children.Add(scoreField);
            basicPanel.Children.Add(stateRow);

            basicPanel.Children.Add(UiKit.Field(Loc.T("exams.remind_before", ""), remindCombo, null));
            basic.Content = new ScrollViewer { Content = basicPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            tabs.Items.Add(basic);

            TabItem record = new TabItem();
            record.Header = Loc.T("tasks.advanced_record");
            StackPanel recordPanel = new StackPanel();
            recordPanel.Margin = new Thickness(2, 12, 2, 2);
            recordPanel.Children.Add(UiKit.Caption(Loc.T("tasks.markdown_hint")));
            recordPanel.Children.Add(markdown);
            recordPanel.Children.Add(UiKit.SectionHeader(Loc.T("tasks.attachments_title"), importFiles));
            recordPanel.Children.Add(attachments);
            record.Content = new ScrollViewer { Content = recordPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            tabs.Items.Add(record);

            bool saved = UiKit.ShowDialog(owner, isNew ? Loc.T("exams.new_exam") : Loc.T("exams.edit_exam"), tabs,
                Loc.T("common.save"), Loc.T("common.cancel"), delegate
                {
                    string title = (titleBox.Text ?? "").Trim();
                    if (title.Length == 0)
                    {
                        MessageBox.Show(owner, Loc.T("exams.title"), Loc.T("error.generic"),
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        tabs.SelectedIndex = 0;
                        return false;
                    }
                    working.Title = title;
                    working.Course = (courseBox.Text ?? "").Trim();
                    working.CategoryId = UiKit.SelectedKey(categoryCombo) ?? "";
                    working.Location = (locationBox.Text ?? "").Trim();
                    working.Seat = (seatBox.Text ?? "").Trim();
                    working.Score = (scoreBox.Text ?? "").Trim();
                    working.Status = UiKit.SelectedKey(statusCombo) ?? "planned";
                    int remind;
                    if (int.TryParse(UiKit.SelectedKey(remindCombo), out remind)) { working.RemindMinutes = remind; }
                    DateTime day = startDate.SelectedDate.HasValue ? startDate.SelectedDate.Value : DateTime.Now.Date;
                    working.StartAt = IsoTime.Format(startTime.Compose(day));
                    if (hasEnd.IsChecked == true)
                    {
                        DateTime endDay = endDate.SelectedDate.HasValue ? endDate.SelectedDate.Value : day;
                        working.EndAt = IsoTime.Format(endTime.Compose(endDay));
                    }
                    else
                    {
                        working.EndAt = "";
                    }
                    working.NoteMarkdown = markdown.Text;
                    AppState.Repo.SaveExam(working);
                    AppState.Commit();
                    return true;
                }, false);

            if (!saved && isNew && !string.IsNullOrEmpty(working.Id))
            {
                AppState.Repo.DeleteExam(working.Id);
                AppState.Commit();
            }
        }

        private static void EnsurePersisted(Exam working, string title)
        {
            if (!string.IsNullOrEmpty(working.Id)) { return; }
            working.Title = string.IsNullOrEmpty(title) ? Loc.T("common.untitled") : title;
            working.CreatedAt = IsoTime.Now();
            if (string.IsNullOrEmpty(working.StartAt)) { working.StartAt = IsoTime.Format(DateTime.Now); }
            AppState.Repo.SaveExam(working);
        }
    }
}
