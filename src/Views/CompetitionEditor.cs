using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Learnest.Data;
using Learnest.Services;
using Learnest.Theming;

using Learnest.Core;
namespace Learnest.Views
{
    /// <summary>
    /// Create or edit a competition: the identity (name, tag, colour, stage, dates), the team and
    /// links, a markdown brief and the attachment categories used for the deliverable files.
    /// </summary>
    internal static class CompetitionEditor
    {
        private static readonly string[] Palette = new string[]
        {
            "#8B5CF6", "#6366F1", "#0EA5E9", "#14B8A6", "#22C55E", "#F59E0B",
            "#EF4444", "#EC4899", "#A855F7", "#64748B", "#0F766E", "#B45309"
        };

        public static void ShowCreate(IShell shell)
        {
            ShowCreate(shell, null);
        }

        public static void ShowCreate(IShell shell, Action<string> onSaved)
        {
            Show(shell, null, onSaved);
        }

        public static void ShowEdit(IShell shell, Competition competition)
        {
            Show(shell, competition, null);
        }

        private static void Show(IShell shell, Competition existing, Action<string> onSaved)
        {
            Window owner = shell != null ? shell.Window : null;
            bool isNew = existing == null;
            Competition working = isNew ? new Competition() : existing;
            string selectedColor = string.IsNullOrEmpty(working.Color) ? Palette[0] : working.Color;

            TextBox nameBox = UiKit.Input(working.Name, Loc.T("comp.name"));
            TextBox tagBox = UiKit.Input(working.Tag, string.IsNullOrEmpty(working.Name) ? "mathmodelling" : working.Name);
            TextBox organizerBox = UiKit.Input(working.Organizer, Loc.T("comp.organizer"));
            TextBox levelBox = UiKit.Input(working.Level, Loc.T("comp.level"));
            TextBox teamBox = UiKit.Input(string.Join(", ", working.Team.ToArray()), Loc.T("comp.team"));
            TextBox linksBox = UiKit.MultiLineInput(string.Join(Environment.NewLine, working.Links.ToArray()), 70);
            TextBox categoriesBox = UiKit.Input(string.Join(", ", working.AttachmentCategories.ToArray()),
                Loc.T("comp.file_category"));

            ComboBox stageCombo = UiKit.Combo(new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("planned", Loc.T("comp.stage_planned")),
                new KeyValuePair<string, string>("registered", Loc.T("comp.stage_registered")),
                new KeyValuePair<string, string>("in_progress", Loc.T("comp.stage_in_progress")),
                new KeyValuePair<string, string>("submitted", Loc.T("comp.stage_submitted")),
                new KeyValuePair<string, string>("finished", Loc.T("comp.stage_finished")),
                new KeyValuePair<string, string>("archived", Loc.T("comp.stage_archived"))
            }, working.Stage);

            DateTime startSeed;
            if (!IsoTime.TryParse(working.StartAt, out startSeed)) { startSeed = DateTime.Now.Date; }
            DateTime endSeed;
            if (!IsoTime.TryParse(working.EndAt, out endSeed)) { endSeed = DateTime.Now.Date.AddMonths(2); }

            System.Windows.Controls.DatePicker startDate = new System.Windows.Controls.DatePicker();
            startDate.SelectedDate = startSeed.Date;
            startDate.Style = UiKit.Style("DatePicker");
            System.Windows.Controls.DatePicker endDate = new System.Windows.Controls.DatePicker();
            endDate.SelectedDate = endSeed.Date;
            endDate.Style = UiKit.Style("DatePicker");

            Slider progress = new Slider();
            progress.Minimum = 0;
            progress.Maximum = 1;
            progress.Value = working.Progress;
            TextBlock progressLabel = UiKit.Caption((working.Progress * 100).ToString("0") + "%");
            progress.ValueChanged += delegate { progressLabel.Text = (progress.Value * 100).ToString("0") + "%"; };

            TextBox descriptionBox = UiKit.MultiLineInput(working.Description, 70);

            Border previewHost = new Border();
            previewHost.Padding = new Thickness(12);
            previewHost.CornerRadius = new CornerRadius(11);
            previewHost.Background = UiKit.Brush("Brush.SurfaceContainer");
            previewHost.BorderThickness = new Thickness(1);
            previewHost.BorderBrush = UiKit.Brush("Brush.OutlineVariant");
            MarkdownEditor markdown = new MarkdownEditor(working.NoteMarkdown, previewHost, true);

            // Colour palette.
            WrapPanel swatches = new WrapPanel();
            List<Border> chips = new List<Border>();
            for (int i = 0; i < Palette.Length; i++)
            {
                string hex = Palette[i];
                Border chip = new Border();
                chip.Width = 26;
                chip.Height = 26;
                chip.CornerRadius = new CornerRadius(8);
                chip.Margin = new Thickness(0, 0, 6, 6);
                chip.Cursor = System.Windows.Input.Cursors.Hand;
                chip.Background = new SolidColorBrush(ColorUtil.Parse(hex, Colors.MediumPurple));
                chip.BorderThickness = new Thickness(2);
                chip.BorderBrush = hex == selectedColor ? UiKit.Brush("Brush.OnSurface") : Brushes.Transparent;
                chip.Tag = hex;
                chip.MouseLeftButtonUp += delegate(object sender, System.Windows.Input.MouseButtonEventArgs e)
                {
                    selectedColor = (string)((Border)sender).Tag;
                    for (int j = 0; j < chips.Count; j++)
                    {
                        chips[j].BorderBrush = (string)chips[j].Tag == selectedColor
                            ? UiKit.Brush("Brush.OnSurface") : Brushes.Transparent;
                    }
                };
                chips.Add(chip);
                swatches.Children.Add(chip);
            }

            TabControl tabs = new TabControl();
            tabs.MinHeight = 380;

            TabItem basics = new TabItem();
            basics.Header = Loc.T("comp.overview");
            StackPanel basicPanel = new StackPanel();
            basicPanel.Margin = new Thickness(2, 12, 2, 2);
            basicPanel.Children.Add(UiKit.Field(Loc.T("comp.name"), nameBox, null));
            basicPanel.Children.Add(UiKit.Field(Loc.T("comp.tag"), tagBox, Loc.T("comp.tag_hint")));

            Grid row1 = new Grid();
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            StackPanel stageField = UiKit.Field(Loc.T("comp.stage"), stageCombo, null);
            Grid.SetColumn(stageField, 0);
            row1.Children.Add(stageField);
            StackPanel levelField = UiKit.Field(Loc.T("comp.level"), levelBox, null);
            levelField.Margin = new Thickness(10, 0, 0, 12);
            Grid.SetColumn(levelField, 1);
            row1.Children.Add(levelField);
            basicPanel.Children.Add(row1);

            Grid row2 = new Grid();
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            StackPanel startField = UiKit.Field(Loc.T("common.start"), startDate, null);
            Grid.SetColumn(startField, 0);
            row2.Children.Add(startField);
            StackPanel endField = UiKit.Field(Loc.T("common.end"), endDate, null);
            endField.Margin = new Thickness(10, 0, 0, 12);
            Grid.SetColumn(endField, 1);
            row2.Children.Add(endField);
            basicPanel.Children.Add(row2);

            basicPanel.Children.Add(UiKit.Field(Loc.T("comp.organizer"), organizerBox, null));
            basicPanel.Children.Add(UiKit.Field(Loc.T("comp.team"), teamBox, Loc.T("common.optional")));

            StackPanel progressField = UiKit.Field(Loc.T("common.progress"), progress, null);
            progressField.Children.Add(progressLabel);
            basicPanel.Children.Add(progressField);

            basicPanel.Children.Add(UiKit.Field(Loc.T("comp.color"), swatches, null));
            basics.Content = new ScrollViewer { Content = basicPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            tabs.Items.Add(basics);

            TabItem detail = new TabItem();
            detail.Header = Loc.T("common.description");
            StackPanel detailPanel = new StackPanel();
            detailPanel.Margin = new Thickness(2, 12, 2, 2);
            detailPanel.Children.Add(UiKit.Field(Loc.T("common.description"), descriptionBox, null));
            detailPanel.Children.Add(UiKit.Field(Loc.T("comp.links"), linksBox, Loc.T("comp.link_add")));
            detailPanel.Children.Add(UiKit.Field(Loc.T("comp.file_category"), categoriesBox, Loc.T("comp.file_category_new")));
            detail.Content = new ScrollViewer { Content = detailPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            tabs.Items.Add(detail);

            TabItem worklog = new TabItem();
            worklog.Header = Loc.T("comp.worklog");
            StackPanel worklogPanel = new StackPanel();
            worklogPanel.Margin = new Thickness(2, 12, 2, 2);
            worklogPanel.Children.Add(UiKit.Caption(Loc.T("tasks.markdown_hint")));
            worklogPanel.Children.Add(markdown);
            worklog.Content = new ScrollViewer { Content = worklogPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            tabs.Items.Add(worklog);

            bool saved = UiKit.ShowDialog(owner, isNew ? Loc.T("comp.new") : Loc.T("comp.edit"), tabs,
                Loc.T("common.save"), Loc.T("common.cancel"), delegate
                {
                    string name = (nameBox.Text ?? "").Trim();
                    if (name.Length == 0)
                    {
                        MessageBox.Show(owner, Loc.T("comp.name"), Loc.T("error.generic"), MessageBoxButton.OK, MessageBoxImage.Warning);
                        tabs.SelectedIndex = 0;
                        return false;
                    }
                    working.Name = name;
                    working.Tag = (tagBox.Text ?? "").Trim();
                    if (working.Tag.Length == 0) { working.Tag = NameToTag(name); }
                    working.Organizer = (organizerBox.Text ?? "").Trim();
                    working.Level = (levelBox.Text ?? "").Trim();
                    working.Stage = UiKit.SelectedKey(stageCombo) ?? "planned";
                    working.Color = selectedColor;
                    working.Team = SplitList(teamBox.Text);
                    working.Links = SplitLines(linksBox.Text);
                    working.AttachmentCategories = SplitList(categoriesBox.Text);
                    working.Description = (descriptionBox.Text ?? "").Trim();
                    working.NoteMarkdown = markdown.Text;
                    working.Progress = progress.Value;
                    if (startDate.SelectedDate.HasValue)
                    {
                        working.StartAt = IsoTime.Format(startDate.SelectedDate.Value.Date.AddHours(9));
                    }
                    if (endDate.SelectedDate.HasValue)
                    {
                        working.EndAt = IsoTime.Format(endDate.SelectedDate.Value.Date.AddHours(23).AddMinutes(59));
                    }
                    AppState.Repo.SaveCompetition(working);
                    AppState.Commit();
                    if (onSaved != null) { onSaved(working.Id); }
                    return true;
                }, false);

            if (!saved && isNew && !string.IsNullOrEmpty(working.Id))
            {
                AppState.Repo.DeleteCompetition(working.Id, false);
                AppState.Commit();
            }
        }

        /// <summary>Derive a tag from the name so todos always have something to reference.</summary>
        public static string NameToTag(string name)
        {
            if (string.IsNullOrEmpty(name)) { return "competition"; }
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length && sb.Length < 24; i++)
            {
                char c = name[i];
                if (char.IsLetterOrDigit(c)) { sb.Append(c); }
                else if (c == ' ' || c == '-' || c == '_') { sb.Append('-'); }
            }
            string tag = sb.ToString().Trim('-');
            return tag.Length == 0 ? "competition" : tag;
        }

        private static List<string> SplitList(string text)
        {
            List<string> items = new List<string>();
            if (string.IsNullOrEmpty(text)) { return items; }
            string[] parts = text.Split(new char[] { ',', '，', ';', '；', '\n' });
            for (int i = 0; i < parts.Length; i++)
            {
                string value = parts[i].Trim();
                if (value.Length > 0 && !items.Contains(value)) { items.Add(value); }
            }
            return items;
        }

        private static List<string> SplitLines(string text)
        {
            List<string> items = new List<string>();
            if (string.IsNullOrEmpty(text)) { return items; }
            string[] parts = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < parts.Length; i++)
            {
                string value = parts[i].Trim();
                if (value.Length > 0) { items.Add(value); }
            }
            return items;
        }
    }
}
