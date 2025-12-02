using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace floodrisk2
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        private Panel panelLeft;
        private GroupBox grpJSN, grpYF, grpYL, grpPH, grpTemp;

        private Label lblJsnDist, lblJsnAmp, lblJsnAlpha, lblJsnNoise;
        private NumericUpDown nudJsnDist, nudJsnAmp, nudJsnAlpha, nudJsnNoise;
        private TrackBar tbJsnDist, tbJsnAmp, tbJsnAlpha, tbJsnNoise;

        private Label lblFlow, lblKFactor;
        private NumericUpDown nudFlow, nudKFactor;
        private TrackBar tbFlow, tbKFactor;

        private Label lblWet, lblYLAlpha;
        private NumericUpDown nudWet, nudYLAlpha;
        private TrackBar tbWet, tbYLAlpha;

        private Label lblVout, lblGain, lblOffset;
        private NumericUpDown nudVout, nudGain, nudOffset;
        private TrackBar tbVout, tbGain, tbOffset;

        private Label lblTempTrue, lblTempNoise;
        private NumericUpDown nudTempTrue, nudTempNoise;
        private TrackBar tbTempTrue, tbTempNoise;

        private Chart chartTime, chartFFT;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();

            this.ClientSize = new System.Drawing.Size(1600, 900);
            this.Text = "Flood Risk Dashboard";
            this.BackColor = System.Drawing.Color.White;

            // ============================================
            // LEFT PANEL FIXED
            // ============================================
            panelLeft = new Panel();
            panelLeft.Dock = DockStyle.Left;
            panelLeft.Width = 380;           // <<< Diperlebar agar NumericUpDown terlihat
            panelLeft.AutoScroll = true;
            panelLeft.BackColor = System.Drawing.Color.FromArgb(235, 235, 235);
            this.Controls.Add(panelLeft);

            // Helper creators
            GroupBox MakeGroup(string t)
            {
                return new GroupBox()
                {
                    Text = t,
                    Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold),
                    Width = 350,
                    Height = 300,             // <<< Lebih tinggi
                    Padding = new Padding(10),
                    Margin = new Padding(10)
                };
            }

            NumericUpDown MakeNUD(decimal min, decimal max, decimal val)
            {
                return new NumericUpDown()
                {
                    Minimum = min,
                    Maximum = max,
                    Value = val,
                    DecimalPlaces = 2,
                    Width = 120,                 // <<< Diperbesar
                    Font = new System.Drawing.Font("Segoe UI", 10)
                };
            }

            TrackBar MakeTB(int min, int max)
            {
                return new TrackBar()
                {
                    Minimum = min,
                    Maximum = max,
                    TickFrequency = (max - min) / 10,
                    Width = 250,                // <<< diperlebar
                };
            }

            Label MakeLabel(string t)
            {
                return new Label()
                {
                    Text = t,
                    Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold),
                    AutoSize = true,
                };
            }

            int posY;

            // ============================================
            // JSN-SR04T BLOCK
            // ============================================
            grpJSN = MakeGroup("JSN-SR04T (Water level)");
            posY = 30;

            lblJsnDist = MakeLabel("Distance (cm):");
            lblJsnDist.Top = posY; lblJsnDist.Left = 10;

            nudJsnDist = MakeNUD(20, 600, 100);
            nudJsnDist.Top = posY + 25; nudJsnDist.Left = 10;

            tbJsnDist = MakeTB(20, 600);
            tbJsnDist.Top = posY + 65; tbJsnDist.Left = 10;

            posY += 110;

            lblJsnAmp = MakeLabel("Amplitude (A):");
            lblJsnAmp.Top = posY; lblJsnAmp.Left = 10;

            nudJsnAmp = MakeNUD(0, 10, 1);
            nudJsnAmp.Top = posY + 25; nudJsnAmp.Left = 10;

            tbJsnAmp = MakeTB(0, 10);
            tbJsnAmp.Top = posY + 65; tbJsnAmp.Left = 10;

            grpJSN.Controls.AddRange(new Control[]
            {
                lblJsnDist, nudJsnDist, tbJsnDist,
                lblJsnAmp, nudJsnAmp, tbJsnAmp
            });

            panelLeft.Controls.Add(grpJSN);

            // ============================================
            // YF-S201
            // ============================================
            grpYF = MakeGroup("YF-S201 (Flow)");
            posY = 30;

            lblFlow = MakeLabel("Flow (L/min):");
            lblFlow.Top = posY; lblFlow.Left = 10;

            nudFlow = MakeNUD(0, 30, 5);
            nudFlow.Top = posY + 25; nudFlow.Left = 10;

            tbFlow = MakeTB(0, 30);
            tbFlow.Top = posY + 65; tbFlow.Left = 10;

            posY += 110;

            lblKFactor = MakeLabel("K-Factor:");
            lblKFactor.Top = posY; lblKFactor.Left = 10;

            nudKFactor = MakeNUD(1, 20, 7);
            nudKFactor.Top = posY + 25; nudKFactor.Left = 10;

            tbKFactor = MakeTB(1, 20);
            tbKFactor.Top = posY + 65; tbKFactor.Left = 10;

            grpYF.Controls.AddRange(new Control[]
            {
                lblFlow,nudFlow,tbFlow,
                lblKFactor,nudKFactor,tbKFactor
            });

            panelLeft.Controls.Add(grpYF);

            // ============================================
            // YL-83 RAIN
            // ============================================
            grpYL = MakeGroup("YL-83 (Rain)");

            posY = 30;

            lblWet = MakeLabel("Wetness:");
            lblWet.Top = posY; lblWet.Left = 10;

            nudWet = MakeNUD(0, 10, 0);
            nudWet.Top = posY + 25; nudWet.Left = 10;

            tbWet = MakeTB(0, 10);
            tbWet.Top = posY + 65; tbWet.Left = 10;

            posY += 110;

            lblYLAlpha = MakeLabel("Filtering α:");
            lblYLAlpha.Top = posY; lblYLAlpha.Left = 10;

            nudYLAlpha = MakeNUD(0, 5, 0.5m);
            nudYLAlpha.Top = posY + 25; nudYLAlpha.Left = 10;

            tbYLAlpha = MakeTB(0, 5);
            tbYLAlpha.Top = posY + 65; tbYLAlpha.Left = 10;

            grpYL.Controls.AddRange(new Control[]
            {
                lblWet,nudWet,tbWet,
                lblYLAlpha,nudYLAlpha,tbYLAlpha
            });

            panelLeft.Controls.Add(grpYL);

            // ============================================
            // CHARTS
            // ============================================
            chartTime = new Chart();
            chartFFT = new Chart();

            chartTime.Parent = this;
            chartFFT.Parent = this;

            chartTime.SetBounds(400, 20, 1150, 350);
            chartFFT.SetBounds(400, 400, 1150, 350);

            var ca1 = new ChartArea("Time");
            var ca2 = new ChartArea("FFT");

            chartTime.ChartAreas.Add(ca1);
            chartFFT.ChartAreas.Add(ca2);

            chartTime.Series.Add("data");
            chartFFT.Series.Add("data");

            chartTime.Series["data"].ChartType =
                SeriesChartType.Line;

            chartFFT.Series["data"].ChartType =
                SeriesChartType.Line;
        }
    }
}
