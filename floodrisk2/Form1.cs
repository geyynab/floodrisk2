using floodrisk2.Models;
using floodrisk2.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace floodrisk2
{
    public partial class Form1 : Form
    {
        // sensors
        private readonly List<ISensorModel> sensors = new List<ISensorModel>();
        private readonly Dictionary<ISensorModel, (Chart timeChart, Chart freqChart)> chartMap =
            new Dictionary<ISensorModel, (Chart, Chart)>();
        private readonly Dictionary<ISensorModel, Panel> paramPanelMap =
            new Dictionary<ISensorModel, Panel>();

        // status labels (Aman / Siaga / Bahaya) per sensor
        private readonly Dictionary<ISensorModel, Label> statusLabelMap =
            new Dictionary<ISensorModel, Label>();

        // === HISTORY BUFFER (streaming) ===
        private readonly Dictionary<ISensorModel, List<double>> signalHistory =
            new Dictionary<ISensorModel, List<double>>();
        private readonly Dictionary<ISensorModel, List<double>> timeHistory =
            new Dictionary<ISensorModel, List<double>>();

        // layout panels
        private Panel leftPanel;
        private Panel centerPanel;
        private Panel rightPanel;

        // timer + settings
        private Timer timer;
        private double sampleRate = 2000.0;
        private double windowSec = 10.0;

        // continuous time
        private double globalTime = 0.0;

        // domain charts
        private Chart sChart;
        private Chart zChart;

        // control buttons
        private Button btnStart;
        private Button btnStop;

        // === shading settings ===
        private readonly Color sStableFill = Color.FromArgb(40, Color.LightGreen);
        private readonly Color zStableFill = Color.FromArgb(40, Color.LightGreen);
        private readonly Pen axisPen = new Pen(Color.FromArgb(180, Color.Black), 1f);

        public Form1()
        {
            //InitializeComponent();

            Text = "Flood Risk Signal Dashboard - Simulation";
            Width = 1400;
            Height = 900;
            StartPosition = FormStartPosition.CenterScreen;

            InitSensors();
            BuildLayout();
            BuildChartsAndParams();
            BuildRightColumn();

            InitHistoryBuffers();

            timer = new Timer { Interval = 200 };
            timer.Tick += Timer_Tick;
        }

        #region Init + Layout

        private void InitSensors()
        {
            var jsn = new JSNSR04T();
            jsn.P.Distance_cm = 10;
            jsn.P.Alpha = 2.0;
            jsn.P.F0 = 40000;
            jsn.P.NoiseStd = 2.0;

            var yf = new YFS201();
            yf.P.Flow_Lpm = 8.0;
            yf.P.K = 7.5;
            yf.P.NoiseStd = 0.05;

            var yl = new YL83();
            yl.P.Wetness = 0.2;
            yl.P.Alpha = 0.5;
            yl.P.NoiseStd = 0.01;

            var ph = new SEN0161();
            ph.P.Vout = 2.6;
            ph.P.NoiseStd = 0.01;
            ph.P.Gain = 1.0;
            ph.P.Offset = 0.0;
            ph.P.Tau = 5.0;

            var tmp = new DS18B20();
            tmp.P.TrueTemp = 26.0;
            tmp.P.NoiseStd = 0.2;
            tmp.P.DriftPerSec = 0.0;

            sensors.Add(jsn);
            sensors.Add(yf);
            sensors.Add(yl);
            sensors.Add(ph);
            sensors.Add(tmp);
        }

        private void InitHistoryBuffers()
        {
            signalHistory.Clear();
            timeHistory.Clear();

            foreach (var s in sensors)
            {
                signalHistory[s] = new List<double>(8000);
                timeHistory[s] = new List<double>(8000);
            }

            globalTime = 0.0;
        }

        private void BuildLayout()
        {
            leftPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 280,
                BackColor = Color.FromArgb(240, 240, 240),
                Padding = new Padding(10, 15, 6, 6)
            };

            rightPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 360,
                BackColor = Color.WhiteSmoke,
                Padding = new Padding(6)
            };

            centerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(8),
                AutoScroll = true
            };

            Controls.Add(centerPanel);
            Controls.Add(rightPanel);
            Controls.Add(leftPanel);

            // === kontrol Start / Stop di kiri atas ===
            var ctlPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(4),
                Margin = new Padding(0, 0, 0, 10)
            };

            btnStart = new Button { Text = "Start", Width = 70 };
            btnStop = new Button { Text = "Stop", Width = 70 };

            btnStart.Click += (s, e) =>
            {
                if (!timer.Enabled)
                {
                    InitHistoryBuffers();
                    timer.Start();
                }
            };

            btnStop.Click += (s, e) => timer?.Stop();

            ctlPanel.Controls.Add(btnStart);
            ctlPanel.Controls.Add(btnStop);

            leftPanel.Controls.Add(ctlPanel);
        }

        private void BuildChartsAndParams()
        {
            var leftLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0, 10, 0, 0)
            };
            leftPanel.Controls.Add(leftLayout);

            var centerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Padding = new Padding(10),
                GrowStyle = TableLayoutPanelGrowStyle.AddRows
            };
            centerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            centerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            foreach (var s in sensors)
            {
                var p = MakeParamPanelForSensor(s);
                leftLayout.Controls.Add(p);
                paramPanelMap[s] = p;

                var lbl = new Label
                {
                    Text = s.Name,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    AutoSize = true,
                    Padding = new Padding(6)
                };

                var chartTime = NewChart($"{s.Name} (Time Domain)");
                var chartFreq = NewChart($"{s.Name} (Frequency Domain)");
                chartMap[s] = (chartTime, chartFreq);

                var statusLabel = new Label
                {
                    Text = "Status: -",
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    ForeColor = Color.Gray,
                    Dock = DockStyle.Bottom,
                    Height = 22,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(4, 0, 0, 0)
                };

                var timePanel = new Panel { Dock = DockStyle.Fill };
                chartTime.Dock = DockStyle.Fill;
                timePanel.Controls.Add(chartTime);
                timePanel.Controls.Add(statusLabel);

                statusLabelMap[s] = statusLabel;

                centerLayout.RowCount += 1;
                centerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                centerLayout.Controls.Add(lbl, 0, centerLayout.RowCount - 1);
                centerLayout.SetColumnSpan(lbl, 2);

                centerLayout.RowCount += 1;
                centerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 260));
                centerLayout.Controls.Add(timePanel, 0, centerLayout.RowCount - 1);
                centerLayout.Controls.Add(chartFreq, 1, centerLayout.RowCount - 1);
            }

            centerPanel.Controls.Add(centerLayout);
        }

        private Panel MakeParamPanelForSensor(ISensorModel s)
        {
            int numParams = 0;
            if (s is JSNSR04T) numParams = 4;
            else if (s is YFS201) numParams = 2;
            else if (s is YL83) numParams = 2;
            else if (s is SEN0161) numParams = 3;
            else if (s is DS18B20) numParams = 2;

            int panelHeight = 30 + (numParams * 55) + 40;

            var panel = new Panel
            {
                Width = 240,
                Height = panelHeight,
                BackColor = Color.WhiteSmoke,
                Margin = new Padding(6, 15, 6, 6),
                BorderStyle = BorderStyle.FixedSingle
            };

            var lbl = new Label
            {
                Text = s.Name,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                AutoSize = false,
                Height = 24,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 0, 0)
            };
            panel.Controls.Add(lbl);

            var sliderContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = panelHeight - 60,
                RowCount = numParams,
                ColumnCount = 2,
                Padding = new Padding(2)
            };

            sliderContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            sliderContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.Controls.Add(sliderContainer);

            int row = 0;
            Action<string, TrackBar, Label> AddParamRow = (labelText, trackBar, valueLabel) =>
            {
                var lblDesc = new Label
                {
                    Text = labelText,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                };

                var trackPanel = new Panel { Dock = DockStyle.Fill };
                valueLabel.Dock = DockStyle.Top;
                valueLabel.Width = 65;
                valueLabel.Height = 20;
                valueLabel.TextAlign = ContentAlignment.MiddleRight;

                trackBar.Dock = DockStyle.Top;
                trackBar.Height = 30;
                trackPanel.Controls.Add(trackBar);
                trackPanel.Controls.Add(valueLabel);

                sliderContainer.Controls.Add(lblDesc, 0, row);
                sliderContainer.Controls.Add(trackPanel, 1, row);
                sliderContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
                row++;
            };

            // IMPORTANT: slider scroll hanya update parameter + label (TANPA redraw)
            if (s is JSNSR04T jsn)
            {
                var trD = new TrackBar { Minimum = 1, Maximum = 600, Value = (int)jsn.P.Distance_cm, TickFrequency = 50 };
                var valD = new Label { Text = jsn.P.Distance_cm.ToString("0") };
                trD.Scroll += (o, e) => { jsn.P.Distance_cm = trD.Value; valD.Text = trD.Value.ToString(); };
                AddParamRow("Distance (cm):", trD, valD);

                var trAlpha = new TrackBar { Minimum = 0, Maximum = 500, Value = (int)(jsn.P.Alpha * 100) };
                var valAlpha = new Label { Text = jsn.P.Alpha.ToString("0.00") };
                trAlpha.Scroll += (o, e) => { jsn.P.Alpha = trAlpha.Value / 100.0; valAlpha.Text = jsn.P.Alpha.ToString("0.00"); };
                AddParamRow("Attenuation (α):", trAlpha, valAlpha);

                var trF0 = new TrackBar { Minimum = 20000, Maximum = 60000, Value = (int)jsn.P.F0, TickFrequency = 5000 };
                var valF0 = new Label { Text = jsn.P.F0.ToString("0") };
                trF0.Scroll += (o, e) => { jsn.P.F0 = trF0.Value; valF0.Text = trF0.Value.ToString(); };
                AddParamRow("F0 (Hz):", trF0, valF0);

                var trNoise = new TrackBar { Minimum = 0, Maximum = 500, Value = (int)(jsn.P.NoiseStd * 100) };
                var valNoise = new Label { Text = jsn.P.NoiseStd.ToString("0.00") };
                trNoise.Scroll += (o, e) => { jsn.P.NoiseStd = trNoise.Value / 100.0; valNoise.Text = jsn.P.NoiseStd.ToString("0.00"); };
                AddParamRow("Noise Std:", trNoise, valNoise);
            }
            else if (s is YFS201 yf)
            {
                var trQ = new TrackBar { Minimum = 1, Maximum = 30, Value = (int)yf.P.Flow_Lpm };
                var valQ = new Label { Text = yf.P.Flow_Lpm.ToString("0") };
                trQ.Scroll += (o, e) => { yf.P.Flow_Lpm = trQ.Value; valQ.Text = trQ.Value.ToString(); };
                AddParamRow("Flow (L/min) Q(t):", trQ, valQ);

                var trK = new TrackBar { Minimum = 10, Maximum = 150, Value = (int)(yf.P.K * 10) };
                var valK = new Label { Text = yf.P.K.ToString("0.0") };
                trK.Scroll += (o, e) => { yf.P.K = trK.Value / 10.0; valK.Text = yf.P.K.ToString("0.0"); };
                AddParamRow("K-Factor (k):", trK, valK);
            }
            else if (s is YL83 yl)
            {
                var trW = new TrackBar { Minimum = 0, Maximum = 100, Value = (int)(yl.P.Wetness * 100) };
                var valW = new Label { Text = yl.P.Wetness.ToString("0.00") };
                trW.Scroll += (o, e) => { yl.P.Wetness = trW.Value / 100.0; valW.Text = yl.P.Wetness.ToString("0.00"); };
                AddParamRow("Wetness (W(t)):", trW, valW);

                var trAlpha = new TrackBar { Minimum = 0, Maximum = 100, Value = (int)(yl.P.Alpha * 100) };
                var valAlpha = new Label { Text = yl.P.Alpha.ToString("0.00") };
                trAlpha.Scroll += (o, e) => { yl.P.Alpha = trAlpha.Value / 100.0; valAlpha.Text = yl.P.Alpha.ToString("0.00"); };
                AddParamRow("Filtering (α):", trAlpha, valAlpha);
            }
            else if (s is SEN0161 ph)
            {
                var trV = new TrackBar
                {
                    Minimum = 0,
                    Maximum = 300,
                    Value = (int)(Math.Max(0.0, Math.Min(3.0, ph.P.Vout)) * 100)
                };
                var valV = new Label { Text = ph.P.Vout.ToString("0.00") };
                trV.Scroll += (o, e) => { ph.P.Vout = trV.Value / 100.0; valV.Text = ph.P.Vout.ToString("0.00"); };
                AddParamRow("Vout (0–3V):", trV, valV);

                var trGain = new TrackBar { Minimum = 10, Maximum = 500, Value = (int)(ph.P.Gain * 100) };
                var valGain = new Label { Text = ph.P.Gain.ToString("0.00") };
                trGain.Scroll += (o, e) => { ph.P.Gain = trGain.Value / 100.0; valGain.Text = ph.P.Gain.ToString("0.00"); };
                AddParamRow("Gain:", trGain, valGain);

                var trOffset = new TrackBar { Minimum = -500, Maximum = 500, Value = (int)(ph.P.Offset * 100) };
                var valOffset = new Label { Text = ph.P.Offset.ToString("0.00") };
                trOffset.Scroll += (o, e) => { ph.P.Offset = trOffset.Value / 100.0; valOffset.Text = ph.P.Offset.ToString("0.00"); };
                AddParamRow("Offset:", trOffset, valOffset);
            }
            else if (s is DS18B20 tmp)
            {
                var trT = new TrackBar { Minimum = -55, Maximum = 125, Value = (int)tmp.P.TrueTemp };
                var valT = new Label { Text = tmp.P.TrueTemp.ToString("0") };
                trT.Scroll += (o, e) => { tmp.P.TrueTemp = trT.Value; valT.Text = tmp.P.TrueTemp.ToString(); };
                AddParamRow("True Temp (°C):", trT, valT);

                var trNoise = new TrackBar { Minimum = 0, Maximum = 500, Value = (int)(tmp.P.NoiseStd * 100) };
                var valNoise = new Label { Text = tmp.P.NoiseStd.ToString("0.00") };
                trNoise.Scroll += (o, e) => { tmp.P.NoiseStd = trNoise.Value / 100.0; valNoise.Text = tmp.P.NoiseStd.ToString("0.00"); };
                AddParamRow("Noise Std:", trNoise, valNoise);
            }

            // formula + sampling info
            var lblInfo = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Bottom,
                Font = new Font("Segoe UI", 8, FontStyle.Italic),
                ForeColor = Color.DimGray,
                Padding = new Padding(4)
            };

            if (s is JSNSR04T)
                lblInfo.Text = "JSN: echo waveform + time-of-flight\nFs = 100 kHz";
            else if (s is YFS201)
                lblInfo.Text = "f(t)=k·Q(t) + n(t)\nFs = 400 Hz";
            else if (s is YL83)
                lblInfo.Text = "Vout(t)=1+2e^{-αW(t)} + n(t)\nFs = 20 Hz";
            else if (s is SEN0161)
                lblInfo.Text = "τ dpH/dt + pH = pH_true(t)\nFs = 10 Hz";
            else if (s is DS18B20)
                lblInfo.Text = "T_meas updated (tCONV) + noise\nFs = 5 Hz";

            panel.Controls.Add(lblInfo);

            return panel;
        }

        private Chart NewChart(string title)
        {
            var chart = new Chart
            {
                Dock = DockStyle.Fill,
                Width = 480,
                Height = 240,
                BackColor = Color.WhiteSmoke
            };
            var ca = new ChartArea("c");
            ca.AxisX.MajorGrid.LineColor = Color.LightGray;
            ca.AxisY.MajorGrid.LineColor = Color.LightGray;
            chart.ChartAreas.Add(ca);
            chart.Legends.Add(new Legend("L") { Docking = Docking.Bottom });
            chart.Titles.Add(title);

            var s = new Series("data")
            {
                ChartType = SeriesChartType.Line,
                BorderWidth = 1
            };
            chart.Series.Add(s);
            return chart;
        }

        #endregion

        #region Right column + S/Z charts

        private void BuildRightColumn()
        {
            var top = new Panel
            {
                Dock = DockStyle.Top,
                Height = 240,
                BackColor = Color.LightGray
            };

            var pic3D = new PictureBox
            {
                Dock = DockStyle.Fill,
                Image = Properties.Resources.alat,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White
            };

            top.Controls.Add(pic3D);

            var sdomPanel = new Panel { Dock = DockStyle.Top, Height = 240, BackColor = Color.White };
            sChart = NewSPoleChart("S-domain Pole-Zero Plot (Stable: Re(s) < 0)");
            sdomPanel.Controls.Add(sChart);

            var zdomPanel = new Panel { Dock = DockStyle.Top, Height = 240, BackColor = Color.White };
            zChart = NewZPoleChart("Z-domain Pole-Zero Plot (Stable: |z| < 1)");
            zdomPanel.Controls.Add(zChart);

            rightPanel.Controls.Add(zdomPanel);
            rightPanel.Controls.Add(sdomPanel);
            rightPanel.Controls.Add(top);
        }

        private Chart NewSPoleChart(string title)
        {
            var chart = new Chart { Dock = DockStyle.Fill, BackColor = Color.WhiteSmoke };

            var area = new ChartArea("s");
            area.AxisX.MajorGrid.LineColor = Color.LightGray;
            area.AxisY.MajorGrid.LineColor = Color.LightGray;

            area.AxisX.Crossing = 0;
            area.AxisY.Crossing = 0;

            area.AxisX.Title = "Re(s)";
            area.AxisY.Title = "Im(s)";

            area.AxisX.Minimum = -6;
            area.AxisX.Maximum = 6;
            area.AxisY.Minimum = -4;
            area.AxisY.Maximum = 4;

            chart.ChartAreas.Add(area);

            var poles = new Series("Poles")
            {
                ChartType = SeriesChartType.Point,
                MarkerStyle = MarkerStyle.Cross,
                MarkerSize = 9,
                Color = Color.Red
            };
            chart.Series.Add(poles);

            var zeros = new Series("Zeros")
            {
                ChartType = SeriesChartType.Point,
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = 8,
                Color = Color.Blue
            };
            chart.Series.Add(zeros);

            var legend = new Legend("L");
            legend.Docking = Docking.Bottom;
            chart.Legends.Add(legend);

            chart.Titles.Add(title);

            return chart;
        }

        private Chart NewZPoleChart(string title)
        {
            var chart = new Chart { Dock = DockStyle.Fill, BackColor = Color.WhiteSmoke };

            var area = new ChartArea("z");
            area.AxisX.MajorGrid.LineColor = Color.LightGray;
            area.AxisY.MajorGrid.LineColor = Color.LightGray;

            area.AxisX.Crossing = 0;
            area.AxisY.Crossing = 0;

            area.AxisX.Minimum = -2;
            area.AxisX.Maximum = 2;
            area.AxisY.Minimum = -2;
            area.AxisY.Maximum = 2;

            area.AxisX.Title = "Re(z)";
            area.AxisY.Title = "Im(z)";
            chart.ChartAreas.Add(area);

            var poles = new Series("Poles")
            {
                ChartType = SeriesChartType.Point,
                MarkerStyle = MarkerStyle.Cross,
                MarkerSize = 9,
                Color = Color.Red
            };

            var zeros = new Series("Zeros")
            {
                ChartType = SeriesChartType.Point,
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = 8,
                Color = Color.Blue
            };

            var unit = new Series("UnitCircle")
            {
                ChartType = SeriesChartType.Line,
                BorderDashStyle = ChartDashStyle.Dash,
                Color = Color.Gray,
                BorderWidth = 2
            };

            chart.Series.Add(poles);
            chart.Series.Add(zeros);
            chart.Series.Add(unit);

            var legend = new Legend("L");
            legend.Docking = Docking.Bottom;
            chart.Legends.Add(legend);

            chart.Titles.Add(title);

            unit.Points.Clear();
            int N = 360;
            for (int i = 0; i <= N; i++)
            {
                double ang = i * Math.PI / 180.0;
                unit.Points.AddXY(Math.Cos(ang), Math.Sin(ang));
            }


            return chart;
        }

        
        private RectangleF GetInnerPlotRectangle(Chart chart, ChartArea area)
        {
            RectangleF caRect = new RectangleF(
                chart.ClientRectangle.Width * area.Position.X / 100f,
                chart.ClientRectangle.Height * area.Position.Y / 100f,
                chart.ClientRectangle.Width * area.Position.Width / 100f,
                chart.ClientRectangle.Height * area.Position.Height / 100f
            );

            RectangleF ipRect = new RectangleF(
                caRect.X + caRect.Width * area.InnerPlotPosition.X / 100f,
                caRect.Y + caRect.Height * area.InnerPlotPosition.Y / 100f,
                caRect.Width * area.InnerPlotPosition.Width / 100f,
                caRect.Height * area.InnerPlotPosition.Height / 100f
            );

            return ipRect;
        }

        #endregion

        #region Timer + plotting (STREAMING)

        private void Timer_Tick(object sender, EventArgs e)
        {
            double dtTick = timer.Interval / 1000.0;
            double tNew = globalTime + dtTick;

            double jsDisplayWindow = 0.025;
            double defaultWindow = windowSec;

            foreach (var s in sensors)
            {
                double sr;
                double localWindow;

                if (s is JSNSR04T) { sr = 100000.0; localWindow = jsDisplayWindow; }
                else if (s is YFS201) { sr = 400; localWindow = defaultWindow; }
                else if (s is YL83) { sr = 20; localWindow = defaultWindow; }
                else if (s is SEN0161) { sr = 10; localWindow = defaultWindow; }
                else if (s is DS18B20) { sr = 5; localWindow = defaultWindow; }
                else { sr = sampleRate; localWindow = defaultWindow; }

                int chunkN = Math.Max(1, (int)Math.Round(sr * dtTick));
                if (s is JSNSR04T) chunkN = Math.Min(chunkN, 2000);

                double[] tChunk = new double[chunkN];

                // chunk harus "nempel" ke waktu sekarang (tNew)
                double tStartChunk = tNew - (chunkN / sr);
                for (int i = 0; i < chunkN; i++)
                    tChunk[i] = tStartChunk + i / sr;

                double[] yChunk = s.Generate(tChunk);

                var th = timeHistory[s];
                var yh = signalHistory[s];

                th.AddRange(tChunk);
                yh.AddRange(yChunk);

                double tCut = tNew - localWindow;
                while (th.Count > 0 && th[0] < tCut)
                {
                    th.RemoveAt(0);
                    yh.RemoveAt(0);
                }

                var (timeChart, freqChart) = chartMap[s];

                PlotTime(timeChart, th.ToArray(), yh.ToArray());

                // FFT window
                int fftN = Math.Min(2048, yh.Count);
                if (fftN >= 16)
                {
                    double[] yFft = yh.Skip(yh.Count - fftN).Take(fftN).ToArray();

                    // DC removal
                    double mean = yFft.Average();
                    for (int i = 0; i < yFft.Length; i++) yFft[i] -= mean;

                    // Hann window
                    ApplyHann(yFft);

                    var (freqs, mags) = FFTService.ComputeFFT(yFft, sr);
                    double fMax = (s is JSNSR04T) ? Math.Min(sr / 2.0, 50000.0) : sr / 2.0;
                    PlotFreq(freqChart, freqs, mags, fMax);
                }

                double currentValue = yh.Count > 0 ? yh[yh.Count - 1] : 0.0;
                UpdateStatusLabel(s, currentValue);
            }

            // domain s/z
            var sPoles = GetSystemPoles();
            var sZeros = GetSystemZeros();

            DrawSPoleZeroPlot(sChart, sPoles, sZeros);

            double Ts = 1.0; // normalisasi
            var zPoles = sPoles.Select(p => Complex.Exp(p * Ts)).ToList();
            var zZeros = sZeros.Select(z => Complex.Exp(z * Ts)).ToList();
            DrawZPoleZeroPlot(zChart, zPoles, zZeros);

            globalTime = tNew;
        }

        private void ApplyHann(double[] x)
        {
            int N = x.Length;
            if (N <= 1) return;
            for (int n = 0; n < N; n++)
            {
                double w = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * n / (N - 1)));
                x[n] *= w;
            }
        }

        private void PlotTime(Chart chart, double[] x, double[] y)
        {
            var s = chart.Series[0];
            s.Points.Clear();

            if (x == null || y == null || x.Length == 0 || y.Length == 0)
                return;

            int step = Math.Max(1, x.Length / 1000);
            double t0 = x[0];

            for (int i = 0; i < x.Length; i += step)
                s.Points.AddXY(x[i] - t0, y[i]);

            chart.ChartAreas[0].RecalculateAxesScale();
        }

        private void PlotFreq(Chart chart, float[] freqs, float[] mags, double fMax)
        {
            var s = chart.Series[0];
            s.Points.Clear();

            if (freqs == null || mags == null || freqs.Length == 0 || mags.Length == 0)
                return;

            int len = Math.Min(freqs.Length, mags.Length);
            int limit = len;

            for (int i = 0; i < len; i++)
            {
                if (freqs[i] > fMax) { limit = i; break; }
            }

            int step = Math.Max(1, limit / 500);
            for (int i = 0; i < limit; i += step)
                s.Points.AddXY(freqs[i], mags[i]);

            chart.ChartAreas[0].AxisX.Minimum = 0;
            chart.ChartAreas[0].AxisX.Maximum = fMax;
            chart.ChartAreas[0].RecalculateAxesScale();
        }

        #endregion

        #region Status logic (Aman / Siaga / Bahaya)

        private void UpdateStatusLabel(ISensorModel sensor, double currentValue)
        {
            if (!statusLabelMap.TryGetValue(sensor, out var lbl))
                return;

            string status = "-";
            Color color = Color.Gray;

            if (sensor is JSNSR04T jsn)
            {
                double d = jsn.P.Distance_cm;

                if (d > 50) { status = "AMAN"; color = Color.Green; }
                else if (d >= 20) { status = "SIAGA"; color = Color.Orange; }
                else { status = "BAHAYA"; color = Color.Red; }

                double tEchoMs = 0.0;
                try { tEchoMs = jsn.LastEchoTimeSec * 1000.0; } catch { }

                lbl.Text = $"Status: {status} (d = {d:0} cm, t_echo = {tEchoMs:0.00} ms)";
            }
            else if (sensor is YFS201 yf)
            {
                double q = yf.P.Flow_Lpm;

                if (q <= 10) { status = "AMAN"; color = Color.Green; }
                else if (q <= 20) { status = "SIAGA"; color = Color.Orange; }
                else { status = "BAHAYA"; color = Color.Red; }

                lbl.Text = $"Status: {status} (Q = {q:0} L/min)";
            }
            else if (sensor is YL83 yl)
            {
                double rain = yl.P.Wetness * 100.0;

                if (rain < 10) { status = "AMAN"; color = Color.Green; }
                else if (rain <= 55) { status = "SIAGA"; color = Color.Orange; }
                else { status = "BAHAYA"; color = Color.Red; }

                lbl.Text = $"Status: {status} (~{rain:0} mm/jam)";
            }
            else if (sensor is SEN0161)
            {
                double pH = currentValue;

                if (pH >= 6.5 && pH <= 8.5)
                {
                    status = "AMAN"; color = Color.Green;
                }
                else if ((pH >= 5.5 && pH < 6.5) || (pH > 8.5 && pH <= 9.5))
                {
                    status = "SIAGA"; color = Color.Orange;
                }
                else
                {
                    status = "BAHAYA"; color = Color.Red;
                }

                lbl.Text = $"Status: {status} (pH = {pH:0.00})";
            }
            else if (sensor is DS18B20)
            {
                double temp = currentValue;

                if (temp >= 0 && temp <= 50)
                {
                    status = "AMAN"; color = Color.Green;
                }
                else
                {
                    status = "SIAGA"; color = Color.Orange;
                }

                lbl.Text = $"Status: {status} (T = {temp:0.0} °C)";
            }

            lbl.ForeColor = color;
        }

        #endregion

        #region S/Z-domain helpers (poles/zeros) + drawing

        private List<Complex> GetSystemPoles()
        {
            // ini “model edukasi” biar ada titik dinamis buat s-plane,
            // ngikut parameter sensor kamu (omega0, beta, tau).
            double omega0 = 3.0; // dari F0
            double beta = 1.0;   // dari alpha YL83
            double tauPh = 5.0;  // dari tau pH

            var jsn = sensors.OfType<JSNSR04T>().FirstOrDefault();
            if (jsn != null)
            {
                omega0 = jsn.P.F0 / 10000.0; // 20k..60k -> 2..6 (skala visual)
            }

            var yl = sensors.OfType<YL83>().FirstOrDefault();
            if (yl != null)
            {
                beta = yl.P.Alpha * 5.0;
                if (beta <= 0) beta = 0.5;
            }

            var ph = sensors.OfType<SEN0161>().FirstOrDefault();
            if (ph != null)
            {
                tauPh = Math.Max(0.1, ph.P.Tau);
            }

            var poles = new List<Complex>
            {
                new Complex(0,  omega0),
                new Complex(0, -omega0),
                new Complex(-beta, 0),
                new Complex(-1.0 / tauPh, 0),
                Complex.Zero,
                Complex.Zero
            };

            return poles;
        }

        private List<Complex> GetSystemZeros()
        {
            // contoh sederhana: ada zero di origin
            return new List<Complex> { Complex.Zero };
        }

        private void DrawSPoleZeroPlot(Chart chart, List<Complex> poles, List<Complex> zeros)
        {
            if (chart == null || chart.Series.Count == 0) return;

            var area = chart.ChartAreas[0];
            var sPoles = chart.Series["Poles"];
            var sZeros = chart.Series["Zeros"];

            sPoles.Points.Clear();
            sZeros.Points.Clear();

            foreach (var p in poles)
                sPoles.Points.AddXY(p.Real, p.Imaginary);

            foreach (var z in zeros)
                sZeros.Points.AddXY(z.Real, z.Imaginary);

            double maxRe = Math.Max(6, poles.Concat(zeros).Select(p => Math.Abs(p.Real)).DefaultIfEmpty(1).Max() + 1);
            double maxIm = Math.Max(4, poles.Concat(zeros).Select(p => Math.Abs(p.Imaginary)).DefaultIfEmpty(1).Max() + 1);

            area.AxisX.Minimum = -maxRe;
            area.AxisX.Maximum = maxRe;
            area.AxisY.Minimum = -maxIm;
            area.AxisY.Maximum = maxIm;
        }

        private void DrawZPoleZeroPlot(Chart chart, List<Complex> zPoles, List<Complex> zZeros)
        {
            if (chart == null) return;

            var area = chart.ChartAreas[0];
            var sPoles = chart.Series["Poles"];
            var sZeros = chart.Series["Zeros"];
            var unit = chart.Series["UnitCircle"];

            sPoles.Points.Clear();
            sZeros.Points.Clear();

            // unit circle refresh
            unit.Points.Clear();
            int N = 360;
            for (int i = 0; i <= N; i++)
            {
                double ang = i * Math.PI / 180.0;
                unit.Points.AddXY(Math.Cos(ang), Math.Sin(ang));
            }

            foreach (var p in zPoles)
                sPoles.Points.AddXY(p.Real, p.Imaginary);

            foreach (var z in zZeros)
                sZeros.Points.AddXY(z.Real, z.Imaginary);

            area.AxisX.Minimum = -2;
            area.AxisX.Maximum = 2;
            area.AxisY.Minimum = -2;
            area.AxisY.Maximum = 2;
        }

        #endregion
    }
}
