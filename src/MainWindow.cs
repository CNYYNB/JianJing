using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Shell;

namespace XinCleaner
{
    public class MainWindow : Window
    {
        public static MainWindow Instance;
        readonly ContentControl _host = new ContentControl();
        readonly Dictionary<string, Func<UIElement>> _factories = new Dictionary<string, Func<UIElement>>();
        readonly Dictionary<string, UIElement> _pages = new Dictionary<string, UIElement>();
        readonly Dictionary<string, RadioButton> _nav = new Dictionary<string, RadioButton>();
        readonly bool _mica = Environment.OSVersion.Version.Build >= 22621; // Mica needs Win11 22H2+
        readonly Grid _root = new Grid();

        public MainWindow()
        {
            Instance = this;
            Title = "简境";
            Width = 1180; Height = 780; MinWidth = 960; MinHeight = 640;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = (FontFamily)Application.Current.FindResource("UiFont");
            try { Icon = BitmapFrame.Create(new Uri("pack://application:,,,/app.ico")); } catch { }

            if (_mica)
            {
                Background = Brushes.Transparent;
                WindowChrome.SetWindowChrome(this, new WindowChrome
                {
                    CaptionHeight = 40, GlassFrameThickness = new Thickness(-1), ResizeBorderThickness = new Thickness(6),
                    UseAeroCaptionButtons = true, CornerRadius = new CornerRadius(8),
                });
                SourceInitialized += (s, e) => ApplyMica();
                StateChanged += (s, e) => _root.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
            }
            else Background = Ui.Br("#F3F5F9");

            _factories["overview"] = () => new OverviewPage();
            _factories["clean"] = () => new CleanPage();
            _factories["space"] = () => new SpacePage();
            _factories["dup"] = () => new DupPage();
            _factories["uninstall"] = () => new UninstallPage();
            _factories["startup"] = () => new StartupPage();
            _factories["traces"] = () => new TracesPage();
            _factories["restore"] = () => new RestorePage();
            _factories["shred"] = () => new ShredPage();
            _factories["protect"] = () => new ProtectPage();
            _factories["settings"] = () => new SettingsPage();

            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(232) });
            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var side = new Grid();
            side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            side.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 14, 0, 18) };
            brand.Children.Add(Logo(22));
            var bt = Ui.Text("简境", 14, Ui.Ink, FontWeights.SemiBold); bt.Margin = new Thickness(10, 0, 0, 1);
            brand.Children.Add(bt);
            WindowChrome.SetIsHitTestVisibleInChrome(brand, false);
            side.Children.Add(brand);

            var nav = new StackPanel { Margin = new Thickness(8, 0, 10, 0) };
            foreach (var (key, label, icon) in new[]
            {
                ("overview", "概览", "home"), ("clean", "扫描清理", "scan"), ("space", "空间分析", "pie"), ("dup", "重复文件", "copy"),
                ("uninstall", "软件管理", "apps"), ("startup", "启动管理", "power"), ("traces", "隐私清理", "lock"), ("restore", "恢复中心", "history"),
                ("shred", "文件粉碎", "fire"), ("protect", "防护", "shield"),
            })
                nav.Children.Add(NavItem(key, label, icon));
            var navScroll = new ScrollViewer { Content = nav, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden };
            Grid.SetRow(navScroll, 1);
            side.Children.Add(navScroll);

            var bottom = new StackPanel { Margin = new Thickness(10, 8, 10, 14) };
            // 本地运行 card
            var lc = new StackPanel();
            var lh = new StackPanel { Orientation = Orientation.Horizontal };
            lh.Children.Add(Ui.SmallIcon("leaf", 15, Ui.GreenSoft, 1.7));
            var lt = Ui.Text("本地运行", 13, Ui.Ink, FontWeights.SemiBold); lt.Margin = new Thickness(8, 0, 0, 0);
            lh.Children.Add(lt);
            lc.Children.Add(lh);
            var l2 = Ui.Text("不联网扫描，不上传任何文件", 12, Ui.Gray); l2.Margin = new Thickness(0, 6, 0, 0);
            lc.Children.Add(l2);
            var l3 = Ui.Text("版本 " + AppInfo.Name, 12, Ui.Gray); l3.Margin = new Thickness(0, 2, 0, 0);
            lc.Children.Add(l3);
            bottom.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(153, 255, 255, 255)), BorderBrush = Ui.Line, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(2, 0, 2, 8), Child = lc,
            });
            bottom.Children.Add(NavItem("settings", "设置", "gear"));
            Grid.SetRow(bottom, 2);
            side.Children.Add(bottom);
            _root.Children.Add(side);

            var content = new Border
            {
                Background = new SolidColorBrush(_mica ? Color.FromArgb(128, 255, 255, 255) : Color.FromRgb(249, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(204, 255, 255, 255)),
                BorderThickness = new Thickness(1, 1, 0, 0), CornerRadius = new CornerRadius(8, 0, 0, 0), Margin = new Thickness(0, 44, 0, 0),
                Child = _host,
            };
            Grid.SetColumn(content, 1);
            _root.Children.Add(content);
            Content = _root;

            _nav["overview"].IsChecked = true;
        }

        public static FrameworkElement Logo(double size)
        {
            var c = new Canvas { Width = 1024, Height = 1024 };
            c.Children.Add(new Rectangle { Width = 944, Height = 944, RadiusX = 220, RadiusY = 220, Fill = Brushes.White, Stroke = Ui.Br("#DDDDDD"), StrokeThickness = 28 }.At(40, 40));
            c.Children.Add(new Polyline
            {
                Points = new PointCollection(new[] { new Point(200, 700), new Point(400, 430), new Point(500, 560), new Point(620, 380), new Point(824, 700) }),
                Stroke = Ui.Br("#111111"), StrokeThickness = 60, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            });
            c.Children.Add(new Ellipse { Width = 108, Height = 108, Fill = Ui.Br("#111111") }.At(646, 246));
            return new Viewbox { Width = size, Height = size, Child = c, VerticalAlignment = VerticalAlignment.Center };
        }

        RadioButton NavItem(string key, string label, string icon)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var ic = Ui.SmallIcon(icon, 17, Ui.Ink);
            sp.Children.Add(ic);
            var t = new TextBlock { Text = label, Margin = new Thickness(14, 0, 0, 1), VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(t);
            var rb = new RadioButton { Content = sp, GroupName = "nav", Style = (Style)Application.Current.FindResource("NavItem") };
            rb.Checked += (s, e) => { Show(key); RecolorIcons(); };
            _nav[key] = rb;
            return rb;
        }

        void RecolorIcons()
        {
            foreach (var rb in _nav.Values)
                if (rb.Content is StackPanel sp && sp.Children[0] is Viewbox vb && vb.Child is Canvas cv && cv.Children[0] is Path p)
                    p.Stroke = rb.IsChecked == true ? Ui.Accent : Ui.Ink;
        }

        public static void Go(string key)
        {
            if (Instance != null && Instance._nav.TryGetValue(key, out var rb)) rb.IsChecked = true;
        }

        public static T Page<T>(string key) where T : class
        {
            if (Instance == null) return null;
            if (!Instance._pages.TryGetValue(key, out var p)) { p = Instance._factories[key](); Instance._pages[key] = p; }
            return p as T;
        }

        void Show(string key)
        {
            if (!_pages.TryGetValue(key, out var p))
            {
                p = _factories[key]();
                _pages[key] = p;
            }
            _host.Content = p;
            if (p is IPage ip) ip.OnShown();
        }

        // ---------- Mica ----------
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
        [StructLayout(LayoutKind.Sequential)] struct MARGINS { public int l, r, t, b; }
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS m);

        void ApplyMica()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                var src = HwndSource.FromHwnd(hwnd);
                src.CompositionTarget.BackgroundColor = Colors.Transparent;
                int dark = 0; DwmSetWindowAttribute(hwnd, 20, ref dark, 4);
                var m = new MARGINS { l = -1, r = -1, t = -1, b = -1 }; DwmExtendFrameIntoClientArea(hwnd, ref m);
                int mica = 2; // DWMSBT_MAINWINDOW
                if (DwmSetWindowAttribute(hwnd, 38, ref mica, 4) != 0) Background = Ui.Br("#F3F5F9");
                int round = 2; DwmSetWindowAttribute(hwnd, 33, ref round, 4);
            }
            catch { Background = Ui.Br("#F3F5F9"); }
        }
    }

    static class UiExt
    {
        public static T At<T>(this T el, double x, double y) where T : UIElement { Canvas.SetLeft(el, x); Canvas.SetTop(el, y); return el; }
    }

    interface IPage { void OnShown(); }
}
