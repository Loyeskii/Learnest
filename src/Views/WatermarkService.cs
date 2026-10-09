using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Learnest.Views
{
    /// <summary>
    /// Attached property that renders placeholder text inside an empty TextBox and a matching
    /// adorner for PasswordBox.  Implemented with an adorner rather than a control template so it
    /// keeps working with the shared theme templates.
    /// </summary>
    internal static class WatermarkService
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.RegisterAttached("Text", typeof(string), typeof(WatermarkService),
                new PropertyMetadata(null, OnTextChanged));

        public static void Set(DependencyObject element, string value)
        {
            element.SetValue(TextProperty, value);
        }

        public static string Get(DependencyObject element)
        {
            return (string)element.GetValue(TextProperty);
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TextBox box = d as TextBox;
            if (box != null)
            {
                box.Loaded -= TextBoxLoaded;
                box.Loaded += TextBoxLoaded;
                if (box.IsLoaded) { AttachTextBox(box); }
            }
        }

        private static void TextBoxLoaded(object sender, RoutedEventArgs e)
        {
            AttachTextBox(sender as TextBox);
        }

        private static void AttachTextBox(TextBox box)
        {
            if (box == null) { return; }
            AdornerLayer layer = AdornerLayer.GetAdornerLayer(box);
            if (layer == null) { return; }
            Adorner[] existing = layer.GetAdorners(box);
            if (existing != null)
            {
                for (int i = 0; i < existing.Length; i++)
                {
                    if (existing[i] is TextBoxWatermarkAdorner) { return; }
                }
            }
            layer.Add(new TextBoxWatermarkAdorner(box));
        }

        private sealed class TextBoxWatermarkAdorner : Adorner
        {
            private readonly TextBox _box;
            private readonly VisualCollection _visuals;
            private readonly TextBlock _label;

            internal TextBoxWatermarkAdorner(TextBox box) : base(box)
            {
                _box = box;

                // Order matters: touching any force-inherit property (IsHitTestVisible included)
                // asks the visual tree for VisualChildrenCount, which reads _visuals.  Assigning the
                // collection after that call throws a NullReferenceException the moment a watermarked
                // TextBox is rendered - which is exactly what happened on the first window in the
                // real render pass while the headless checks stayed green.
                _visuals = new VisualCollection(this);
                _label = new TextBlock();
                _label.Foreground = UiKit.Brush("Brush.OnSurfaceFaint");
                _label.FontFamily = UiKit.UiFont;
                _label.FontSize = box.FontSize > 0 ? box.FontSize : 13;
                _label.VerticalAlignment = VerticalAlignment.Center;
                _label.Text = Get(box);
                _visuals.Add(_label);

                IsHitTestVisible = false;

                _box.TextChanged += OnTextChanged;
                _box.GotFocus += OnStateChanged;
                _box.LostFocus += OnStateChanged;
                Update();
            }

            private void OnTextChanged(object sender, TextChangedEventArgs e) { Update(); }
            private void OnStateChanged(object sender, RoutedEventArgs e) { Update(); }

            private void Update()
            {
                string text = Get(_box);
                _label.Text = text ?? "";
                Visibility = string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;
            }

            protected override int VisualChildrenCount { get { return _visuals.Count; } }

            protected override Visual GetVisualChild(int index) { return _visuals[index]; }

            protected override Size MeasureOverride(Size constraint)
            {
                _label.Measure(constraint);
                return _box.RenderSize;
            }

            protected override Size ArrangeOverride(Size finalSize)
            {
                Thickness padding = _box.Padding;
                double left = padding.Left + _box.BorderThickness.Left + 1;
                double top = Math.Max(0, (finalSize.Height - _label.DesiredSize.Height) / 2);
                _label.Arrange(new Rect(left, top, Math.Max(0, finalSize.Width - left - 6), _label.DesiredSize.Height));
                return finalSize;
            }
        }
    }
}
