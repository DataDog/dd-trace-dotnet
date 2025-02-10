namespace BenchmarkFramework.Util;

public static class Statistics
{
    // public static (List<TSource> Kept, List<TSource> Removed) FindOutliersBy<TSource>(List<TSource> data, Func<TSource, double> selector)
    // {
    //     var outliers = FindOutliersInternal(data.Select(selector).ToArray());
    //     var kept = new List<TSource>();
    //     var removed = new List<TSource>();
    //
    //     for (var i = 0; i < data.Count; i++)
    //     {
    //         if (outliers[i])
    //         {
    //             kept.Add(data[i]);
    //         }
    //         else
    //         {
    //             removed.Add(data[i]);
    //         }
    //     }
    //
    //     return (kept, removed);
    // }

    public static (List<double> Kept, List<double> Removed) FindOutliers(double[] data)
    {
        var outliers = FindOutliersInternal(data);
        var kept = new List<double>();
        var removed = new List<double>();

        for (var i = 0; i < data.Length; i++)
        {
            if (outliers[i])
            {
                kept.Add(data[i]);
            }
            else
            {
                removed.Add(data[i]);
            }
        }

        return (kept, removed);
    }

    private static bool[] FindOutliersInternal(double[] data)
    {
        var dataLength = data.Length;

        // calculate the median
        var median = GetMedian(data);

        // calculate the median absolute deviation (MAD)
        var buffer = new double[dataLength];

        for (var i = 0; i < dataLength; i++)
        {
            buffer[i] = Math.Abs(data[i] - median);
        }

        var mad = GetMedian(buffer);

        // compute M modified z-scores (re-use the same buffer)
        for (var i = 0; i < dataLength; i++)
        {
            buffer[i] = 0.6745 * (data[i] - median) / mad;
        }

        // identify and filter out outliers (|Modified Z| > 3.5)
        var indexesToKeep = new bool[dataLength];

        for (var i = 0; i < dataLength; i++)
        {
            indexesToKeep[i] = Math.Abs(buffer[i]) <= 3.5;
        }

        return indexesToKeep;
    }

    public static double GetMedian(double[] data)
    {
        // make a copy of the data and sort it
        var sortedData = new double[data.Length];
        data.CopyTo(sortedData, 0);
        Array.Sort(sortedData);

        var n = sortedData.Length;

        if (n % 2 == 0)
        {
            // even number of elements, average the two middle ones
            return (sortedData[n / 2 - 1] + sortedData[n / 2]) / 2.0;
        }

        // ddd number of elements, take the middle one
        return sortedData[n / 2];
    }
}
