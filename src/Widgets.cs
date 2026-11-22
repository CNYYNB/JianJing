using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace XinCleaner
{
    static class W
    {
        /// small rotating ring
        public static FrameworkElement Spinner(double size = 14)
        {
            var g = new Grid { Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center };
            g.Children.Add(new Ellipse { Stroke = Ui.Br("#D8DDE4"), StrokeThickness = 1.6 });
            var arc = new Path
            {
                Data = Geometry.Parse($"M{size / 2},0.8 A{size / 2 - 0.8},{size / 2 - 0.8} 0 0 1 {size - 0.8},{size / 2}"),
                Stroke = Ui.Accent, StrokeThickness = 1.8, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                RenderTransformOrigin = new Point(0.5, 0.5),
            };
            var rt = new RotateTransform(); arc.RenderTransform = rt;
            rt.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.9)) { RepeatBehavior = RepeatBehavior.Forever });
            g.Children.Add(arc);
            return g;
        }

        public static FrameworkElement Waiting(double size = 14) =>
            new Ellipse { Width = size, Height = size, Stroke = Ui.Br("#9AA1AB"), StrokeThickness = 1.4, VerticalAlignment = VerticalAlignment.Center };

        public static FrameworkElement Done(double size = 16)
        {
            var g = new Grid { Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center };
            g.Children.Add(new Ellipse { Fill = Ui.GreenSoft });
            g.Children.Add(new Viewbox { Width = size * 0.62, Height = size * 0.62, Child = Ui.IconRaw("check", Brushes.White, 3.2) });
            return g;
        }

        public static StackPanel Status(FrameworkElement icon, string text)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(icon);
            var t = Ui.Text(text, 13, Ui.Gray); t.Margin = new Thickness(8, 0, 0, 1);
            sp.Children.Add(t);
            return sp;
        }

        /// ring gauge 0..1
        public static Grid Ring(double value, double size, Brush color, string big, string small)
        {
            var g = new Grid { Width = size, Height = size };
            double th = size * 0.085, r = (size - th) / 2, c = size / 2;
            g.Children.Add(new Ellipse { Stroke = Ui.Br("#E3EFE7"), StrokeThickness = th, Margin = new Thickness(th / 2 - th / 2) });
            double a = 2 * Math.PI * Math.Clamp(value, 0.001, 0.999);
            var end = new Point(c + r * Math.Sin(a), c - r * Math.Cos(a));
            var fig = new PathFigure { StartPoint = new Point(c, c - r) };
            fig.Segments.Add(new ArcSegment(end, new Size(r, r), 0, a > Math.PI, SweepDirection.Clockwise, true));
            g.Children.Add(new Path { Data = new PathGeometry(new[] { fig }), Stroke = color, StrokeThickness = th, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            var b = Ui.Text(big, size * 0.3, color, FontWeights.SemiBold); b.HorizontalAlignment = HorizontalAlignment.Center;
            var s = Ui.Text(small, 11, Ui.Gray); s.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(b); sp.Children.Add(s);
            g.Children.Add(sp);
            return g;
        }

        public static Border HeaderStrip(string text) => new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)), BorderBrush = Ui.Line, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(14, 7, 14, 7), Child = Ui.Text(text, 12, Ui.Gray),
        };
    }

    /// Lists exactly which files will be deleted.
    class PreviewWindow : Window
    {
        public PreviewWindow(string title, List<(string section, int total, long bytes, List<(string path, long size)> items)> data, string hintText = null)
        {
            Title = title; Width = 820; Height = 620; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Owner = Application.Current.MainWindow; Background = Ui.Br("#F7F8FA");
            FontFamily = (FontFamily)Application.Current.FindResource("UiFont");
            var sp = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
            sp.Children.Add(Ui.Text(title, 20, Ui.Ink, FontWeights.SemiBold));
            var hint = Ui.Text(hintText ?? "下面是会被删除的文件。正在使用的文件会自动跳过。每类只列出最大的 300 个。", 12.5, Ui.Gray);
            hint.Margin = new Thickness(0, 4, 0, 10);
            sp.Children.Add(hint);
            foreach (var (section, total, bytes, items) in data)
            {
                sp.Children.Add(Ui.Section($"{section} · {total} 个 · {Fmt.Size(bytes)}", 14));
                var list = new StackPanel();
                if (items.Count == 0) list.Children.Add(Ui.Text(total == 0 ? "没有要删的文件" : "这一类按整体处理（例如清空回收站）", 12.5, Ui.Gray).Also(t => t.Margin = new Thickness(0, 8, 0, 8)));
                foreach (var (p, s) in items)
                {
                    var row = Ui.Cols((Ui.Text(p, 12, Ui.Ink).Also(t => { t.TextWrapping = TextWrapping.NoWrap; t.TextTrimming = TextTrimming.CharacterEllipsis; t.ToolTip = p; }), Ui.Star()),
                                      (Ui.Text(Fmt.Size(s), 12, Ui.Gray).Also(t => t.Margin = new Thickness(12, 0, 0, 0)), Ui.Auto));
                    row.Margin = new Thickness(0, 3, 0, 3);
                    list.Children.Add(row);
                }
                sp.Children.Add(Ui.Card(list, new Thickness(14, 8, 14, 8)));
            }
            Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }
    }

    static class FluentExt
    {
        public static T Also<T>(this T o, Action<T> a) { a(o); return o; }
    }
}
