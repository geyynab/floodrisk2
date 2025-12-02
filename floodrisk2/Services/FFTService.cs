using System;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics;

namespace floodrisk2.Services
{
    public static class FFTService
    {
        public static (float[] freqs, float[] mags) ComputeFFT(double[] signal, double sampleRate)
        {
            int N = signal.Length;

            // Convert ke Complex32
            Complex32[] buffer = new Complex32[N];
            for (int i = 0; i < N; i++)
                buffer[i] = new Complex32((float)signal[i], 0f);

            // FFT tanpa scaling
            Fourier.Forward(buffer, FourierOptions.NoScaling);

            int half = N / 2; // single-sided spectrum
            float[] freqs = new float[half];
            float[] mags = new float[half];

            for (int k = 0; k < half; k++)
            {
                // frekuensi
                freqs[k] = (float)(k * sampleRate / N);

                // magnitude single-sided → scale by (2/N)
                float mag = buffer[k].Magnitude * (2f / N);

                // khusus DC dan Nyquist (k=0), jangan dikali 2
                if (k == 0) mag = mag / 2f;

                mags[k] = mag;
            }

            return (freqs, mags);
        }
    }
}
