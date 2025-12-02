using System;

namespace floodrisk2.Models
{
    public class YL83 : ISensorModel
    {
        public string Name => "YL-83 (Rain)";
        public string Formula => "Vout(t) = 1.0 + 2.0 e^{-α W(t)} + n(t)";

        public class Params
        {
            // Wetness rata-rata (0..1) dikontrol slider
            public double Wetness { get; set; } = 0.2;

            // Koefisien eksponensial
            public double Alpha { get; set; } = 0.5;

            // Noise tegangan
            public double NoiseStd { get; set; } = 0.01;

            // --- parameter DINAMIS ---

            // Amplitudo fluktuasi W(t) (0..1)
            public double DynamicAmp { get; set; } = 0.3;

            // Frekuensi fluktuasi hujan (Hz), misal 0.02 Hz = periode 50 s
            public double RainFreqHz { get; set; } = 0.02;
        }

        public Params P { get; set; } = new Params();
        private readonly Random rnd = new Random();

        public double[] Generate(double[] t)
        {
            int N = t.Length;
            double[] v = new double[N];

            double W0 = Math.Max(0.0, Math.Min(1.0, P.Wetness));
            double A = P.DynamicAmp;
            double fR = P.RainFreqHz;

            for (int i = 0; i < N; i++)
            {
                double ti = t[i];

                // 1) Wetness dinamis: W(t) = W0 + A sin(2π fR t) + noise kecil
                double wDyn = W0 + A * Math.Sin(2.0 * Math.PI * fR * ti)
                                   + 0.05 * Gaussian(0.0, 1.0);

                // clamp ke 0..1
                double Wt = Math.Max(0.0, Math.Min(1.0, wDyn));

                // 2) Tegangan sesuai rumus: Vout = 1.0 + 2.0 e^{-α W(t)} + noise
                double baseV = 1.0 + 2.0 * Math.Exp(-P.Alpha * Wt);

                double noise = Gaussian(0.0, P.NoiseStd);

                v[i] = baseV + noise;
            }

            return v;
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
