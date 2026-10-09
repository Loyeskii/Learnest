using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Learnest.Services;

namespace Learnest.Views
{
    /// <summary>
    /// The "advanced record" editor: a markdown source box with a live preview.
    ///
    /// The renderer supports the subset a student notebook actually needs - headings, bold,
    /// italic, inline code, fenced code blocks, bullet and numbered lists, markdown checklists,
    /// blockquotes, tables, horizontal rules and links - and it renders checklists as real
    /// (read-only) check boxes so a task breakdown is readable at a glance.
    /// </summary>
    internal sealed class MarkdownEditor : Grid
    {
        private readonly TextBox _source;
        private readonly Border _preview;
        private readonly TextBox _previewText;
        private readonly bool _sideBySide;

        public MarkdownEditor(string initialText, Border previewHost, bool sideBySide)
        {
            _sideBySide = sideBySide;

            _source = new TextBox();
            _source.Style = UiKit.Style("TextBox.Base");
            _source.AcceptsReturn = true;
            _source.AcceptsTab = true;
            _source.TextWrapping = TextWrapping.Wrap;
            _source.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _source.FontFamily = UiKit.UiFont;
            _source.MinHeight = 220;
            _source.Text = initialText ?? "";
            _source.TextChanged += delegate { RenderPreview(); };

            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel left = new StackPanel();
            left.Margin = new Thickness(0, 0, 6, 0);
            left.Children.Add(UiKit.Label(Loc.T("tasks.markdown_edit")));
            _source.Margin = new Thickness(0, 5, 0, 0);
            left.Children.Add(_source);
            Children.Add(left);

            ScrollViewer previewScroller = new ScrollViewer();
            previewScroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            previewScroller.Margin = new Thickness(6, 0, 0, 0);
            _previewText = new TextBox();
            _previewText.Style = UiKit.Style("TextBox.Base");
            _previewText.IsReadOnly = true;
            _previewText.AcceptsReturn = true;
            _previewText.TextWrapping = TextWrapping.Wrap;
            _previewText.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _previewText.MinHeight = 220;
            _previewText.Background = Brushes.Transparent;
            _previewText.BorderThickness = new Thickness(0);
            _previewText.Padding = new Thickness(0);
            _previewHost = previewHost;

            StackPanel right = new StackPanel();
            right.Margin = new Thickness(6, 0, 0, 0);
            right.Children.Add(UiKit.Label(Loc.T("tasks.markdown_preview")));
            _preview = previewHost;
            _preview.Margin = new Thickness(0, 5, 0, 0);
            _preview.Child = previewScroller;
            previewScroller.Content = _previewText;
            right.Children.Add(_preview);
            Grid.SetColumn(right, 1);
            Children.Add(right);

            RenderPreview();
        }

        private readonly Border _previewHost;

        public string Text
        {
            get { return _source.Text ?? ""; }
            set
            {
                _source.Text = value ?? "";
                RenderPreview();
            }
        }

        public TextBox SourceBox { get { return _source; } }

        /// <summary>Flatten the markdown into the read-only preview box.</summary>
        private void RenderPreview()
        {
            string markdown = _source.Text;
            if (string.IsNullOrEmpty(markdown))
            {
                _previewText.Text = Loc.T("tasks.markdown_hint");
                _previewText.Foreground = UiKit.Brush("Brush.OnSurfaceFaint");
                return;
            }
            _previewText.Foreground = UiKit.Brush("Brush.OnSurface");
            _previewText.Text = Markdown.ToPlainText(markdown);
        }

        public void FocusSource()
        {
            _source.Focus();
        }
    }

    /// <summary>
    /// Minimal markdown utilities: a plain-text projection for the live preview, a document
    /// builder for rich rendering, and helpers used by the lists (checklist extraction, first
    /// line summary).
    /// </summary>
    internal static class Markdown
    {
        /// <summary>Readable plain text rendering with aligned table columns and list bullets.</summary>
        public static string ToPlainText(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) { return ""; }
            string text = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = text.Split('\n');
            System.Text.StringBuilder output = new System.Text.StringBuilder();
            bool inCode = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();

                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    inCode = !inCode;
                    output.AppendLine(inCode ? "┌─ " + Loc.T("common.details") + " ─" : "└────");
                    continue;
                }
                if (inCode)
                {
                    output.AppendLine("  " + line);
                    continue;
                }

                if (trimmed.Length == 0)
                {
                    output.AppendLine();
                    continue;
                }

                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    int level = 0;
                    while (level < trimmed.Length && trimmed[level] == '#') { level++; }
                    string heading = trimmed.Substring(level).Trim();
                    output.AppendLine(heading.ToUpperInvariant());
                    output.AppendLine(new string('─', Math.Min(40, Math.Max(4, heading.Length))));
                    continue;
                }

                if (trimmed == "---" || trimmed == "***" || trimmed == "___")
                {
                    output.AppendLine(new string('─', 40));
                    continue;
                }

                if (trimmed.StartsWith(">", StringComparison.Ordinal))
                {
                    output.AppendLine("│ " + Inline(trimmed.TrimStart('>').Trim()));
                    continue;
                }

                // Lists, including markdown checklists.
                if (IsChecklist(trimmed))
                {
                    bool done = IsChecked(trimmed);
                    string body = ChecklistText(trimmed);
                    output.AppendLine((done ? "☑ " : "☐ ") + Inline(body));
                    continue;
                }
                if (IsBullet(trimmed))
                {
                    output.AppendLine("• " + Inline(trimmed.Substring(2).Trim()));
                    continue;
                }
                int number;
                string orderedText;
                if (TryOrdered(trimmed, out number, out orderedText))
                {
                    output.AppendLine(number + ". " + Inline(orderedText));
                    continue;
                }

                if (IsTableRow(trimmed))
                {
                    if (IsTableSeparator(trimmed)) { continue; }
                    string[] cells = SplitTableRow(trimmed);
                    System.Text.StringBuilder row = new System.Text.StringBuilder();
                    for (int c = 0; c < cells.Length; c++)
                    {
                        if (c > 0) { row.Append("  │  "); }
                        row.Append(Inline(cells[c]));
                    }
                    output.AppendLine(row.ToString());
                    continue;
                }

                output.AppendLine(Inline(trimmed));
            }
            return output.ToString().TrimEnd();
        }

        /// <summary>Strip inline markdown emphasis, code ticks and link syntax.</summary>
        public static string Inline(string text)
        {
            if (string.IsNullOrEmpty(text)) { return ""; }
            string result = text;
            result = System.Text.RegularExpressions.Regex.Replace(result, @"!\[([^\]]*)\]\([^)]*\)", "$1");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\[([^\]]*)\]\(([^)]*)\)", "$1 ($2)");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\*\*\*(.+?)\*\*\*", "$1");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\*\*(.+?)\*\*", "$1");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"__(.+?)__", "$1");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\*(.+?)\*", "$1");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"_([^_]+)_", "$1");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"~~(.+?)~~", "$1");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"`([^`]*)`", "$1");
            return result;
        }

        public static bool IsChecklist(string line)
        {
            return line.StartsWith("- [", StringComparison.Ordinal) ||
                   line.StartsWith("* [", StringComparison.Ordinal) ||
                   line.StartsWith("+ [", StringComparison.Ordinal);
        }

        public static bool IsChecked(string line)
        {
            if (line.Length < 5) { return false; }
            char mark = line[3];
            return mark == 'x' || mark == 'X';
        }

        public static string ChecklistText(string line)
        {
            if (line.Length <= 5) { return ""; }
            return line.Substring(5).Trim();
        }

        public static bool IsBullet(string line)
        {
            if (line.Length < 2) { return false; }
            char first = line[0];
            if (first != '-' && first != '*' && first != '+') { return false; }
            return line[1] == ' ';
        }

        public static bool TryOrdered(string line, out int number, out string text)
        {
            number = 0;
            text = null;
            int index = 0;
            while (index < line.Length && char.IsDigit(line[index])) { index++; }
            if (index == 0 || index + 1 >= line.Length) { return false; }
            if (line[index] != '.' && line[index] != ')') { return false; }
            if (line[index + 1] != ' ') { return false; }
            int parsed;
            if (!int.TryParse(line.Substring(0, index), out parsed)) { return false; }
            number = parsed;
            text = line.Substring(index + 2).Trim();
            return true;
        }

        public static bool IsTableRow(string line)
        {
            return line.StartsWith("|", StringComparison.Ordinal) && line.EndsWith("|", StringComparison.Ordinal) && line.Length > 2;
        }

        public static bool IsTableSeparator(string line)
        {
            string compact = line.Replace(" ", "").Replace("|", "").Replace(":", "");
            if (compact.Length == 0) { return false; }
            for (int i = 0; i < compact.Length; i++)
            {
                if (compact[i] != '-') { return false; }
            }
            return true;
        }

        public static string[] SplitTableRow(string line)
        {
            string inner = line.Trim();
            if (inner.StartsWith("|", StringComparison.Ordinal)) { inner = inner.Substring(1); }
            if (inner.EndsWith("|", StringComparison.Ordinal)) { inner = inner.Substring(0, inner.Length - 1); }
            string[] cells = inner.Split('|');
            for (int i = 0; i < cells.Length; i++) { cells[i] = cells[i].Trim(); }
            return cells;
        }

        public static int CountChecklistItems(string markdown, out int completed)
        {
            completed = 0;
            if (string.IsNullOrEmpty(markdown)) { return 0; }
            string[] lines = markdown.Replace("\r\n", "\n").Split('\n');
            int total = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (!IsChecklist(line)) { continue; }
                total++;
                if (IsChecked(line)) { completed++; }
            }
            return total;
        }

        public static bool ToggleChecklistLine(string markdown, int lineIndex)
        {
            return false;
        }

        /// <summary>Rich rendering into a FlowDocument, used for the read-only note viewer.</summary>
        public static FlowDocument BuildDocument(string markdown)
        {
            FlowDocument document = new FlowDocument();
            document.FontFamily = UiKit.UiFont;
            document.FontSize = 13;
            document.Foreground = UiKit.Brush("Brush.OnSurface");
            document.PagePadding = new Thickness(2);
            if (string.IsNullOrEmpty(markdown))
            {
                document.Blocks.Add(new Paragraph(new Run(Loc.T("tasks.no_attachments"))));
                return document;
            }

            string[] lines = markdown.Replace("\r\n", "\n").Split('\n');
            bool inCode = false;
            System.Text.StringBuilder code = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();

                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    if (inCode)
                    {
                        Paragraph block = new Paragraph(new Run(code.ToString()));
                        block.FontFamily = UiKit.MonoFont;
                        block.Background = UiKit.Brush("Brush.SurfaceContainerHigh");
                        block.Padding = new Thickness(10);
                        document.Blocks.Add(block);
                        code = null;
                    }
                    else
                    {
                        code = new System.Text.StringBuilder();
                    }
                    inCode = !inCode;
                    continue;
                }
                if (inCode)
                {
                    code.AppendLine(line);
                    continue;
                }
                if (trimmed.Length == 0) { continue; }

                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    int level = 0;
                    while (level < trimmed.Length && trimmed[level] == '#') { level++; }
                    Paragraph heading = new Paragraph(new Run(Inline(trimmed.Substring(level).Trim())));
                    heading.FontSize = Math.Max(14, 22 - level * 2);
                    heading.FontWeight = FontWeights.SemiBold;
                    heading.Margin = new Thickness(0, 10, 0, 4);
                    document.Blocks.Add(heading);
                    continue;
                }
                if (trimmed == "---" || trimmed == "***")
                {
                    document.Blocks.Add(new Paragraph(new Run("──────────")) { Foreground = UiKit.Brush("Brush.Outline") });
                    continue;
                }
                if (IsChecklist(trimmed))
                {
                    Paragraph item = new Paragraph();
                    item.Margin = new Thickness(6, 2, 0, 2);
                    item.Inlines.Add(new Run(IsChecked(trimmed) ? "☑ " : "☐ ") { FontSize = 15 });
                    item.Inlines.Add(new Run(Inline(ChecklistText(trimmed))));
                    if (IsChecked(trimmed)) { item.Foreground = UiKit.Brush("Brush.OnSurfaceVariant"); }
                    document.Blocks.Add(item);
                    continue;
                }
                int orderedIndex;
                string orderedBody;
                if (IsBullet(trimmed) || TryOrdered(trimmed, out orderedIndex, out orderedBody))
                {
                    Paragraph item = new Paragraph(new Run("•  " + Inline(Text(trimmed))));
                    item.Margin = new Thickness(10, 1, 0, 1);
                    document.Blocks.Add(item);
                    continue;
                }
                if (trimmed.StartsWith(">", StringComparison.Ordinal))
                {
                    Paragraph quote = new Paragraph(new Run(Inline(trimmed.TrimStart('>').Trim())));
                    quote.BorderBrush = UiKit.Brush("Brush.Primary");
                    quote.BorderThickness = new Thickness(3, 0, 0, 0);
                    quote.Padding = new Thickness(8, 0, 0, 0);
                    quote.Foreground = UiKit.Brush("Brush.OnSurfaceVariant");
                    document.Blocks.Add(quote);
                    continue;
                }

                document.Blocks.Add(new Paragraph(new Run(Inline(trimmed))) { Margin = new Thickness(0, 2, 0, 2) });
            }
            return document;
        }

        private static string Text(string trimmedLine)
        {
            int number;
            string text;
            if (TryOrdered(trimmedLine, out number, out text)) { return text; }
            if (trimmedLine.Length > 2) { return trimmedLine.Substring(2).Trim(); }
            return trimmedLine;
        }
    }
}
