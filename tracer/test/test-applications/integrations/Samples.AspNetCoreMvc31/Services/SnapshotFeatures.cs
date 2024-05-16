using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebService.Services
{
    public class SnapshotFeatures
    {
        public static int GuessNumber(int min, int max, int number)
        {
            var finder = new NumberFinder(min, max, number);
            return finder.Find();
        }

        public static int GuessNumberRecursive(int min, int max, int number)
        {
            var finder = new NumberFinder(min, max, number);
            return finder.FindRecursive(min, max);
        }

        public static void SearchingItems()
        {

        }
    }

    public class NumberFinder
    {
        public int Min { get; }
        public int Max { get; }
        public int Target { get; }

        public NumberFinder(int min, int max, int target)
        {
            Min = min;
            Max = max;
            Target = target;
        }

        public int Find()
        {
            var low = Min;
            var high = Max;
            var guess = (low + high) / 2;

            while (guess != Target)
            {
                if (guess > Target)
                {
                    high = guess;
                }
                else if (guess < Target)
                {
                    low = guess;
                }
                guess = (low + high) / 2;
            }

            return guess;
        }

        public int FindRecursive(int low, int high)
        {
            var guess = (low + high) / 2;


            if (guess > Target)
            {
                return FindRecursive(low, guess);
            }
            else if (guess < Target)
            {
                return FindRecursive(guess, high);
            }
            else
            {
                return guess;
            }

            throw new Exception("We should not get here");
        }
    }
}
