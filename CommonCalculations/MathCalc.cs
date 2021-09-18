using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonCalculations
{
    public static class MathCalc
    {
        public static double Percentile(double[] sequence, double excelPercentile)
        {
            Array.Sort(sequence);
            int N = sequence.Length;
            double n = (N - 1) * excelPercentile + 1;
            // Another method: double n = (N + 1) * excelPercentile;
            if (n == 1d) return sequence[0];
            else if (n == N) return sequence[N - 1];
            else
            {
                int k = (int)n;
                double d = n - k;
                return sequence[k - 1] + d * (sequence[k] - sequence[k - 1]);
            }
        }

        public record bucket(double Bucket, int Count);
        public static List<bucket> BucketizeDouble(double[] values, double[] ceilings)
        {
            return values.GroupBy(x => ceilings.First(r => r >= x)).Select(g => new bucket(g.Key, g.Count())).ToList();
        }
    }
}
