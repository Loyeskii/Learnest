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
    /// Create / edit / delete a category: name plus colour, with the palette presets the MD3 look
    /// expects.  Deleting a category that still holds todos asks what should happen to them.
    /// </summary>
    internal static class CategoryEditor
    {
        private static readonly string[] Palette = new string[]
        {
            "#2563EB", "#0EA5E9", "#14B8A6", "#22C55E", "#84CC16", "#EAB308",
            "#F59E0B", "#EF4444", "#EC4899", "#A855F7", "#6366F1", "#64748B"
        };

        public static void ShowCreate(IShell shell, Action onSaved)
        {
            Show(shell, null, onSaved);
        }

        public static void ShowEdit(IShell shell, Category category, Action onSaved)
        {
            Show(shell, category, onSaved);
        }

        private static void Show(IShell shell, Category existing, Action onSaved)
        {
            Window owner = shell != null ? shell.Window : null;
            bool isNew = existing == null;
            Category working = isNew ? new Category() : existing.Clone();
            if (isNew)
            {
                working.Color = Palette[0];
                working.Id = "";
            }
            string selectedColor = working.Color;

            StackPanel content = new StackPanel();
            TextBox nameBox = UiKit.Input(working.Name, Loc.T("tasks.category_name"));
            content.Children.Add(UiKit.Field(Loc.T("tasks.category_name"), nameBox, null));

            TextBox descriptionBox = UiKit.Input(working.Description, Loc.T("common.optional"));
            content.Children.Add(UiKit.Field(Loc.T("common.description"), descriptionBox, null));

            // Colour palette + a live preview swatch.
            Border preview = new Border();
            preview.Width = 30;
            preview.Height = 30;
            preview.CornerRadius = new CornerRadius(9);
            preview.Background = new SolidColorBrush(ColorUtil.Parse(selectedColor, Colors.SteelBlue));

            WrapPanel swatches = new WrapPanel();
            swatches.Margin = new Thickness(0, 4, 0, 0);
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
                chip.Background = new SolidColorBrush(ColorUtil.Parse(hex, Colors.SteelBlue));
                chip.BorderThickness = new Thickness(2);
                chip.BorderBrush = hex == selectedColor ? UiKit.Brush("Brush.OnSurface") : Brushes.Transparent;
                chip.Tag = hex;
                chip.MouseLeftButtonUp += delegate(object sender, System.Windows.Input.MouseButtonEventArgs e)
                {
                    selectedColor = (string)((Border)sender).Tag;
                    preview.Background = new SolidColorBrush(ColorUtil.Parse(selectedColor, Colors.SteelBlue));
                    for (int j = 0; j < chips.Count; j++)
                    {
                        chips[j].BorderBrush = (string)chips[j].Tag == selectedColor
                            ? UiKit.Brush("Brush.OnSurface") : Brushes.Transparent;
                    }
                };
                chips.Add(chip);
                swatches.Children.Add(chip);
            }

            Grid colourRow = new Grid();
            colourRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            colourRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            colourRow.Children.Add(preview);
            Grid.SetColumn(swatches, 1);
            swatches.Margin = new Thickness(12, 0, 0, 0);
            colourRow.Children.Add(swatches);
            content.Children.Add(UiKit.Field(Loc.T("tasks.category_color"), colourRow, null));

            // Deleting is only offered for user categories.
            if (!isNew && !working.IsUncategorized)
            {
                Button delete = UiKit.Button(Loc.T("common.delete"), "Button.Danger", delegate
                {
                    Window dialogOwner = owner;
                    int todoCount = AppState.Repo.CountTodosInCategory(working.Id);
                    if (todoCount > 0)
                    {
                        List<Category> others = AppState.Data.Categories;
                        Dialogs.CategoryDeleteRequest request = Dialogs.AskCategoryDeletion(dialogOwner, working, todoCount, others);
                        if (request.Choice == Dialogs.CategoryDeleteChoice.Cancel) { return; }
                        bool deleteTodos = request.Choice == Dialogs.CategoryDeleteChoice.DeleteTodos;
                        string target = request.Choice == Dialogs.CategoryDeleteChoice.MoveToOther
                            ? request.TargetCategoryId
                            : Category.CreateUncategorized().Id;
                        AppState.Repo.DeleteCategory(working.Id, deleteTodos, target);
                    }
                    else
                    {
                        if (!UiKit.Confirm(dialogOwner, Loc.T("common.delete"),
                                Loc.T("common.are_you_sure") + "\r\n" + working.Name))
                        {
                            return;
                        }
                        AppState.Repo.DeleteCategory(working.Id, false, Category.CreateUncategorized().Id);
                    }
                    AppState.Commit();
                    CloseDialogs(dialogOwner);
                    if (onSaved != null) { onSaved(); }
                });
                delete.HorizontalAlignment = HorizontalAlignment.Left;
                delete.Margin = new Thickness(0, 6, 0, 0);
                content.Children.Add(delete);
            }

            bool saved = UiKit.ShowDialog(owner, isNew ? Loc.T("tasks.category_new") : Loc.T("tasks.category_edit"),
                content, Loc.T("common.save"), Loc.T("common.cancel"), delegate
                {
                    string name = (nameBox.Text ?? "").Trim();
                    if (name.Length == 0)
                    {
                        MessageBox.Show(owner, Loc.T("tasks.category_name"), Loc.T("error.generic"),
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                    working.Name = name;
                    working.Description = (descriptionBox.Text ?? "").Trim();
                    working.Color = selectedColor;
                    if (string.IsNullOrEmpty(working.Id)) { working.Id = IsoTime.ShortId("cat"); }
                    AppState.Repo.SaveCategory(working);
                    AppState.Repo.Log(isNew ? "category.create" : "category.update", "category", working.Id,
                        (isNew ? "新建分类：" : "更新分类：") + working.Name);
                    AppState.Commit();
                    if (onSaved != null) { onSaved(); }
                    return true;
                }, false);

            if (!saved && working.IsUncategorized && onSaved != null)
            {
                // Nothing to do; kept for symmetry.
            }
        }

        private static void CloseDialogs(Window owner)
        {
            // The delete path closes the editor because the entity no longer exists.
            if (owner != null)
            {
                for (int i = 0; i < owner.OwnedWindows.Count; i++)
                {
                    Window child = owner.OwnedWindows[i];
                    if (child != null && child.IsActive) { child.DialogResult = false; }
                }
            }
        }
    }
}
