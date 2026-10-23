using System;

namespace CircuitMatrices.Services
{
    /// <summary>Small dense-matrix helpers (jagged arrays, row-major).</summary>
    public static class Mat
    {
        public static double[][] Create(int rows, int cols)
        {
            var m = new double[rows][];
            for (int i = 0; i < rows; i++) m[i] = new double[cols];
            return m;
        }

        public static double[][] Copy(double[][] a)
        {
            var m = new double[a.Length][];
            for (int i = 0; i < a.Length; i++) m[i] = (double[])a[i].Clone();
            return m;
        }

        public static double[][] Diag(double[] d)
        {
            var m = Create(d.Length, d.Length);
            for (int i = 0; i < d.Length; i++) m[i][i] = d[i];
            return m;
        }

        public static double[][] Transpose(double[][] a, int cols)
        {
            int rows = a.Length;
            var t = Create(cols, rows);
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    t[j][i] = a[i][j];
            return t;
        }

        /// <summary>a (r x k) times b (k x c). Both must be non-empty.</summary>
        public static double[][] Mul(double[][] a, double[][] b)
        {
            int r = a.Length;
            int k = b.Length;
            int c = b[0].Length;
            var m = Create(r, c);
            for (int i = 0; i < r; i++)
                for (int p = 0; p < k; p++)
                {
                    double v = a[i][p];
                    if (v == 0) continue;
                    for (int j = 0; j < c; j++)
                        m[i][j] += v * b[p][j];
                }
            return m;
        }

        public static double[] MulVec(double[][] a, double[] v)
        {
            var r = new double[a.Length];
            for (int i = 0; i < a.Length; i++)
            {
                double s = 0;
                for (int j = 0; j < v.Length; j++) s += a[i][j] * v[j];
                r[i] = s;
            }
            return r;
        }

        /// <summary>Transpose(a) times v, where a is r x cols and v has r entries.</summary>
        public static double[] MulTransposeVec(double[][] a, int cols, double[] v)
        {
            var r = new double[cols];
            for (int j = 0; j < cols; j++)
            {
                double s = 0;
                for (int i = 0; i < a.Length; i++) s += a[i][j] * v[i];
                r[j] = s;
            }
            return r;
        }

        public static double MaxAbs(double[] v)
        {
            double m = 0;
            foreach (double x in v) m = Math.Max(m, Math.Abs(x));
            return m;
        }

        /// <summary>Solves a x = rhs (Gauss-Jordan, partial pivoting). Returns null if singular.</summary>
        public static double[] Solve(double[][] a, double[] rhs)
        {
            int n = rhs.Length;
            var m = Create(n, n + 1);
            double scale = 0;
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    m[i][j] = a[i][j];
                    scale = Math.Max(scale, Math.Abs(a[i][j]));
                }
                m[i][n] = rhs[i];
            }

            for (int col = 0; col < n; col++)
            {
                int piv = col;
                for (int r = col + 1; r < n; r++)
                    if (Math.Abs(m[r][col]) > Math.Abs(m[piv][col])) piv = r;

                if (Math.Abs(m[piv][col]) <= 1e-12 * Math.Max(1.0, scale)) return null;

                double[] tmp = m[col]; m[col] = m[piv]; m[piv] = tmp;

                for (int r = 0; r < n; r++)
                {
                    if (r == col) continue;
                    double f = m[r][col] / m[col][col];
                    if (f == 0) continue;
                    for (int c = col; c <= n; c++) m[r][c] -= f * m[col][c];
                }
            }

            var x = new double[n];
            for (int i = 0; i < n; i++) x[i] = m[i][n] / m[i][i];
            return x;
        }

        /// <summary>Determinant by Gaussian elimination with partial pivoting.</summary>
        public static double Det(double[][] a)
        {
            int n = a.Length;
            var m = Copy(a);
            double det = 1;
            for (int col = 0; col < n; col++)
            {
                int piv = col;
                for (int r = col + 1; r < n; r++)
                    if (Math.Abs(m[r][col]) > Math.Abs(m[piv][col])) piv = r;
                if (Math.Abs(m[piv][col]) < 1e-12) return 0;
                if (piv != col)
                {
                    double[] tmp = m[col]; m[col] = m[piv]; m[piv] = tmp;
                    det = -det;
                }
                det *= m[col][col];
                for (int r = col + 1; r < n; r++)
                {
                    double f = m[r][col] / m[col][col];
                    for (int c = col; c < n; c++) m[r][c] -= f * m[col][c];
                }
            }
            return det;
        }
    }
}
