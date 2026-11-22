using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace XinCleaner
{
    static class Icons
    {
        public static readonly Dictionary<string, string> P = new()
        {
            ["home"] = "M3.5,10.5 L12,3.5 L20.5,10.5 L20.5,20.8 L15,20.8 L15,14.8 L9,14.8 L9,20.8 L3.5,20.8 Z",
            ["scan"] = "M17.5,11 A6.5,6.5 0 1 1 4.5,11 A6.5,6.5 0 1 1 17.5,11 Z M15.7,15.7 L20,20",
            ["pie"] = "M20.5,13 A8.5,8.5 0 1 1 11,3.5 L11,13 Z M14,3.6 A8.5,8.5 0 0 1 20.4,10 L14,10 Z",
            ["copy"] = "M10.5,8.5 L18.5,8.5 A2,2 0 0 1 20.5,10.5 L20.5,18.5 A2,2 0 0 1 18.5,20.5 L10.5,20.5 A2,2 0 0 1 8.5,18.5 L8.5,10.5 A2,2 0 0 1 10.5,8.5 Z M15.5,8.5 L15.5,5.5 A2,2 0 0 0 13.5,3.5 L5.5,3.5 A2,2 0 0 0 3.5,5.5 L3.5,13.5 A2,2 0 0 0 5.5,15.5 L8.5,15.5",
            ["apps"] = "M3.5,3.5 H10.5 V10.5 H3.5 Z M13.5,3.5 H20.5 V10.5 H13.5 Z M3.5,13.5 H10.5 V20.5 H3.5 Z M13.5,13.5 H20.5 V20.5 H13.5 Z",
            ["power"] = "M12,3 L12,11.5 M6.6,6.6 A7.6,7.6 0 1 0 17.4,6.6",
            ["shield"] = "M12,3.2 L19.5,6 L19.5,11.6 C19.5,16.2 16.3,19.4 12,20.8 C7.7,19.4 4.5,16.2 4.5,11.6 L4.5,6 Z",
            ["history"] = "M3.8,12 A8.2,8.2 0 1 0 6.2,6.2 L3.8,8.5 M3.8,4 L3.8,8.5 L8.3,8.5 M12,7.8 L12,12 L15,13.8",
            ["gear"] = "M15,12 A3,3 0 1 1 9,12 A3,3 0 1 1 15,12 Z M18.6,12 A6.6,6.6 0 1 1 5.4,12 A6.6,6.6 0 1 1 18.6,12 Z M12,2.8 L12,5.2 M12,18.8 L12,21.2 M2.8,12 L5.2,12 M18.8,12 L21.2,12",
            ["globe"] = "M20.5,12 A8.5,8.5 0 1 1 3.5,12 A8.5,8.5 0 1 1 20.5,12 Z M3.5,12 L20.5,12 M12,3.5 C14.6,5.9 14.6,18.1 12,20.5 M12,3.5 C9.4,5.9 9.4,18.1 12,20.5",
            ["file"] = "M6,3.5 L14,3.5 L18.5,8 L18.5,20.5 L5.5,20.5 L5.5,3.5 Z M14,3.5 L14,8 L18.5,8 M8.5,12.5 L15.5,12.5 M8.5,16 L13.5,16",
            ["folder"] = "M3.5,5.5 L9.3,5.5 L11.3,7.8 L20.5,7.8 L20.5,19.5 L3.5,19.5 Z",
            ["cube"] = "M12,3 L19.8,7.5 L19.8,16.5 L12,21 L4.2,16.5 L4.2,7.5 Z M4.2,7.5 L12,12 L19.8,7.5 M12,12 L12,21",
            ["trash"] = "M4,6.5 L20,6.5 M9,6.5 L9,4.3 L15,4.3 L15,6.5 M6,6.5 L7,20.5 L17,20.5 L18,6.5 M10,10.5 L10,16.5 M14,10.5 L14,16.5",
            ["chat"] = "M4,5.5 L20,5.5 L20,16 L9.5,16 L5.5,19.5 L5.5,16 L4,16 Z",
            ["image"] = "M3.5,4.5 H20.5 V19.5 H3.5 Z M3.5,17 L8.5,12.5 L12.5,16 L15.5,13.5 L20.5,17.5",
            ["leaf"] = "M5,19 C5,11 10,6 20,5 C19,15 14,20 6,19 Z M5,19 L13,11",
            ["list"] = "M8.5,7 L19.5,7 M8.5,12 L19.5,12 M8.5,17 L19.5,17 M4.5,7 L5,7 M4.5,12 L5,12 M4.5,17 L5,17",
            ["cpu"] = "M6,6 H18 V18 H6 Z M9.5,9.5 H14.5 V14.5 H9.5 Z M9.5,3 L9.5,6 M14.5,3 L14.5,6 M9.5,18 L9.5,21 M14.5,18 L14.5,21 M3,9.5 L6,9.5 M3,14.5 L6,14.5 M18,9.5 L21,9.5 M18,14.5 L21,14.5",
            ["mem"] = "M3,7 H21 V17 H3 Z M7,10.5 L7,13.5 M10.5,10.5 L10.5,13.5 M14,10.5 L14,13.5 M17.5,10.5 L17.5,13.5",
            ["disk"] = "M4.5,6 C4.5,4.5 7.9,3.2 12,3.2 C16.1,3.2 19.5,4.5 19.5,6 C19.5,7.5 16.1,8.8 12,8.8 C7.9,8.8 4.5,7.5 4.5,6 Z M4.5,6 L4.5,18 C4.5,19.5 7.9,20.8 12,20.8 C16.1,20.8 19.5,19.5 19.5,18 L19.5,6 M4.5,12 C4.5,13.5 7.9,14.8 12,14.8 C16.1,14.8 19.5,13.5 19.5,12",
            ["rocket"] = "M12,15.5 L8.5,12 C10,7 13,4 19.5,3.5 C19,10 16,13 12,15.5 Z M8.5,12 L5,12 L7.5,8.5 L11,8.5 M12,15.5 L12,19 L15.5,16.5 L15.5,13",
            ["restore"] = "M4,12 A8,8 0 1 0 6.4,6.3 M4,4.5 L4,8.5 L8,8.5",
            ["open"] = "M14,4 L20,4 L20,10 M20,4 L11.5,12.5 M18,14 L18,20 L4,20 L4,6 L10,6",
            ["chev"] = "M9,6 L15,12 L9,18",
            ["lock"] = "M5,10.5 H19 V20.5 H5 Z M8,10.5 L8,8 A4,4 0 0 1 16,8 L16,10.5",
            ["fire"] = "M12,21 C8,21 5.5,18.3 5.5,15 C5.5,11 9,9.5 9,5.5 C11.5,7 13,9 13,11 C14,10 14.5,9 14.5,7.5 C17,9.5 18.5,12 18.5,15 C18.5,18.3 16,21 12,21 Z",
            ["search"] = "M17.5,11 A6.5,6.5 0 1 1 4.5,11 A6.5,6.5 0 1 1 17.5,11 Z M15.7,15.7 L20,20",
            ["back"] = "M15,6 L9,12 L15,18",
            ["check"] = "M5,12.5 L10,17 L19,7.5",
        };
    }

    static class Ui
    {
        public static Brush Br(string hex) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex); b.Freeze(); return b; }
        // Win11 Fluent palette
        public static readonly Brush Ink = Br("#1B1B1B"), Gray = Br("#5F6670"), Light = Br("#8A9099"), Line = Br("#E3E6EB"), RowLine = Br("#EDEFF2"),
            Accent = Br("#0F6CBD"), Green = Br("#0F7B0F"), GreenSoft = Br("#2E9E5B"), Red = Br("#C42B1C"), Amber = Br("#9D5D00");

        // icon tile palettes (bg, fg)
        public static readonly (string bg, string fg) TBlue = ("#E6F0FB", "#2B6CC4"), TGreen = ("#E5F4EA", "#2E8B57"), TPurple = ("#EFE9FA", "#7A4FC9"),
            TOrange = ("#FFF3E0", "#C77700"), TRed = ("#FDECEC", "#C93B3B"), TGray = ("#F0F3F7", "#3D4652");

        public static TextBlock Text(string s, double size = 14, Brush color = null, FontWeight? weight = null)
        {
            return new TextBlock
            {
                Text = s,
                FontSize = size,
                Foreground = color ?? Ink,
                FontWeight = weight ?? FontWeights.Normal,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        public static FrameworkElement Icon(string name, double size = 18, Brush color = null, double stroke = 1.5) => SmallIcon(name, size, color ?? Ink, stroke);

        public static Border Tile(string icon, (string bg, string fg) pal, double size = 40)
        {
            return new Border
            {
                Width = size, Height = size, CornerRadius = new CornerRadius(size > 34 ? 8 : 6),
                Background = Br(pal.bg),
                Child = new Viewbox { Width = size * 0.55, Height = size * 0.55, Child = IconRaw(icon, Br(pal.fg), 1.6) },
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        /// icon in a 24x24 box, for use inside a Viewbox
        public static Canvas IconRaw(string name, Brush color, double stroke = 1.5)
        {
            var c = new Canvas { Width = 24, Height = 24 };
            c.Children.Add(new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(Icons.P.TryGetValue(name, out var d) ? d : Icons.P["file"]),
                Stroke = color, StrokeThickness = stroke,
                StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            });
            return c;
        }

        public static FrameworkElement SmallIcon(string name, double size, Brush color, double stroke = 1.5) =>
            new Viewbox { Width = size, Height = size, Child = IconRaw(name, color, stroke), VerticalAlignment = VerticalAlignment.Center };

        public static Border Card(UIElement child, Thickness? pad = null)
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)),
                BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                Padding = pad ?? new Thickness(18, 6, 18, 6), Child = child,
                Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Direction = 270, Opacity = 0.05, Color = Color.FromRgb(11, 42, 90) },
            };
        }

        public static Border Chip(string s, Brush fg, string bg) => new Border
        {
            Background = Br(bg), CornerRadius = new CornerRadius(11), Padding = new Thickness(10, 2, 10, 3),
            Child = Text(s, 12, fg, FontWeights.SemiBold), VerticalAlignment = VerticalAlignment.Center,
        };

        class BarState { public Border Fill; public double F; public double H; }

        public static Grid Bar(double fraction, Brush color, double height = 6, string track = "#E6E9EE")
        {
            var g = new Grid { Height = height };
            g.Children.Add(new Border { Background = Br(track), CornerRadius = new CornerRadius(height / 2) });
            var fill = new Border { Background = color, CornerRadius = new CornerRadius(height / 2), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
            var st = new BarState { Fill = fill, F = Math.Clamp(fraction, 0, 1), H = height };
            g.Tag = st;
            g.SizeChanged += (s, e) => fill.Width = Math.Max(st.H, e.NewSize.Width * st.F);
            g.Children.Add(fill);
            return g;
        }

        public static void SetBar(Grid bar, double fraction)
        {
            if (bar.Tag is not BarState st) return;
            st.F = Math.Clamp(fraction, 0, 1);
            if (bar.ActualWidth > 0) st.Fill.Width = Math.Max(st.H, bar.ActualWidth * st.F);
        }

        public static Button Pill(string s, RoutedEventHandler click = null)
        {
            var b = new Button { Content = s, Style = (Style)Application.Current.FindResource("PillButton") };
            if (click != null) b.Click += click;
            return b;
        }

        public static Button Ghost(string s, RoutedEventHandler click = null)
        {
            var b = new Button { Content = s, Style = (Style)Application.Current.FindResource("GhostButton") };
            if (click != null) b.Click += click;
            return b;
        }

        public static Button IconButton(string icon, string s, bool primary, RoutedEventHandler click = null)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(SmallIcon(icon, 16, primary ? Brushes.White : Ink));
            var t = Text(s, 14, primary ? Brushes.White : Ink);
            t.Margin = new Thickness(8, 0, 0, 1);
            sp.Children.Add(t);
            var b = primary ? Pill("") : Ghost("");
            b.Content = sp;
            if (click != null) b.Click += click;
            return b;
        }

        public static Button Link(string s, RoutedEventHandler click = null)
        {
            var b = new Button { Content = s, Style = (Style)Application.Current.FindResource("LinkButton") };
            if (click != null) b.Click += click;
            return b;
        }

        public static CheckBox Check(bool isChecked = true, object content = null) =>
            new CheckBox { IsChecked = isChecked, Content = content, Style = (Style)Application.Current.FindResource("RoundCheck") };

        public static CheckBox Toggle(bool on) =>
            new CheckBox { IsChecked = on, Style = (Style)Application.Current.FindResource("Toggle"), VerticalAlignment = VerticalAlignment.Center };

        public static Border Divider() => new Border { Height = 1, Background = RowLine };

        /// Page title + one line value + gray sub
        public static StackPanel Header(string label, string big, out TextBlock bigText, out TextBlock sub)
        {
            var sp = new StackPanel();
            sp.Children.Add(Text(label, 26, Ink, FontWeights.SemiBold));
            bigText = Text(big, 15, Ink, FontWeights.SemiBold);
            bigText.Margin = new Thickness(0, 6, 0, 0);
            sp.Children.Add(bigText);
            sub = Text("", 13, Gray);
            sub.Margin = new Thickness(0, 2, 0, 0);
            sp.Children.Add(sub);
            return sp;
        }

        public static TextBlock Title(string s) => Text(s, 26, Ink, FontWeights.SemiBold);

        public static TextBlock Section(string s, double top = 20)
        {
            var t = Text(s, 14, Ink, FontWeights.SemiBold);
            t.Margin = new Thickness(0, top, 0, 8);
            return t;
        }

        /// A list row: [left element] Name / desc ............ right
        public static Grid Row(UIElement left, string name, string desc, UIElement right, out TextBlock nameText, out TextBlock descText)
        {
            var g = new Grid { MinHeight = 62, Background = Brushes.Transparent };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (left != null)
            {
                if (left is FrameworkElement fe) { fe.Margin = new Thickness(0, 0, 14, 0); fe.VerticalAlignment = VerticalAlignment.Center; }
                g.Children.Add(left);
            }
            var mid = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 10, 12, 10) };
            nameText = Text(name, 14.5, Ink, FontWeights.Medium);
            mid.Children.Add(nameText);
            descText = Text(desc ?? "", 12.5, Gray);
            descText.Margin = new Thickness(0, 2, 0, 0);
            if (string.IsNullOrEmpty(desc)) descText.Visibility = Visibility.Collapsed;
            mid.Children.Add(descText);
            Grid.SetColumn(mid, 1);
            g.Children.Add(mid);
            if (right != null)
            {
                if (right is FrameworkElement r) r.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(right, 2);
                g.Children.Add(right);
            }
            return g;
        }

        public static Grid Row(UIElement left, string name, string desc, UIElement right) => Row(left, name, desc, right, out _, out _);

        public static Ellipse Dot(Brush b) => new Ellipse { Width = 8, Height = 8, Fill = b, Margin = new Thickness(10, 0, 0, 0) };

        public static FrameworkElement StatusDone() => Circle(GreenSoft, "check");

        static Grid Circle(Brush bg, string icon)
        {
            var g = new Grid { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
            g.Children.Add(new Ellipse { Fill = bg });
            g.Children.Add(new Viewbox { Width = 10, Height = 10, Child = IconRaw(icon, Brushes.White, 3) });
            return g;
        }

        /// Page frame: header on top, scrolling body (in a card), footer at bottom
        public static Grid Frame(UIElement header, UIElement body, UIElement footer, bool card = true)
        {
            var g = new Grid { Margin = new Thickness(32, 22, 32, 22) };
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.Children.Add(header);
            UIElement content = body;
            if (card && body is Panel p && !(body is Grid)) content = Card(body, new Thickness(18, 4, 18, 4));
            var sv = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 18, 0, 14), Padding = new Thickness(0, 0, 4, 4) };
            Grid.SetRow(sv, 1);
            g.Children.Add(sv);
            if (footer != null) { Grid.SetRow(footer, 2); g.Children.Add(footer); }
            return g;
        }

        /// Footer: gray text left, buttons right
        public static Grid Footer(TextBlock left, params Button[] buttons)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (left != null) g.Children.Add(left);
            var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            foreach (var b in buttons) { b.Margin = new Thickness(10, 0, 0, 0); sp.Children.Add(b); }
            Grid.SetColumn(sp, 1);
            g.Children.Add(sp);
            return g;
        }

        /// "安全设计" strip
        public static Border SafeNote(string text)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var ic = SmallIcon("shield", 20, Accent, 1.6); ic.Margin = new Thickness(0, 0, 12, 0);
            g.Children.Add(ic);
            var sp = new StackPanel();
            sp.Children.Add(Text("安全设计", 13, Accent, FontWeights.SemiBold));
            sp.Children.Add(Text(text, 12.5, Gray));
            Grid.SetColumn(sp, 1);
            g.Children.Add(sp);
            var c = Card(g, new Thickness(16, 10, 16, 10));
            c.Background = new SolidColorBrush(Color.FromArgb(0xB3, 0xFF, 0xFF, 0xFF));
            return c;
        }

        public static bool Confirm(string msg) =>
            MessageBox.Show(msg, "简境", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;

        public static void Info(string msg) => MessageBox.Show(msg, "简境", MessageBoxButton.OK, MessageBoxImage.Information);

        public static Grid Cols(params (UIElement el, GridLength w)[] cols)
        {
            var g = new Grid();
            int i = 0;
            foreach (var (el, w) in cols)
            {
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
                if (el != null) { Grid.SetColumn(el, i); g.Children.Add(el); }
                i++;
            }
            return g;
        }
        public static GridLength Star(double n = 1) => new GridLength(n, GridUnitType.Star);
        public static GridLength Px(double n) => new GridLength(n);
        public static readonly GridLength Auto = GridLength.Auto;
    }
}
