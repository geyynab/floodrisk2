using System;
using System.Numerics;

namespace floodrisk2.Services
{
    public static class Transforms
    {
        // Numerical Laplace Transform: F(s=jω + σ)
        public static Complex[] NumericalLaplace(double[] x, double[] t, double sigma, double[] omega)
        {
            int N = x.Length;
            int M = omega.Length;
            Complex[] result = new Complex[M];

            for (int k = 0; k < M; k++)
            {
                Complex sum = 0;
                double w = omega[k];

                for (int n = 0; n < N; n++)
                {
                    double dt = (n == 0) ? 0 : (t[n] - t[n - 1]);
                    double expTerm = Math.Exp(-sigma * t[n]);
                    Complex ct = new Complex(expTerm * x[n], 0);
                    Complex ejwt = Complex.Exp(new Complex(0, -w * t[n]));
                    sum += ct * ejwt * dt;
                }

                result[k] = sum;
            }
            return result;
        }

        // Numerical Z-Transform: X(z) evaluated on unit circle z = e^{jω}
        public static Complex[] NumericalZTransform(double[] x, double[] omega)
        {
            int N = x.Length;
            int M = omega.Length;
            Complex[] result = new Complex[M];

            for (int k = 0; k < M; k++)
            {
                double w = omega[k];
                Complex sum = 0;

                for (int n = 0; n < N; n++)
                {
                    Complex e = Complex.Exp(new Complex(0, -w * n));
                    sum += x[n] * e;
                }

                result[k] = sum;
            }
            return result;
        }
    }
}
