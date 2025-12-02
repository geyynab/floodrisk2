using System;

namespace floodrisk2.Models
{
    public class JSNSR04T : ISensorModel
    {
        public string Name => "JSN-SR04T (Water level)";

        // Sesuai dengan tabel rumus simulasi
        public string Formula =>
            "s(t) = A e^{-αd} cos(2πf0 ( t - 2d/c(T) )) cos^n(θ) + n(t)";

        public class Params
        {
            // Jarak air ke sensor (cm)
            public double Distance_cm { get; set; } = 100.0;   // d (cm)

            // Frekuensi ultrasonic (Hz)
            public double F0 { get; set; } = 40000.0;          // f0 (Hz)

            // Koefisien atenuasi jarak
            public double Alpha { get; set; } = 0.005;         // α

            // Temperatur lingkungan (°C)
            public double TempC { get; set; } = 20.0;          // T (°C)

            // Amplitudo dasar A (sebelum faktor jarak & sudut)
            public double Amplitude { get; set; } = 1.0;       // A

            // Sudut beam ultrasonic (derajat) untuk faktor cos^n(θ)
            public double ThetaDeg { get; set; } = 0.0;        // θ (deg)

            // Orde cos^n(θ) → n
            public double AngleOrder { get; set; } = 1.0;      // n

            // Standar deviasi noise n(t)
            public double NoiseStd { get; set; } = 0.01;       // σ
        }

        public Params P { get; set; } = new Params();

        private readonly Random rnd = new Random();

        public double[] Generate(double[] t)
        {
            int N = t.Length;
            double[] s = new double[N];

            // d dalam meter, dibatasi 0.2–6 m (range JSN-SR04T praktis)
            double d_m = Math.Max(0.2, Math.Min(6.0, P.Distance_cm / 100.0));

            // Kecepatan suara (m/s) sebagai fungsi temperatur (aproksimasi)
            double c = 331.4 + 0.606 * P.TempC;

            // Waktu tempuh bolak-balik 2d/c(T)
            double tEcho = 2.0 * d_m / c;

            // Faktor atenuasi jarak e^{-α d}
            double distAtt = Math.Exp(-P.Alpha * d_m);

            // Faktor sudut cos^n(θ)
            double thetaRad = P.ThetaDeg * Math.PI / 180.0;
            double angleFactor = Math.Pow(Math.Cos(thetaRad), P.AngleOrder);

            // Amplitudo efektif: A e^{-αd} cos^n(θ)
            double Aeff = P.Amplitude * distAtt * angleFactor;

            for (int i = 0; i < N; i++)
            {
                double ti = t[i];

                // Komponen utama: echo teredam & ter-delay
                double echo = Aeff * Math.Cos(2.0 * Math.PI * P.F0 * (ti - tEcho));

                // Noise Gaussian n(t)
                double noise = Gaussian(0.0, P.NoiseStd);

                // Sinyal total
                s[i] = echo + noise;
            }

            return s;
        }

        private double Gaussian(double mu, double sigma)
        {
            // Box-Muller transform
            double u1 = 1.0 - rnd.NextDouble();
            double u2 = 1.0 - rnd.NextDouble();
            double r = Math.Sqrt(-2.0 * Math.Log(u1));
            double th = 2.0 * Math.PI * u2;
            return mu + sigma * r * Math.Cos(th);
        }
    }
}
