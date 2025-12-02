using System;

namespace floodrisk2.Models
{
    // f(t) = k Q(t) ; simulate pulse train of Hall effect pulses
    public class YFS201 : ISensorModel
    {
        public string Name => "YF-S201 (Flow)";
        public string Formula => "f(t) = k * Q(t)   (k ~ 7.5 Hz per L/min)";

        public class Params
        {
            public double K { get; set; } = 7.5; // Hz per L/min
            public double Flow_Lpm { get; set; } = 5.0; // L/min
            public double NoiseStd { get; set; } = 0.02;

            // Amplitude real-world = 5V, normalized = 1.0
            public double PulseAmplitude { get; set; } = 5.0;
        }

        public Params P { get; set; } = new Params();
        private readonly Random rnd = new Random();

        // returns analog-ish pulse waveform sampled at t
        public double[] Generate(double[] t)
        {
            int N = t.Length;
            double[] s = new double[N];

            double freqHz = Math.Max(0.001, P.K * P.Flow_Lpm);
            double period = 1.0 / freqHz;
            double width = period * 0.2; // 20% duty cycle

            for (int i = 0; i < N; i++)
            {
                double ti = t[i];
                double phase = ti % period;

                double pulse = (phase < width) ? P.PulseAmplitude : 0.0;
                double noise = Gaussian(0.0, P.NoiseStd);

                s[i] = pulse + noise;
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
