using System;

namespace floodrisk2.Models
{
    // T_meas = T_true + n(t) + drift(t)
    public class DS18B20 : ISensorModel
    {
        public string Name => "DS18B20 (Temp)";
        public string Formula => "T_meas = T_true + n(t) + b(t)";

        public class Params
        {
            public double TrueTemp { get; set; } = 25.0;
            public double NoiseStd { get; set; } = 0.1;
            public double DriftPerSec { get; set; } = 0.0;

            public bool EnableQuantization { get; set; } = false;
        }

        public Params P { get; set; } = new Params();
        private readonly Random rnd = new Random();

        public double[] Generate(double[] t)
        {
            int N = t.Length;
            double[] s = new double[N];

            for (int i = 0; i < N; i++)
            {
                double tn = t[i];
                double noise = Gaussian(0.0, P.NoiseStd);
                double drift = P.DriftPerSec * tn;

                double temp = P.TrueTemp + noise + drift;

                if (P.EnableQuantization)
                    temp = Math.Round(temp / 0.0625) * 0.0625;

                s[i] = temp;
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
