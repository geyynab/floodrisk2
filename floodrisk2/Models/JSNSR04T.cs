using System;

namespace floodrisk2.Models
{
    public class JSNSR04T : ISensorModel
    {
        public string Name => "JSN-SR04T (Water level)";

        // Model rapi: output utama = time-of-flight / pulse width
        // plus sinyal echo (40 kHz) untuk visualisasi/FFT
        public string Formula =>
            "y(t)=t_echo=2d/c(T)+n(t);  s(t)=Aeff cos(2πf0(t-t_echo))·u(t-t_echo)+n(t)";

        public class Params
        {
            // Jarak air ke sensor (cm)
            public double Distance_cm { get; set; } = 100.0;

            // Frekuensi ultrasonic (Hz)
            public double F0 { get; set; } = 40000.0;

            // Koefisien atenuasi jarak (1/m)
            public double Alpha { get; set; } = 0.005;

            // Temperatur lingkungan (°C)
            public double TempC { get; set; } = 20.0;

            // Amplitudo dasar
            public double Amplitude { get; set; } = 1.0;

            // Sudut beam ultrasonic (derajat)
            public double ThetaDeg { get; set; } = 0.0;

            // Orde cos^n(theta)
            public double AngleOrder { get; set; } = 1.0;

            // Noise std
            public double NoiseStd { get; set; } = 0.01;

            // Durasi paket echo (agar tidak sinus terus-menerus)
            public double EchoWindowSec { get; set; } = 0.001; // 1 ms
        }

        public Params P { get; set; } = new Params();

        private readonly Random rnd = new Random();

        // Output utama sensor (time-of-flight / pulse width)
        public double LastEchoTimeSec { get; private set; } = 0.0;

        // Kecepatan suara sebagai fungsi temperatur (aproksimasi)
        private static double SpeedOfSound(double tempC) => 331.4 + 0.606 * tempC;

        public double[] Generate(double[] t)
        {
            int N = t.Length;
            double[] s = new double[N];

            // Range JSN-SR04T: 21 cm – 600 cm
            double d_m = Math.Max(0.21, Math.Min(6.0, P.Distance_cm / 100.0));

            double c = SpeedOfSound(P.TempC);

            // time-of-flight bolak-balik
            double tEcho = 2.0 * d_m / c;
            LastEchoTimeSec = tEcho;

            // atenuasi jarak
            double distAtt = Math.Exp(-P.Alpha * d_m);

            // faktor sudut
            double thetaRad = P.ThetaDeg * Math.PI / 180.0;
            double angleFactor = Math.Pow(Math.Cos(thetaRad), P.AngleOrder);

            // amplitudo efektif
            double Aeff = P.Amplitude * distAtt * angleFactor;

            double echoWin = Math.Max(1e-6, P.EchoWindowSec);

            for (int i = 0; i < N; i++)
            {
                double ti = t[i];

                // gating: echo hanya muncul setelah tEcho, dan selama echoWin
                double gate = (ti >= tEcho && ti <= tEcho + echoWin) ? 1.0 : 0.0;

                // echo 40 kHz
                double echo = gate * Aeff * Math.Cos(2.0 * Math.PI * P.F0 * (ti - tEcho));

                // noise
                double noise = Gaussian(0.0, P.NoiseStd);

                s[i] = echo + noise;
            }

            return s;
        }

        private double Gaussian(double mu, double sigma)
        {
            // Box-Muller
            double u1 = 1.0 - rnd.NextDouble();
            double u2 = 1.0 - rnd.NextDouble();
            double r = Math.Sqrt(-2.0 * Math.Log(u1));
            double th = 2.0 * Math.PI * u2;
            return mu + sigma * r * Math.Cos(th);
        }
    }
}
