using System;

namespace floodrisk2.Models
{
    // pH(t) = 7 - (Vout(t) - 2.5) / 0.18
    // Tambahan: model orde-1 agar punya pole di domain-S & Z
    // τ dpH/dt + pH = pH_true

    public class SEN0161 : ISensorModel
    {
        public string Name => "SEN0161 (pH)";
        public string Formula => "pH_meas(t) = 7 - (Vout - 2.5)/0.18 with first-order dynamic lag";

        public class Params
        {
            // ==== PARAMETER LAMA ====
            public double Vout { get; set; } = 2.5;
            public double NoiseStd { get; set; } = 0.01;
            public double Gain { get; set; } = 1.0;
            public double Offset { get; set; } = 0.0;

            public double TruePH { get; set; } = 7.0;
            public double OffsetVolt { get; set; } = 2.5;
            public double Slope { get; set; } = 0.18;

            // ==== PARAMETER BARU ====

            // konstanta waktu respon sensor pH
            public double Tau { get; set; } = 5.0;

            // optional: fluktuasi lambat (biar terlihat dinamis di time-domain)
            public double DynamicAmp { get; set; } = 0.05;
            public double DynamicFreq { get; set; } = 0.01; // 0.01 Hz
        }

        public Params P { get; set; } = new Params();

        private readonly Random rnd = new Random();

        private double lastPH = 7.0;
        private double lastTime = 0.0;

        public double[] Generate(double[] t)
        {
            int N = t.Length;
            double[] ph = new double[N];

            for (int i = 0; i < N; i++)
            {
                double ti = t[i];
                if (i == 0) lastTime = ti;

                double dt = ti - lastTime;
                if (dt <= 0) dt = 0.001;

                // ---- Vout measurement (anggap P.Vout adalah tegangan sensor) ----
                double noise = Gaussian(0.0, P.NoiseStd);

                // Tegangan efektif setelah gain + offset
                double vout = (P.Vout + noise) * P.Gain + P.Offset;

                // Optional: batasi tegangan ke rentang ADC/sensor (mis. 0–5V)
                vout = Math.Max(0.0, Math.Min(5.0, vout));

                // pH hasil konversi tegangan
                double pH_true = 7.0 - (vout - P.OffsetVolt) / P.Slope;

                // optional: fluktuasi lambat
                pH_true += P.DynamicAmp * Math.Sin(2 * Math.PI * P.DynamicFreq * ti);

                // ---- FIRST-ORDER dynamic lag (lebih stabil dari dt/tau) ----
                double alpha = 1.0 - Math.Exp(-dt / Math.Max(0.001, P.Tau));
                lastPH = lastPH + alpha * (pH_true - lastPH);

                // Clamp pH ke range sensor 0–14 (biar tidak minus / >14)
                lastPH = Math.Max(0.0, Math.Min(14.0, lastPH));

                ph[i] = lastPH;
                lastTime = ti;
            }

            return ph;
        }


        private double Gaussian(double mu, double sigma)
        {
            double u1 = 1.0 - rnd.NextDouble();
            double u2 = 1.0 - rnd.NextDouble();
            double r = Math.Sqrt(-2.0 * Math.Log(u1));
            double th = 2.0 * Math.PI * u2;
            return mu + sigma * r * Math.Cos(th);
        }
    }
}
