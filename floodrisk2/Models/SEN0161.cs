using System;

namespace floodrisk2.Models
{
    // Model:
    // Vout(t) = measurement voltage (0..3V) + noise, with gain/offset
    // pH_true(t) = pH0 - (Vout(t)-V0)/S
    // Dynamic (orde-1): τ dpH/dt + pH = pH_true

    public class SEN0161 : ISensorModel
    {
        public string Name => "SEN0161 (pH)";

        public string Formula =>
            "pH(t)=pH0-(Vout(t)-V0)/S + n(t);  dynamic: τ dpH/dt + pH = pH_true";

        public class Params
        {
            // Tegangan yang “diinput user” / baseline output sensor
            public double Vout { get; set; } = 2.5;

            // Noise std (volt)
            public double NoiseStd { get; set; } = 0.01;

            // Gain/offset penguat (opsional)
            public double Gain { get; set; } = 1.0;
            public double Offset { get; set; } = 0.0;

            // Parameter kalibrasi (default mengikuti rumus awalmu)
            // pH = 7 - (Vout - 2.5)/0.18
            public double PH0 { get; set; } = 7.0;        // pH0
            public double V0 { get; set; } = 2.5;         // V0 (volt)
            public double Slope { get; set; } = 0.18;     // S (V/pH)

            // Konstanta waktu (s)
            public double Tau { get; set; } = 5.0;

            // Opsional: fluktuasi lambat pH agar terlihat dinamis
            public double DynamicAmp { get; set; } = 0.05;
            public double DynamicFreq { get; set; } = 0.01; // Hz
        }

        public Params P { get; set; } = new Params();

        private readonly Random rnd = new Random();

        private double lastPH = 7.0;
        private double lastTime = double.NaN;

        public double[] Generate(double[] t)
        {
            int N = t.Length;
            double[] ph = new double[N];

            for (int i = 0; i < N; i++)
            {
                double ti = t[i];

                if (double.IsNaN(lastTime))
                    lastTime = ti;

                double dt = ti - lastTime;
                if (dt <= 0) dt = 0.001;

                // --- Voltage measurement (0..3V untuk board SEN0161 V2) ---
                double noiseV = Gaussian(0.0, P.NoiseStd);

                double vout = (P.Vout + noiseV) * P.Gain + P.Offset;

                // CLAMP 0..3V (SEN0161 V2 output range)
                vout = Math.Max(0.0, Math.Min(3.0, vout));

                // --- Convert voltage to pH (parameter kalibrasi) ---
                double slope = (Math.Abs(P.Slope) < 1e-6) ? 1e-6 : P.Slope;
                double pH_true = P.PH0 - (vout - P.V0) / slope;

                // optional slow fluctuation
                pH_true += P.DynamicAmp * Math.Sin(2.0 * Math.PI * P.DynamicFreq * ti);

                // --- First-order lag: stable alpha form ---
                double tau = Math.Max(0.001, P.Tau);
                double alpha = 1.0 - Math.Exp(-dt / tau);
                lastPH = lastPH + alpha * (pH_true - lastPH);

                // clamp pH to physical range
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
