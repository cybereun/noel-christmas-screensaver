using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Noel
{
    /// <summary>An entirely local, resolution-independent winter scene.</summary>
    public sealed class WinterScene : FrameworkElement
    {
        private readonly VisualCollection visuals;
        private readonly DrawingVisual scenery = new DrawingVisual();
        private readonly DrawingVisual atmosphere = new DrawingVisual();
        private readonly DrawingVisual lettering = new DrawingVisual();
        private readonly Stopwatch watch = new Stopwatch();
        private readonly DispatcherTimer timer;
        private readonly List<Lamp> lamps = new List<Lamp>();
        private readonly List<Flake> flakes = new List<Flake>();
        private readonly List<Spark> stars = new List<Spark>();
        private readonly Brush glow = Radial("#FFF3BF", "#00DDA54C");
        private readonly Brush whiteGlow = Radial("#B4E9F1", "#00A3DDE9");
        private readonly Brush flakeBrush = Solid("#DFEDF0");
        private readonly Brush lampCore = Solid("#FFF5D7");
        private readonly Brush lampAmber = Solid("#E8B66D");
        private SaverSettings settings;
        private double worldWidth = 1600;
        private double treeX, treeTop, treeHeight, treeRadius, scale = 1;
        private bool compact;
        private bool ready;
        private bool rebuilding;
        private double cachedWidth, cachedHeight, cachedDpiX, cachedDpiY;
        private bool letteringDirty = true;
        private string clockLabel = "";
        private int lastClockMinute = -1;

        private struct Lamp { public double X, Y, R, Phase, Depth; }
        private struct Flake { public double X, Y, R, Speed, Drift, Phase, Alpha; }
        private struct Spark { public double X, Y, R, Phase; }

        public WinterScene(SaverSettings settings)
        {
            this.settings = settings;
            ClipToBounds = true;
            Focusable = false;
            IsHitTestVisible = false;
            visuals = new VisualCollection(this);
            visuals.Add(scenery);
            visuals.Add(atmosphere);
            visuals.Add(lettering);
            timer = new DispatcherTimer(DispatcherPriority.Render);
            timer.Tick += OnTick;
            SetInterval();
            SizeChanged += delegate { Rebuild(); };
            Loaded += delegate { Rebuild(); Start(); };
            Unloaded += delegate { Stop(); };
        }

        protected override int VisualChildrenCount { get { return visuals == null ? 0 : visuals.Count; } }
        protected override Visual GetVisualChild(int index) { return visuals[index]; }

        public void SetSettings(SaverSettings value)
        {
            int oldTheme = settings == null ? 0 : settings.Theme;
            settings = value;
            SetInterval();
            letteringDirty = true;
            if (ready && oldTheme != settings.Theme)
            {
                cachedWidth = 0;
                ready = false;
                Rebuild();
                return;
            }
            if (ready) RenderAt(watch.Elapsed.TotalSeconds);
            else Rebuild();
        }

        private void SetInterval()
        {
            timer.Interval = TimeSpan.FromMilliseconds(1000.0 / Math.Max(15, Math.Min(60, settings.FrameRate)));
        }

        public void Start()
        {
            if (!watch.IsRunning) watch.Start();
            if (!timer.IsEnabled) timer.Start();
        }

        public void Stop()
        {
            timer.Stop();
            watch.Stop();
        }

        private void OnTick(object sender, EventArgs e) { RenderAt(watch.Elapsed.TotalSeconds); }

        private void Rebuild()
        {
            if (ActualWidth < 1 || ActualHeight < 1 || rebuilding) return;
            double dpiX = 1, dpiY = 1;
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
            {
                Matrix toDevice = source.CompositionTarget.TransformToDevice;
                dpiX = toDevice.M11; dpiY = toDevice.M22;
            }
            if (ready && Math.Abs(cachedWidth - ActualWidth) < .01 &&
                Math.Abs(cachedHeight - ActualHeight) < .01 &&
                Math.Abs(cachedDpiX - dpiX) < .001 && Math.Abs(cachedDpiY - dpiY) < .001)
                return;
            rebuilding = true;
            try
            {
            scale = ActualHeight / 900.0;
            worldWidth = ActualWidth / scale;
            compact = worldWidth < 1120;
            double inset = Math.Max(0, (worldWidth - 1600) / 2.0);
            treeX = compact ? worldWidth * .5 : inset + Math.Min(1110, worldWidth * .705);
            treeTop = compact ? 292 : 146;
            treeHeight = compact ? 472 : 610;
            treeRadius = compact ? Math.Min(186, worldWidth * .34) : Math.Min(232, worldWidth * .156);
            lamps.Clear();
            flakes.Clear();
            stars.Clear();
            BuildParticles();
            // Rasterize the many thousands of needle strokes only on a size or
            // DPI change. The animation then moves one frozen bitmap instead
            // of continuously repainting the entire vector forest and tree.
            double padding = 12 * scale;
            double imageWidth = ActualWidth + padding * 2;
            double imageHeight = ActualHeight + padding * 2;
            double quality = Math.Min(1, 4096.0 / Math.Max(imageWidth * dpiX, imageHeight * dpiY));
            double renderDpiX = dpiX * quality, renderDpiY = dpiY * quality;
            DrawingVisual cacheSource = new DrawingVisual();
            using (DrawingContext dc = cacheSource.RenderOpen())
            {
                dc.PushTransform(new TranslateTransform(padding, padding));
                dc.PushTransform(new ScaleTransform(scale, scale));
                DrawBackdrop(dc);
                if (settings.Theme == 0) { DrawTree(dc); DrawPresents(dc); }
                dc.Pop();
                dc.Pop();
            }
            RenderTargetBitmap cache = new RenderTargetBitmap(
                Math.Max(1, (int)Math.Ceiling(imageWidth * renderDpiX)),
                Math.Max(1, (int)Math.Ceiling(imageHeight * renderDpiY)),
                96 * renderDpiX, 96 * renderDpiY, PixelFormats.Pbgra32);
            cache.Render(cacheSource);
            cache.Freeze();
            using (DrawingContext dc = scenery.RenderOpen())
            {
                dc.DrawImage(cache, new Rect(-padding, -padding, imageWidth, imageHeight));
            }
            RenderOptions.SetBitmapScalingMode(scenery, BitmapScalingMode.LowQuality);
            cachedWidth = ActualWidth; cachedHeight = ActualHeight;
            cachedDpiX = dpiX; cachedDpiY = dpiY;
            ready = true;
            letteringDirty = true;
            }
            finally { rebuilding = false; }
            RenderAt(watch.Elapsed.TotalSeconds);
        }

        private void BuildParticles()
        {
            Random random = new Random(25122026);
            for (int i = 0; i < 68; i++)
                stars.Add(new Spark { X = random.NextDouble() * worldWidth, Y = 20 + random.NextDouble() * 560,
                    R = .35 + random.NextDouble() * .85, Phase = random.NextDouble() * 6.283 });
            for (int i = 0; i < 290; i++)
            {
                double depth = random.NextDouble();
                flakes.Add(new Flake { X = random.NextDouble() * (worldWidth + 160) - 80,
                    Y = random.NextDouble() * 1000 - 60, R = .65 + Math.Pow(depth, 3) * 3.8,
                    Speed = 8 + depth * 24, Drift = 5 + random.NextDouble() * 14,
                    Phase = random.NextDouble() * 6.283, Alpha = .14 + depth * .52 });
            }
        }

        private void DrawBackdrop(DrawingContext dc)
        {
            if (settings.Theme != 0)
            {
                DrawAlternateBackdrop(dc);
                return;
            }
            LinearGradientBrush bg = new LinearGradientBrush();
            bg.StartPoint = new Point(0, 0); bg.EndPoint = new Point(.45, 1);
            bg.GradientStops.Add(new GradientStop(ColorOf("#06151F"), 0));
            bg.GradientStops.Add(new GradientStop(ColorOf("#0B252E"), .65));
            bg.GradientStops.Add(new GradientStop(ColorOf("#18343A"), 1));
            bg.Freeze();
            dc.DrawRectangle(bg, null, new Rect(-12, -12, worldWidth + 24, 924));
            dc.DrawEllipse(Radial("#22434D", "#0022434D"), null,
                new Point(treeX, 390), 560, 550);
            dc.DrawEllipse(Radial("#17353F", "#0017353F"), null,
                new Point(worldWidth * .19, 270), 390, 390);

            Random forest = new Random(1919);
            for (int row = 0; row < 3; row++)
            {
                for (double x = -90; x < worldWidth + 130; x += 51 + forest.NextDouble() * 60)
                {
                    double height = 82 + forest.NextDouble() * 125 + row * 19;
                    double floor = 724 + row * 31 + forest.NextDouble() * 14;
                    DrawDistantFir(dc, x, floor, height,
                        row == 0 ? "#172E36" : row == 1 ? "#102A31" : "#09232A");
                }
            }
            dc.DrawRectangle(Linear("#001B3940", "#B319343B"), null, new Rect(-12, 590, worldWidth + 24, 250));
            StreamGeometry ground = new StreamGeometry();
            using (StreamGeometryContext g = ground.Open())
            {
                g.BeginFigure(new Point(-10, 785), true, true);
                g.BezierTo(new Point(worldWidth * .27, 748), new Point(worldWidth * .55, 823), new Point(worldWidth + 10, 769), true, false);
                g.LineTo(new Point(worldWidth + 10, 912), true, false);
                g.LineTo(new Point(-10, 912), true, false);
            }
            ground.Freeze();
            dc.DrawGeometry(Linear("#243D43", "#0C222C"), null, ground);
            dc.DrawEllipse(Radial("#315057", "#00315057"), null,
                new Point(treeX - 40, 792), treeRadius * 2.2, 93);
            dc.DrawEllipse(Radial("#3561564A", "#0061564A"), null,
                new Point(treeX, 790), treeRadius * 1.3, 50);
            dc.DrawEllipse(Radial("#B307161C", "#0007161C"), null,
                new Point(treeX, 786), treeRadius * .9, 17);
            // Restrained corner falloff keeps attention on the warm tree lights.
            RadialGradientBrush vignette = new RadialGradientBrush();
            vignette.Center = new Point(.51, .45); vignette.GradientOrigin = new Point(.51, .45);
            vignette.RadiusX = .72; vignette.RadiusY = .8;
            vignette.GradientStops.Add(new GradientStop(Colors.Transparent, .34));
            vignette.GradientStops.Add(new GradientStop(ColorOf("#99010A12"), 1));
            vignette.Freeze();
            dc.DrawRectangle(vignette, null, new Rect(-12, -12, worldWidth + 24, 924));
        }

        private void DrawAlternateBackdrop(DrawingContext dc)
        {
            if (settings.Theme == 1) DrawVillage(dc);
            else if (settings.Theme == 2) DrawAurora(dc);
            else if (settings.Theme == 3) DrawWinterExpress(dc);
            else DrawGingerbread(dc);
        }

        private LinearGradientBrush Gradient(string upper, string lower, double diagonal)
        {
            var brush = new LinearGradientBrush(ColorOf(upper), ColorOf(lower), new Point(0, 0), new Point(diagonal, 1));
            brush.Freeze(); return brush;
        }

        private StreamGeometry Hill(double x, double floor, double width, double peak)
        {
            var path = new StreamGeometry();
            using (var g = path.Open())
            {
                g.BeginFigure(new Point(x - width, floor), true, true);
                g.BezierTo(new Point(x - width * .55, peak + 30), new Point(x - width * .22, peak - 5), new Point(x, peak), true, false);
                g.BezierTo(new Point(x + width * .27, peak + 13), new Point(x + width * .62, floor - 32), new Point(x + width, floor), true, false);
                g.LineTo(new Point(x + width, 930), true, false); g.LineTo(new Point(x - width, 930), true, false);
            }
            path.Freeze(); return path;
        }

        private void DrawSnowGround(DrawingContext dc, string top, string baseColor)
        {
            var ground = new StreamGeometry();
            using (var g = ground.Open())
            {
                g.BeginFigure(new Point(-40, 785), true, true);
                g.BezierTo(new Point(worldWidth * .22, 738), new Point(worldWidth * .59, 820), new Point(worldWidth + 40, 758), true, false);
                g.LineTo(new Point(worldWidth + 40, 940), true, false); g.LineTo(new Point(-40, 940), true, false);
            }
            ground.Freeze(); dc.DrawGeometry(Gradient(top, baseColor, .4), null, ground);
            dc.DrawEllipse(Radial(top, "#001C3440"), null, new Point(worldWidth * .53, 792), worldWidth * .48, 77);
        }

        private void DrawCabin(DrawingContext dc, double x, double floor, double width, double height,
            string walls, string roof, string windowColor, double warmth)
        {
            double inset = width * .13;
            dc.DrawEllipse(Radial("#80040B10", "#00040B10"), null, new Point(x + width * .48, floor + 12), width * .76, 18);
            var wall = new Rect(x, floor - height * .63, width, height * .63);
            dc.DrawRectangle(Gradient(walls, "#12262C", .9), null, wall);
            var roofPath = Polygon(new Point(x - 15, wall.Top + 8), new Point(x + width * .48, floor - height), new Point(x + width + 15, wall.Top + 8), new Point(x + width - inset * .35, wall.Top + 31), new Point(x + width * .48, floor - height + 31), new Point(x + inset * .35, wall.Top + 31));
            dc.DrawGeometry(Gradient(roof, "#3C4A4B", .6), null, roofPath);
            var snowLine = new Pen(Solid("#D3E2DF"), Math.Max(3, width * .022)); snowLine.StartLineCap = PenLineCap.Round; snowLine.EndLineCap = PenLineCap.Round; snowLine.Freeze();
            dc.DrawLine(snowLine, new Point(x - 8, wall.Top + 12), new Point(x + width * .48, floor - height + 25));
            dc.DrawLine(snowLine, new Point(x + width * .48, floor - height + 25), new Point(x + width + 8, wall.Top + 12));
            dc.DrawRectangle(Gradient("#3B3128", "#191F20", .7), null, new Rect(x + width * .39, floor - height * .34, width * .22, height * .34));
            WindowLamp(dc, x + inset, floor - height * .43, width * .18, height * .20, windowColor, warmth);
            WindowLamp(dc, x + width - inset - width * .18, floor - height * .43, width * .18, height * .20, windowColor, warmth * .85);
            dc.DrawRoundedRectangle(Solid("#15272B"), null, new Rect(x + width * .44, wall.Top - height * .22, width * .11, height * .22), 2, 2);
            dc.DrawEllipse(Radial("#F3BF77", "#00F3BF77"), null, new Point(x + width * .50, floor - height * .27), 7, 8);
        }

        private void WindowLamp(DrawingContext dc, double x, double y, double w, double h, string color, double warmth)
        {
            var frame = new Pen(Solid("#D4BA84"), Math.Max(1.5, w * .09)); frame.Freeze();
            dc.DrawRectangle(Solid(color), frame, new Rect(x, y, w, h));
            dc.DrawLine(frame, new Point(x + w / 2, y), new Point(x + w / 2, y + h));
            dc.DrawLine(frame, new Point(x, y + h * .51), new Point(x + w, y + h * .51));
            lamps.Add(new Lamp { X = x + w * .52, Y = y + h * .5, R = Math.Max(2, w * .24), Phase = x * .07 + y * .03, Depth = warmth });
        }

        private void DrawVillage(DrawingContext dc)
        {
            dc.DrawRectangle(Gradient("#101E35", "#27606A", .45), null, new Rect(-12, -12, worldWidth + 24, 924));
            double center = worldWidth / 2, unit = Math.Min(1, worldWidth / 1500.0);
            for (int i = 0; i < 70; i++)
            {
                double x = Mod(i * 191.0 + 73, worldWidth);
                double y = 45 + Mod(i * 137.0 + 31, 455);
                double r = .5 + (i % 5) * .22;
                dc.DrawEllipse(Solid(i % 4 == 0 ? "#9BAFCE" : "#607993"), null, new Point(x, y), r, r);
            }
            double village = worldWidth * .67;
            dc.DrawGeometry(Gradient("#536879", "#233746", .8), null, Hill(village * .92, 718, worldWidth * .42, 510));
            var summit = new StreamGeometry();
            using (var g = summit.Open())
            {
                double x = village * .92, peak = 510, width = worldWidth * .42, floor = 718;
                g.BeginFigure(new Point(x, peak - 2), true, true);
                g.BezierTo(new Point(x + width * .10, peak + 18), new Point(x + width * .13, floor - 34), new Point(x + width * .22, floor - 18), true, false);
                g.LineTo(new Point(x + width * .15, floor - 17), true, false);
                g.BezierTo(new Point(x + width * .09, peak + 73), new Point(x + width * .06, peak + 47), new Point(x, peak + 38), true, false);
                g.BezierTo(new Point(x - width * .08, peak + 58), new Point(x - width * .12, floor - 28), new Point(x - width * .21, floor - 18), true, false);
                g.LineTo(new Point(x - width * .15, floor - 17), true, false);
                g.BezierTo(new Point(x - width * .12, peak + 20), new Point(x - width * .07, peak + 20), new Point(x, peak - 2), true, false);
            }
            summit.Freeze(); dc.DrawGeometry(Gradient("#E1EBE9", "#95ADB0", .7), null, summit);
            dc.DrawGeometry(Gradient("#304952", "#142B35", .55), null, Hill(center * 1.03, 753, worldWidth * .60, 528));
            DrawSnowGround(dc, "#DCE8E4", "#526970");
            DrawCabin(dc, village - 280 * unit, 743, 245 * unit, 252 * unit, "#A56F50", "#3A2528", "#EBC37D", .7);
            DrawCabin(dc, village - 10 * unit, 759, 305 * unit, 312 * unit, "#A15C45", "#492B30", "#FFE09B", 1.0);
            DrawCabin(dc, village + 320 * unit, 735, 238 * unit, 250 * unit, "#45665E", "#273B3C", "#F5D38D", .82);
            for (int i = 0; i < 12; i++)
            {
                double x = Mod(i * 233 + 29, worldWidth);
                double h = 100 + (i % 4) * 23;
                DrawDistantFir(dc, x, 791 + (i % 3) * 29, h, i % 2 == 0 ? "#183737" : "#1C4140");
            }
            dc.DrawLine(new Pen(Solid("#F2E7D2"), 1), new Point(0, 845), new Point(worldWidth, 840));
        }

        private void DrawAurora(DrawingContext dc)
        {
            dc.DrawRectangle(Gradient("#080D29", "#12374D", .52), null, new Rect(-12, -12, worldWidth + 24, 924));
            double center = worldWidth / 2;
            for (int i = 0; i < 92; i++)
            {
                double x = Mod(i * 127 + 17, worldWidth), y = 30 + Mod(i * 83, 510);
                dc.DrawEllipse(Solid(i % 8 == 0 ? "#C9C2DE" : "#617193"), null, new Point(x, y), .7 + (i % 3) * .25, .7 + (i % 3) * .25);
            }
            for (int band = 0; band < 4; band++)
            {
                double y = 185 + band * 50;
                StreamGeometry ribbon = new StreamGeometry();
                using (var g = ribbon.Open())
                {
                    g.BeginFigure(new Point(-20, y + 75), true, true);
                    g.BezierTo(new Point(center * .28, y - 130), new Point(center * .40, y + 148), new Point(center * .71, y + 15), true, false);
                    g.BezierTo(new Point(center * 1.08, y - 138), new Point(center * 1.56, y + 4), new Point(worldWidth + 25, y - 95), true, false);
                    g.LineTo(new Point(worldWidth + 25, y - 59), true, false);
                    g.BezierTo(new Point(center * 1.58, y + 47), new Point(center * 1.08, y - 83), new Point(center * .70, y + 84), true, false);
                    g.BezierTo(new Point(center * .41, y + 222), new Point(center * .25, y - 46), new Point(-20, y + 126), true, false);
                }
                ribbon.Freeze();
                string[][] colors = { new string[] { "#8D7DD89C", "#283BDF9D" }, new string[] { "#8074BEA9", "#245AD59A" }, new string[] { "#7464A5D8", "#213BCAAC" }, new string[] { "#72609BC6", "#1B2DAE91" } };
                dc.DrawGeometry(Gradient(colors[band][0], colors[band][1], .55), null, ribbon);
            }
            dc.DrawEllipse(Radial("#3878D7B4", "#0078D7B4"), null, new Point(center, 299), worldWidth * .29, 130);
            dc.DrawGeometry(Solid("#18293A51"), null, Hill(center * .29, 739, worldWidth * .43, 550));
            dc.DrawGeometry(Solid("#16304759"), null, Hill(center * 1.18, 764, worldWidth * .68, 607));
            DrawSnowGround(dc, "#D7E4E1", "#5A6C78");
            for (int i = 0; i < 13; i++) DrawDistantFir(dc, Mod(i * 211 + 40, worldWidth), 801 + (i % 3) * 23, 118 + (i % 5) * 17, "#111F38");
            DrawCabin(dc, worldWidth * .74, 769, Math.Min(158, worldWidth * .24), 170, "#765148", "#343348", "#EAC47F", .4);
        }

        private void DrawWinterExpress(DrawingContext dc)
        {
            dc.DrawRectangle(Gradient("#10172C", "#405266", .35), null, new Rect(-12, -12, worldWidth + 24, 924));
            for (int i = 0; i < 110; i++)
            {
                double x = Mod(i * 167 + 29, worldWidth), y = 38 + Mod(i * 83, 490), r = i % 9 == 0 ? 1.6 : .65;
                dc.DrawEllipse(Solid(i % 9 == 0 ? "#ECD8A9" : "#9BA9C0"), null, new Point(x, y), r, r);
            }
            dc.DrawEllipse(Radial("#FFF2D4", "#00FFF2D4"), null, new Point(worldWidth * .75, 203), 152, 152);
            dc.DrawEllipse(Solid("#F2E8CF"), null, new Point(worldWidth * .75, 203), 67, 67);
            dc.DrawGeometry(Solid("#384454"), null, Hill(worldWidth * .31, 732, worldWidth * .53, 488));
            dc.DrawGeometry(Solid("#252F41"), null, Hill(worldWidth * .92, 747, worldWidth * .49, 542));
            for (int i = 0; i < 27; i++)
            {
                double x = Mod(i * 271 + 13, worldWidth), h = 112 + (i % 5) * 27;
                DrawDistantFir(dc, x, 767 + (i % 3) * 27, h, i % 3 == 0 ? "#142B35" : "#172F39");
            }
            DrawSnowGround(dc, "#CDD8D9", "#586D78");
            Pen track = new Pen(Solid("#C7BCA3"), 2); track.Freeze();
            dc.DrawLine(track, new Point(0, 712), new Point(worldWidth, 712)); dc.DrawLine(track, new Point(0, 740), new Point(worldWidth, 740));
            for (int i = 0; i < worldWidth / 27 + 1; i++) dc.DrawRectangle(Solid("#5B4F46"), null, new Rect(i * 27, 707, 10, 37));
        }

        private void DrawTrain(DrawingContext dc, double seconds)
        {
            double trainWidth = 484, travel = Mod(seconds * 94 + 760, worldWidth + trainWidth + 100) - trainWidth - 50;
            dc.PushTransform(new TranslateTransform(travel, 0));
            var gold = new Pen(Solid("#C5A474"), 2); gold.Freeze();
            for (int car = 0; car < 3; car++)
            {
                double x = 111 + car * 101, y = 618 + car % 2 * 2;
                dc.DrawRoundedRectangle(Gradient(car % 2 == 0 ? "#75454A" : "#325158", "#263038", .8), gold, new Rect(x, y, 92, 70), 9, 9);
                dc.DrawRectangle(Gradient("#FFDFA0", "#B98753", .2), null, new Rect(x + 15, y + 12, 17, 23));
                dc.DrawRectangle(Gradient("#F4D39A", "#987350", .2), null, new Rect(x + 49, y + 12, 17, 23));
                TrainWheel(dc, x + 20, y + 74, seconds); TrainWheel(dc, x + 71, y + 74, seconds);
            }
            dc.DrawRectangle(Gradient("#683E43", "#263038", .8), gold, new Rect(411, 604, 64, 83));
            dc.DrawRoundedRectangle(Gradient("#963F3B", "#492E34", .3), gold, new Rect(431, 565, 45, 115), 14, 14);
            dc.DrawEllipse(Radial("#FFD99A", "#00FFD99A"), null, new Point(470, 624), 21, 21);
            dc.DrawEllipse(Solid("#F8DFAE"), gold, new Point(472, 628), 6, 6);
            dc.DrawRectangle(Gradient("#A97A43", "#624333", .3), gold, new Rect(439, 546, 31, 13));
            dc.DrawRectangle(Gradient("#554C48", "#292C32", .6), gold, new Rect(438, 534, 24, 15));
            dc.DrawGeometry(Solid("#29313A"), null, Polygon(new Point(417, 676), new Point(478, 676), new Point(490, 690), new Point(403, 690)));
            TrainWheel(dc, 425, 689, seconds); TrainWheel(dc, 464, 689, seconds);
            for (int i = 0; i < 3; i++)
            {
                double t = seconds * .25 + i * 2.1;
                dc.PushOpacity(.18 - i * .04);
                dc.DrawEllipse(Solid("#DAE4E9"), null, new Point(435 + Math.Cos(t) * (12 + i * 4), 510 - i * 39), 16 + i * 9, 12 + i * 5);
                dc.Pop();
            }
            dc.Pop();
        }

        private void DrawSleigh(DrawingContext dc, double seconds)
        {
            double x = Mod(seconds * 48 + 260, worldWidth + 620) - 560;
            double y = 222 + Math.Sin(seconds * .8) * 9;
            dc.PushOpacity(.88);
            dc.PushTransform(new TranslateTransform(x, y));
            var line = new Pen(Solid("#D9BF8B"), 2.2); line.Freeze();
            dc.DrawLine(new Pen(Solid("#80EED8A6"), 1), new Point(-160, 0), new Point(5, 0));
            for (int i = 0; i < 4; i++)
            {
                double rx = i * 34;
                dc.DrawEllipse(Gradient("#DFCDA8", "#6D6C6F", .6), null, new Point(rx + 6, 22), 21, 10);
                dc.DrawEllipse(Solid("#DBCBAA"), null, new Point(rx + 24, 16), 8, 7);
                dc.DrawLine(line, new Point(rx - 3, 27), new Point(rx - 10, 42));
                dc.DrawLine(line, new Point(rx + 12, 28), new Point(rx + 17, 42));
                dc.DrawLine(line, new Point(rx + 20, 12), new Point(rx + 17, 2));
                dc.DrawLine(line, new Point(rx + 17, 3), new Point(rx + 10, -2));
                dc.DrawLine(line, new Point(rx + 19, 4), new Point(rx + 24, -3));
            }
            var harness = new Pen(Solid("#D3B780"), 1.1); harness.Freeze();
            dc.DrawLine(harness, new Point(10, 20), new Point(178, 27));
            dc.DrawLine(harness, new Point(117, 20), new Point(178, 17));
            dc.DrawGeometry(Gradient("#CB4A49", "#69343D", .6), harness,
                Polygon(new Point(176, 11), new Point(231, 12), new Point(255, 25), new Point(239, 38), new Point(191, 33)));
            dc.DrawEllipse(Solid("#9A674F"), null, new Point(204, 10), 12, 12);
            dc.DrawEllipse(Solid("#E3B9A2"), null, new Point(207, 7), 8, 7);
            dc.DrawGeometry(Solid("#BC463D"), null,
                Polygon(new Point(196, 5), new Point(199, -13), new Point(214, -8), new Point(218, 4)));
            dc.DrawEllipse(Solid("#FFF1D0"), null, new Point(219, -8), 4, 4);
            dc.DrawLine(line, new Point(178, 33), new Point(255, 38));
            dc.DrawLine(line, new Point(255, 38), new Point(272, 30));
            dc.Pop(); dc.Pop();
        }

        private void TrainWheel(DrawingContext dc, double x, double y, double seconds)
        {
            var pen = new Pen(Solid("#D0AF7D"), 2); pen.Freeze();
            dc.DrawEllipse(Solid("#222B31"), pen, new Point(x, y), 15, 15);
            dc.PushTransform(new RotateTransform(seconds * 94, x, y));
            dc.DrawLine(pen, new Point(x - 11, y), new Point(x + 11, y)); dc.DrawLine(pen, new Point(x, y - 11), new Point(x, y + 11));
            dc.Pop();
        }

        private void DrawGingerbread(DrawingContext dc)
        {
            dc.DrawRectangle(Gradient("#17223C", "#A26463", .54), null, new Rect(-12, -12, worldWidth + 24, 924));
            for (int i = 0; i < 66; i++)
            {
                double x = Mod(i * 157 + 42, worldWidth), y = 43 + Mod(i * 89, 498);
                dc.DrawEllipse(Solid(i % 8 == 0 ? "#FFE8C8" : "#C7D4D6"), null, new Point(x, y), .7 + (i % 3) * .2, .7 + (i % 3) * .2);
            }
            DrawSnowGround(dc, "#E9DADD", "#715463");
            double center = worldWidth / 2, width = Math.Min(470, worldWidth * .78), left = center - width / 2;
            dc.DrawEllipse(Radial("#6D2A242D", "#001B1825"), null, new Point(center, 744), width * .70, 45);
            dc.DrawRectangle(Gradient("#A65A49", "#4A3038", .35), null, new Rect(left + width * .12, 512, width * .76, 232));
            dc.DrawGeometry(Gradient("#7D463F", "#402C3C", .7), null,
                Polygon(new Point(left, 545), new Point(center, 339), new Point(left + width, 545), new Point(left + width - 39, 572), new Point(center, 396), new Point(left + 39, 572)));
            var icing = new Pen(Solid("#FFF1DC"), 11); icing.StartLineCap = PenLineCap.Round; icing.EndLineCap = PenLineCap.Round; icing.Freeze();
            dc.DrawLine(icing, new Point(left + 9, 547), new Point(center, 348)); dc.DrawLine(icing, new Point(center, 348), new Point(left + width - 9, 547));
            WindowLamp(dc, left + width * .24, 584, width * .20, 81, "#FFDEA5", .9);
            WindowLamp(dc, left + width * .56, 584, width * .20, 81, "#FFDEA5", .8);
            dc.DrawRectangle(Gradient("#503044", "#332636", .5), null, new Rect(center - 27, 639, 54, 105));
            dc.DrawEllipse(Solid("#E6BC80"), null, new Point(center + 14, 696), 3, 3);
            var stripe = new Pen(Solid("#C95159"), 5); stripe.Freeze();
            for (int side = 0; side < 2; side++)
            {
                double x = center + (side == 0 ? -1 : 1) * width * .42;
                var cane = new Pen(Solid("#FFF1DF"), 21); cane.StartLineCap = PenLineCap.Round; cane.EndLineCap = PenLineCap.Round; cane.Freeze();
                dc.DrawLine(cane, new Point(x, 757), new Point(x, 536));
                var hook = new StreamGeometry();
                using (var g = hook.Open()) { g.BeginFigure(new Point(x, 544), false, false); g.BezierTo(new Point(x, 510), new Point(x + 31, 510), new Point(x + 31, 544), true, false); }
                hook.Freeze(); dc.DrawGeometry(null, cane, hook);
                for (int k = 0; k < 7; k++) dc.DrawLine(stripe, new Point(x - 8, 566 + k * 27), new Point(x + 8, 558 + k * 27));
            }
            for (int i = 0; i < 11; i++)
            {
                double x = Mod(i * 293 + 34, worldWidth), y = 763 - (i % 4) * 19;
                dc.DrawEllipse(Solid(i % 3 == 0 ? "#F3CB81" : "#D88183"), null, new Point(x, y), 11 + i % 4 * 3, 10 + i % 3 * 2);
                dc.DrawEllipse(Solid("#FFF5E0"), null, new Point(x - 4, y - 6), 3, 2);
            }
        }

        private static void DrawDistantFir(DrawingContext dc, double x, double floor, double height, string color)
        {
            StreamGeometry fir = new StreamGeometry();
            using (StreamGeometryContext g = fir.Open())
            {
                g.BeginFigure(new Point(x, floor - height), true, true);
                for (int j = 1; j <= 7; j++)
                {
                    double u = j / 7.0;
                    g.LineTo(new Point(x + height * .25 * u, floor - height + height * u), true, false);
                    if (j < 7) g.LineTo(new Point(x + height * .12 * u, floor - height + height * u - height * .027), true, false);
                }
                g.LineTo(new Point(x - height * .25, floor), true, false);
                for (int j = 6; j >= 1; j--)
                {
                    double u = j / 7.0;
                    g.LineTo(new Point(x - height * .12 * u, floor - height + height * u - height * .027), true, false);
                    g.LineTo(new Point(x - height * .25 * u, floor - height + height * u), true, false);
                }
            }
            fir.Freeze(); dc.DrawGeometry(Solid(color), null, fir);
        }

        private void DrawTree(DrawingContext dc)
        {
            double bottom = treeTop + treeHeight;
            dc.DrawRoundedRectangle(Linear("#5D4930", "#2D2923"), null,
                new Rect(treeX - 9, bottom - 25, 18, 47), 2, 2);
            Random random = new Random(20261225);
            Brush[] foliage = { Solid("#12372F"), Solid("#194137"), Solid("#234B3E"), Solid("#315447"), Solid("#183B32"), Solid("#375B4B") };
            Pen[] needles = new Pen[foliage.Length];
            for (int i = 0; i < needles.Length; i++) { needles[i] = new Pen(foliage[i], .9); needles[i].Freeze(); }
            Pen stem = new Pen(Solid("#36503A"), 1.6); stem.Freeze();
            Pen snow = new Pen(Solid("#637F79"), .8); snow.Freeze();

            // Irregular overlapping boughs, then thousands of fine needles. The
            // ellipse projection gives each tier volume without a raster asset.
            for (int tier = 0; tier < 23; tier++)
            {
                double u = .035 + tier * .042;
                double radius = treeRadius * Math.Pow(u, .88);
                double y = treeTop + treeHeight * u;
                for (int side = 0; side < 2; side++)
                {
                    for (int branch = 0; branch < 12; branch++)
                    {
                        double angle = branch * Math.PI * 2 / 12 + tier * 1.81;
                        double depth = Math.Sin(angle);
                        if ((depth >= 0 ? 1 : 0) != side) continue;
                        double length = radius * (.87 + random.NextDouble() * .19);
                        double endX = treeX + Math.Cos(angle) * length;
                        double endY = y + (17 + u * 18) + depth * radius * .16;
                        double startY = y - (11 + u * 21);
                        double dx = endX - treeX, dy = endY - startY;
                        double magnitude = Math.Sqrt(dx * dx + dy * dy);
                        double vx = dx / magnitude, vy = dy / magnitude;
                        double nx = -vy, ny = vx;
                        StreamGeometry mass = new StreamGeometry();
                        using (StreamGeometryContext g = mass.Open())
                        {
                            g.BeginFigure(new Point(treeX, startY - 9), true, true);
                            g.QuadraticBezierTo(new Point(treeX + dx * .62, startY + dy * .34 - 11), new Point(endX, endY), true, false);
                            g.QuadraticBezierTo(new Point(treeX + dx * .56, startY + dy * .68 + 17 + u * 10), new Point(treeX, y + 24), true, false);
                        }
                        mass.Freeze();
                        dc.DrawGeometry(foliage[side == 0 ? 0 : 1], null, mass);
                        dc.DrawLine(stem, new Point(treeX, startY), new Point(endX, endY));
                        int twigs = Math.Max(6, (int)(magnitude / 5));
                        for (int twig = 1; twig <= twigs; twig++)
                        {
                            double t = twig / (double)twigs;
                            double bx = treeX + dx * t;
                            double by = startY + dy * t - Math.Sin(t * Math.PI) * (4 + u * 5);
                            double leaf = (7 + u * 14) * (1 - t * .44);
                            for (int sign = -1; sign <= 1; sign += 2)
                            {
                                for (int k = 0; k < 3; k++)
                                {
                                    double along = leaf * (.3 + .21 * k);
                                    double outwards = leaf * (.65 - .17 * k);
                                    double ex = bx - vx * along + nx * outwards * sign;
                                    double ey = by - vy * along + ny * outwards * sign;
                                    Pen p = needles[side == 0 ? random.Next(0, 3) : random.Next(1, needles.Length)];
                                    dc.DrawLine(p, new Point(bx + random.NextDouble() * 2, by), new Point(ex, ey));
                                }
                            }
                            if (side == 1 && random.NextDouble() < .105)
                                dc.DrawLine(snow, new Point(bx - vx * 7 - nx * 4, by - vy * 7 - ny * 4 - 1), new Point(bx, by - 1));
                        }
                    }
                }
            }

            // A winding, fine gold wire carries softly breathing warm bulbs.
            Point? previous = null;
            Pen frontWire = new Pen(Solid("#A3886141"), .9); frontWire.Freeze();
            Pen backWire = new Pen(Solid("#426A623D"), .65); backWire.Freeze();
            int count = 670;
            for (int i = 0; i <= count; i++)
            {
                double t = i / (double)count;
                double u = .07 + t * .89;
                double a = t * Math.PI * 12.6 + .6;
                double depth = Math.Cos(a);
                double x = treeX + Math.Sin(a) * treeRadius * Math.Pow(u, .88) * .93;
                double y = treeTop + treeHeight * u + depth * 15 * u;
                Point point = new Point(x, y);
                if (previous.HasValue) dc.DrawLine(depth > -.1 ? frontWire : backWire, previous.Value, point);
                previous = point;
                if (i % 3 == 0)
                    lamps.Add(new Lamp { X = x, Y = y, R = .8 + random.NextDouble() * .65,
                        Phase = random.NextDouble() * 6.283, Depth = depth > -.15 ? 1 : .32 });
            }
            // A few independent lights avoid an overly mechanical spiral.
            for (int i = 0; i < 105; i++)
            {
                double u = .15 + random.NextDouble() * .78;
                double x = treeX + (random.NextDouble() * 2 - 1) * treeRadius * Math.Pow(u, .88) * .85;
                lamps.Add(new Lamp { X = x, Y = treeTop + treeHeight * u, R = .65 + random.NextDouble() * .6,
                    Phase = random.NextDouble() * 6.283, Depth = .45 + random.NextDouble() * .5 });
            }
            // Jewel-like, muted ornaments, with tiny specular reflections.
            for (int i = 0; i < 34; i++)
            {
                double u = .20 + random.NextDouble() * .72;
                double x = treeX + (random.NextDouble() * 2 - 1) * treeRadius * Math.Pow(u, .88) * .83;
                double y = treeTop + treeHeight * u;
                double r = 3.6 + random.NextDouble() * 3.8;
                RadialGradientBrush ornament = new RadialGradientBrush();
                ornament.GradientOrigin = new Point(.25, .2); ornament.Center = new Point(.35, .3);
                ornament.GradientStops.Add(new GradientStop(ColorOf(i % 3 == 0 ? "#BC8A65" : "#DFCD99"), 0));
                ornament.GradientStops.Add(new GradientStop(ColorOf(i % 3 == 0 ? "#713C36" : "#897544"), .48));
                ornament.GradientStops.Add(new GradientStop(ColorOf(i % 3 == 0 ? "#3C2526" : "#3B3827"), 1));
                ornament.Freeze();
                dc.DrawEllipse(ornament, null, new Point(x, y), r, r * 1.08);
                dc.DrawEllipse(Solid("#BAEEE0B0"), null, new Point(x - r * .3, y - r * .35), r * .17, r * .13);
            }

            double starY = treeTop - 12;
            dc.DrawEllipse(Radial("#52E9BB73", "#00E9BB73"), null, new Point(treeX, starY), 70, 70);
            dc.DrawEllipse(Radial("#95FFE2A0", "#00FFE2A0"), null, new Point(treeX, starY), 29, 29);
            dc.DrawGeometry(Linear("#FFF7CD", "#BB8C40"), null, Star(treeX, starY, 19, 7.6, 5));
            dc.DrawLine(new Pen(Solid("#80FBE7B3"), .7), new Point(treeX, starY - 39), new Point(treeX, starY + 36));
            dc.DrawLine(new Pen(Solid("#58FBE7B3"), .7), new Point(treeX - 34, starY), new Point(treeX + 34, starY));
        }

        private void DrawPresents(DrawingContext dc)
        {
            double y = treeTop + treeHeight + 14;
            double k = compact ? .74 : 1;
            dc.PushTransform(new TranslateTransform(treeX, y));
            dc.PushTransform(new ScaleTransform(k, k));
            Present(dc, -142, -8, 74, 48, "#693F3C", "#352528", "#B5A17A", -6);
            Present(dc, 59, -3, 86, 58, "#A5916C", "#65583D", "#D1C5A6", 5);
            Present(dc, -82, 5, 61, 38, "#344F47", "#1C332E", "#A39570", 2);
            Present(dc, 21, 15, 48, 35, "#543537", "#332529", "#AF9874", -3);
            dc.Pop(); dc.Pop();
        }

        private static void Present(DrawingContext dc, double x, double y, double width, double height,
            string front, string side, string ribbon, double angle)
        {
            dc.PushTransform(new RotateTransform(angle, x + width / 2, y));
            dc.DrawEllipse(Radial("#90020E13", "#00020E13"), null, new Point(x + width / 2, y + 6), width * .74, 11);
            dc.DrawRectangle(Linear(front, side), null, new Rect(x, y - height, width, height));
            dc.DrawGeometry(Solid(side), null, Polygon(new Point(x + width, y - height), new Point(x + width + 12, y - height - 7), new Point(x + width + 12, y - 7), new Point(x + width, y)));
            dc.DrawGeometry(Solid(front), null, Polygon(new Point(x, y - height), new Point(x + 12, y - height - 7), new Point(x + width + 12, y - height - 7), new Point(x + width, y - height)));
            dc.DrawRectangle(Solid(ribbon), null, new Rect(x + width * .46, y - height, width * .09, height));
            dc.DrawRectangle(Solid(ribbon), null, new Rect(x, y - height * .71, width, 3));
            Pen bow = new Pen(Solid(ribbon), 2); bow.Freeze();
            dc.DrawEllipse(null, bow, new Point(x + width * .40, y - height - 4), width * .12, 5);
            dc.DrawEllipse(null, bow, new Point(x + width * .60, y - height - 4), width * .12, 5);
            dc.DrawEllipse(Solid(ribbon), null, new Point(x + width * .50, y - height - 3), 3, 2.5);
            dc.Pop();
        }

        public void RenderAt(double seconds)
        {
            if (!ready)
            {
                if (ActualWidth > 0 && ActualHeight > 0) Rebuild();
                return;
            }
            double driftX = Math.Sin(seconds / 105) * 2.5;
            double driftY = Math.Sin(seconds / 139) * 1.7;
            scenery.Offset = new Vector(driftX * scale, driftY * scale);
            using (DrawingContext dc = atmosphere.RenderOpen())
            {
                dc.PushTransform(new ScaleTransform(scale, scale));
                for (int i = 0; i < stars.Count; i++)
                {
                    Spark s = stars[i];
                    dc.PushOpacity(.12 + .15 * (.5 + .5 * Math.Sin(seconds * .36 + s.Phase)));
                    dc.DrawEllipse(flakeBrush, null, new Point(s.X, s.Y), s.R, s.R);
                    dc.Pop();
                }
                if (settings.Theme == 2) DrawSleigh(dc, seconds);
                dc.PushTransform(new TranslateTransform(driftX, driftY));
                for (int i = 0; i < lamps.Count; i++)
                {
                    Lamp lamp = lamps[i];
                    double breath = .78 + .22 * Math.Sin(seconds * .64 + lamp.Phase);
                    dc.PushOpacity(lamp.Depth * breath);
                    dc.DrawEllipse(glow, null, new Point(lamp.X, lamp.Y), lamp.R * 7.6, lamp.R * 7.6);
                    dc.DrawEllipse(lampAmber, null, new Point(lamp.X, lamp.Y), lamp.R * 1.7, lamp.R * 1.7);
                    dc.DrawEllipse(lampCore, null, new Point(lamp.X, lamp.Y), lamp.R * .74, lamp.R * .74);
                    dc.Pop();
                }
                dc.PushOpacity(.38 + .09 * Math.Sin(seconds * .55));
                dc.DrawEllipse(glow, null, new Point(treeX, treeTop - 12), 36, 36);
                dc.Pop();
                dc.Pop();

                int snowCount = settings.SnowAmount == 0 ? 90 : settings.SnowAmount == 2 ? 290 : 180;
                for (int i = 0; i < Math.Min(snowCount, flakes.Count); i++)
                {
                    Flake f = flakes[i];
                    double y = Mod(f.Y + seconds * f.Speed + 65, 1030) - 65;
                    double x = Mod(f.X + Math.Sin(seconds * .21 + f.Phase) * f.Drift + seconds * 2.5 + 80, worldWidth + 160) - 80;
                    dc.PushOpacity(f.Alpha);
                    if (f.R > 2.8)
                    {
                        dc.DrawEllipse(whiteGlow, null, new Point(x, y), f.R * 2.8, f.R * 2.8);
                        dc.DrawEllipse(flakeBrush, null, new Point(x, y), f.R * .55, f.R * .55);
                    }
                    else dc.DrawEllipse(flakeBrush, null, new Point(x, y), f.R, f.R);
                    dc.Pop();
                }
                if (settings.Theme == 3) DrawTrain(dc, seconds);
                dc.Pop();
            }
            DrawLettering(seconds);
        }

        private void DrawLettering(double seconds)
        {
            // Keep shaped glyphs in the retained visual. Only the transform
            // changes each frame; the clock is reshaped once per minute.
            lettering.Offset = new Vector(Math.Sin(seconds / 121) * 4 * scale,
                Math.Cos(seconds / 157) * 2 * scale);
            DateTime current = DateTime.Now;
            int currentMinute = current.DayOfYear * 1440 + current.Hour * 60 + current.Minute;
            if (!letteringDirty && (!settings.ShowClock || currentMinute == lastClockMinute)) return;
            letteringDirty = false;
            using (DrawingContext dc = lettering.RenderOpen())
            {
                dc.PushTransform(new ScaleTransform(scale, scale));
                double offset = Math.Max(0, (worldWidth - 1600) / 2.0);
                double textX = compact ? worldWidth / 2 : offset + Math.Max(72, Math.Min(150, worldWidth * .094));
                dc.PushTransform(Transform.Identity);
                if (settings.ShowGreeting)
                {
                    if (compact)
                    {
                        SpacedText(dc, ThemeKicker(), worldWidth / 2, 74, 9.5, 2.3, "#BCA77F", true);
                        Text(dc, "Merry Christmas", textX, 111, Math.Min(53, worldWidth / 9.8), "Georgia", "#F0E8D7", true, false);
                        Text(dc, "고요한 겨울밤, 따뜻한 크리스마스", textX, 184, 12, "Malgun Gothic", "#8EA5A8", true, false);
                        dc.DrawLine(new Pen(Solid("#6972594A"), 1), new Point(textX - 26, 224), new Point(textX + 26, 224));
                    }
                    else
                    {
                        double fontSize = worldWidth < 1350 ? 79 : 98;
                        SpacedText(dc, ThemeKicker(), textX + 4, 292, 11, 2.6, "#BCA77F", false);
                        Text(dc, "Merry", textX, 334, fontSize * .84, "Georgia", "#F2EBDD", false, false);
                        Text(dc, "Christmas", textX - 3, 414, fontSize, "Georgia", "#F2EBDD", false, false);
                        dc.DrawLine(new Pen(Solid("#967E54"), 1), new Point(textX + 4, 550), new Point(textX + 48, 550));
                        dc.DrawGeometry(Solid("#B8A37B"), null, Star(textX + 64, 550, 3, 1.15, 4));
                        Text(dc, "고요한 겨울밤, 따뜻한 크리스마스", textX + 4, 576, 14, "Malgun Gothic", "#9AAEAF", false, false);
                        SpacedText(dc, "A LITTLE WONDER. A LITTLE WARMTH.", textX + 4, 611, 8.2, 1.7, "#667F85", false);
                    }
                    SpacedText(dc, "N O Ë L  /  2 5  D E C E M B E R", compact ? worldWidth / 2 : textX + 4,
                        compact ? 845 : 808, 8.5, .35, "#82958E", compact);
                }
                if (settings.ShowClock)
                {
                    DateTime now = DateTime.Now;
                    int stamp = now.DayOfYear * 1440 + now.Hour * 60 + now.Minute;
                    if (stamp != lastClockMinute)
                    {
                        clockLabel = now.ToString("HH:mm", CultureInfo.InvariantCulture);
                        lastClockMinute = stamp;
                    }
                    Text(dc, clockLabel, compact ? worldWidth / 2 : textX + 4,
                        compact ? 28 : 736, compact ? 18 : 31, "Segoe UI Light", "#B7C1BA", compact, false);
                }
                dc.Pop(); dc.Pop();
            }
        }

        private string ThemeKicker()
        {
            string[] names = { "A SEASON OF QUIET WONDER", "A VILLAGE IN SOFT SNOW", "UNDER THE AURORA", "THE POLAR NIGHT EXPRESS", "A LITTLE SUGAR & SNOW" };
            return names[Math.Max(0, Math.Min(names.Length - 1, settings.Theme))];
        }

        private static void Text(DrawingContext dc, string text, double x, double y, double size,
            string font, string color, bool center, bool italic)
        {
            FormattedText ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily(font), italic ? FontStyles.Italic : FontStyles.Normal,
                FontWeights.Normal, FontStretches.Normal), size, Solid(color), 1.0);
            dc.DrawText(ft, new Point(center ? x - ft.Width / 2 : x, y));
        }

        private static void SpacedText(DrawingContext dc, string text, double x, double y, double size, double spacing, string color, bool center)
        {
            List<FormattedText> chars = new List<FormattedText>();
            double width = 0;
            Brush brush = Solid(color);
            for (int i = 0; i < text.Length; i++)
            {
                FormattedText ft = new FormattedText(text[i].ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), size, brush, 1.0);
                chars.Add(ft); width += ft.WidthIncludingTrailingWhitespace + spacing;
            }
            if (center) x -= (width - spacing) / 2;
            for (int i = 0; i < chars.Count; i++)
            {
                dc.DrawText(chars[i], new Point(x, y));
                x += chars[i].WidthIncludingTrailingWhitespace + spacing;
            }
        }

        private static double Mod(double n, double d) { return ((n % d) + d) % d; }
        private static Color ColorOf(string value) { return (Color)ColorConverter.ConvertFromString(value); }
        private static SolidColorBrush Solid(string color)
        {
            SolidColorBrush b = new SolidColorBrush(ColorOf(color)); b.Freeze(); return b;
        }
        private static LinearGradientBrush Linear(string top, string bottom)
        {
            LinearGradientBrush b = new LinearGradientBrush(ColorOf(top), ColorOf(bottom), new Point(0, 0), new Point(0, 1));
            b.Freeze(); return b;
        }
        private static RadialGradientBrush Radial(string middle, string edge)
        {
            RadialGradientBrush b = new RadialGradientBrush();
            Color c = ColorOf(middle), e = ColorOf(edge);
            b.GradientStops.Add(new GradientStop(c, 0));
            b.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(c.A * .35), c.R, c.G, c.B), .28));
            b.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(c.A * .08), c.R, c.G, c.B), .64));
            b.GradientStops.Add(new GradientStop(e, 1));
            b.Freeze(); return b;
        }
        private static StreamGeometry Star(double x, double y, double outer, double inner, int points)
        {
            Point[] p = new Point[points * 2];
            for (int i = 0; i < p.Length; i++)
            {
                double a = -Math.PI / 2 + Math.PI * i / points;
                double r = i % 2 == 0 ? outer : inner;
                p[i] = new Point(x + Math.Cos(a) * r, y + Math.Sin(a) * r);
            }
            return Polygon(p);
        }
        private static StreamGeometry Polygon(params Point[] p)
        {
            StreamGeometry g = new StreamGeometry();
            using (StreamGeometryContext c = g.Open())
            {
                c.BeginFigure(p[0], true, true);
                for (int i = 1; i < p.Length; i++) c.LineTo(p[i], true, false);
            }
            g.Freeze(); return g;
        }
    }
}
