using System;

namespace floodrisk2.Models
{
    // Model:
    // T_meas(t) = QΔ{ T_true(t) + drift(t) + n(t) }
    // Updated every tCONV (sample/hold), because DS18B20 has conversion time.

    public class DS18B20 : ISensorModel
    {
        public string Name => "DS18B20 (Temp)";
        public string Formula => "T_meas(t)=QΔ{T_true(t)+n(t)+b(t)} updated every tCONV";

        public class Params
        {
            public double TrueTemp { get; set; } = 25.0;    // °C
            public double NoiseStd { get; set; } = 0.1;     // °C
            public double DriftPerSec { get; set; } = 0.0;  // °C/s

            // Quantization/resolution
            public bool EnableQuantization { get; set; } = true;

            // step size ΔT (12-bit default = 0.0625°C)
            public double ResolutionStep { get; set; } = 0.0625;

            // conversion time (12-bit default ~0.75 s)
            public double ConversionTimeSec { get; set; } = 0.75;
        }

        public Params P { get; set; } = new Params();
        private readonly Random rnd = new Random();

        private double lastUpdateTime = double.NegativeInfinity;
        private double lastValue = 25.0;

        public double[] Generate(double[] t)
        {
            int N = t.Length;
            double[] s = new double[N];

            double tConv = Math.Max(0.01, P.ConversionTimeSec);

            for (int i = 0; i < N; i++)
            {
                double ti = t[i];

                // update hanya setiap tCONV (sample-and-hold)
                if (ti - lastUpdateTime >= tConv || double.IsNegativeInfinity(lastUpdateTime))
                {
                    double noise = Gaussian(0.0, P.NoiseStd);
                    double drift = P.DriftPerSec * ti;

                    double temp = P.TrueTemp + noise + drift;

                    if (P.EnableQuantization)
                    {
                        double step = Math.Max(1e-6, P.ResolutionStep);
                        temp = Math.Round(temp / step) * step;
                    }

                    lastValue = temp;
                    lastUpdateTime = ti;
                }

                s[i] = lastValue;
            }

            return s;
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
