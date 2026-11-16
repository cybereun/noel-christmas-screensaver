using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Noel
{
    internal sealed class SettingsWindow : Window
    {
        private readonly SaverSettings settings;
        private readonly WinterScene scene;
        private readonly TextBlock status;
        private SaverRun saver;
        private static readonly Brush Gold = Brush("#DDC393");
        private static readonly Brush Ink = Brush("#F2ECE1");
        private static readonly Brush Muted = Brush("#9DAFAF");
        internal static readonly string[] Themes = { "샴페인 골드 트리", "눈 내리는 겨울 마을", "오로라와 산타 썰매", "별빛 속 겨울 열차", "진저브레드 마을" };

        internal SettingsWindow(SaverSettings initial)
        {
            settings = initial.Copy();
            Title = "Noël · 크리스마스 스크린세이버";
            Width = 1120; Height = 740; MinWidth = 900; MinHeight = 660;
            MaxHeight = SystemParameters.WorkArea.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Brush("#0C191D"); Foreground = Ink; FontFamily = new FontFamily("Malgun Gothic");
            var root = new Grid { Margin = new Thickness(32, 26, 32, 24) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 24) };
            header.Children.Add(Text("N O Ë L   /   CHRISTMAS SCREENSAVER", 11, Gold));
            var title = Text("당신의 화면에, 고요한 크리스마스", 27, Ink); title.Margin = new Thickness(0, 12, 0, 8); header.Children.Add(title);
            header.Children.Add(Text("샴페인빛 트리와 천천히 내려앉는 눈. 잠시 쉬어 가는 겨울밤.", 13, Muted));
            root.Children.Add(header);

            var body = new Grid(); Grid.SetRow(body, 1); root.Children.Add(body);
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(292) });
            var previewPanel = new Grid { Margin = new Thickness(0, 0, 28, 0) };
            previewPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            previewPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.Children.Add(previewPanel);
            scene = new WinterScene(settings.Copy());
            var frame = new Border { BorderBrush = Brush("#31413D"), BorderThickness = new Thickness(1), Background = Brushes.Black, ClipToBounds = true, Child = scene };
            previewPanel.Children.Add(frame);
            var caption = new Grid { Margin = new Thickness(0, 14, 0, 0) }; Grid.SetRow(caption, 1); previewPanel.Children.Add(caption);
            caption.Children.Add(Text("●  LIVE PREVIEW", 10, Gold));
            var captionRight = Text("소리 없이, 편안하게", 11, Muted); captionRight.HorizontalAlignment = HorizontalAlignment.Right; caption.Children.Add(captionRight);

            var controls = new StackPanel { Margin = new Thickness(0, 3, 0, 0) }; Grid.SetColumn(controls, 1); body.Children.Add(controls);
            controls.Children.Add(Text("나만의 겨울밤", 19, Ink));
            var themeLabel = Text("크리스마스 장면  ·  5가지", 12, Muted); themeLabel.Margin = new Thickness(0, 22, 0, 10); controls.Children.Add(themeLabel);
            var theme = Combo(Themes, settings.Theme);
            controls.Children.Add(theme);
            theme.SelectionChanged += delegate
            {
                if (theme.SelectedIndex < 0) return;
                settings.Theme = theme.SelectedIndex; UpdatePreview();
            };
            var snowLabel = Text("내리는 눈", 12, Muted); snowLabel.Margin = new Thickness(0, 25, 0, 10); controls.Children.Add(snowLabel);
            var snow = Combo(new string[] { "은은하게", "잔잔하게", "풍성하게" }, settings.SnowAmount);
            controls.Children.Add(snow);
            snow.SelectionChanged += delegate { settings.SnowAmount = snow.SelectedIndex; UpdatePreview(); };

            var greeting = Check("크리스마스 인사말", settings.ShowGreeting); greeting.Margin = new Thickness(0, 23, 0, 14); controls.Children.Add(greeting);
            greeting.Checked += delegate { settings.ShowGreeting = true; UpdatePreview(); };
            greeting.Unchecked += delegate { settings.ShowGreeting = false; UpdatePreview(); };
            var clock = Check("작은 시계 표시", settings.ShowClock); controls.Children.Add(clock);
            clock.Checked += delegate { settings.ShowClock = true; UpdatePreview(); };
            clock.Unchecked += delegate { settings.ShowClock = false; UpdatePreview(); };

            var motionLabel = Text("움직임", 12, Muted); motionLabel.Margin = new Thickness(0, 25, 0, 10); controls.Children.Add(motionLabel);
            var motion = Combo(new string[] { "절전 · 24 fps", "균형 · 30 fps", "부드럽게 · 60 fps" }, settings.FrameRate == 24 ? 0 : (settings.FrameRate == 60 ? 2 : 1));
            controls.Children.Add(motion);
            motion.SelectionChanged += delegate { settings.FrameRate = new int[] { 24, 30, 60 }[motion.SelectedIndex]; UpdatePreview(); };

            var line = new Border { Height = 1, Background = Brush("#2C3C3C"), Margin = new Thickness(0, 24, 0, 14) }; controls.Children.Add(line);
            var apply = Button("선택한 장면 Windows에 적용", true); controls.Children.Add(apply); apply.Click += Apply;
            var help = Text("Windows의 화면 보호기를 이 장면으로 바꿉니다.\n대기 시간은 아래 설정 창에서 지정할 수 있어요.", 11, Muted); help.LineHeight = 19; help.Margin = new Thickness(0, 10, 0, 0); controls.Children.Add(help);

            var footer = new Grid { Margin = new Thickness(0, 27, 0, 0) }; Grid.SetRow(footer, 2); root.Children.Add(footer);
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var note = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            note.Children.Add(Text("전체 화면은 마우스 이동이나 아무 키로 닫힙니다.", 11, Muted));
            status = Text("설정을 저장하면 다음 실행에도 그대로 적용됩니다.", 10, Gold); status.Margin = new Thickness(0, 6, 0, 0); note.Children.Add(status); footer.Children.Add(note);
            var save = Button("설정 저장", false); save.Margin = new Thickness(12, 0, 0, 0); Grid.SetColumn(save, 1); footer.Children.Add(save); save.Click += delegate { Save(); };
            var windowed = Button("창으로 보기", false); windowed.Margin = new Thickness(10, 0, 0, 0); Grid.SetColumn(windowed, 2); footer.Children.Add(windowed);
            windowed.Click += delegate { var preview = Program.CreateWindowed(settings); preview.Owner = this; preview.Show(); };
            var fullscreen = Button("전체 화면 보기", false); fullscreen.Margin = new Thickness(10, 0, 0, 0); Grid.SetColumn(fullscreen, 3); footer.Children.Add(fullscreen);
            fullscreen.Click += delegate
            {
                scene.Stop(); Hide();
                saver = new SaverRun(settings, delegate { saver = null; Show(); Activate(); scene.Start(); }); saver.Show();
            };
            Loaded += delegate { scene.Start(); };
            Closed += delegate { scene.Stop(); if (saver != null) saver.Dispose(); };
        }
        private void UpdatePreview() { settings.Normalize(); scene.SetSettings(settings.Copy()); status.Text = "미리보기에 적용했습니다. 저장하면 다음에도 유지됩니다."; }
        private bool Save()
        {
            try { SettingsStore.Save(settings, SettingsStore.FilePath); status.Text = "설정을 저장했습니다."; return true; }
            catch (Exception e) { MessageBox.Show(this, "설정을 저장하지 못했습니다.\n" + e.Message, "Noël", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
        }
        private void Apply(object sender, RoutedEventArgs e)
        {
            try
            {
                string destination = Program.InstallAndApply(settings, Assembly.GetExecutingAssembly().Location);
                Program.OpenScreenSaverSettings();
                status.Text = "Windows에 ‘" + Themes[settings.Theme] + "’ 장면을 적용했습니다.";
            }
            catch (Exception error) { MessageBox.Show(this, "Windows에 적용하지 못했습니다.\n" + error.Message, "Noël", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
        private static Brush Brush(string color) { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); return brush; }
        private static TextBlock Text(string text, double size, Brush foreground) { return new TextBlock { Text = text, FontSize = size, Foreground = foreground, TextWrapping = TextWrapping.Wrap }; }
        private static ComboBox Combo(string[] items, int selected)
        {
            var combo = new ComboBox { Height = 36, FontSize = 13, Padding = new Thickness(9, 5, 8, 5), Foreground = Brushes.Black, Background = Brush("#E8E9E5") };
            foreach (string item in items) combo.Items.Add(item); combo.SelectedIndex = selected; return combo;
        }
        private static CheckBox Check(string label, bool value) { return new CheckBox { Content = label, IsChecked = value, FontSize = 13, Foreground = Ink, Padding = new Thickness(7, 0, 0, 0) }; }
        private static Button Button(string label, bool primary)
        {
            var button = new Button { Content = label, Padding = new Thickness(16, 11, 16, 11), MinHeight = 42,
                FontSize = 12, FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal,
                Background = primary ? Gold : Brush("#172B2F"), Foreground = primary ? Brush("#142327") : Ink,
                BorderBrush = primary ? Gold : Brush("#3E5352"), BorderThickness = new Thickness(1), Cursor = System.Windows.Input.Cursors.Hand };
            var border = new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            var content = new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center); content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            content.SetBinding(FrameworkElement.MarginProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) }); border.AppendChild(content);
            button.Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
            button.MouseEnter += delegate { button.Opacity = 0.85; }; button.MouseLeave += delegate { button.Opacity = 1; };
            return button;
        }
    }
}
