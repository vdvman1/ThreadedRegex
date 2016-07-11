using System;
using System.Collections.Generic;
using System.Text;

namespace ThreadedRegex
{
    public class CharacterRange
    {
        private struct Range
        {
            public Range(int low, int high)
            {
                Low = low;
                High = high;
            }

            public int Low { get; }
            public int High { get; }

            public override string ToString()
            {
                if (Low == High)
                {
                    return char.ConvertFromUtf32(Low);
                }
                return char.ConvertFromUtf32(Low) + "-" + char.ConvertFromUtf32(High);
            }
        }

        private readonly List<Range> _ranges = new List<Range>();

        private int Find(int val)
        {
            var lower = 0;
            var upper = _ranges.Count - 1;
            while (lower <= upper)
            {
                var middle = lower + (upper - lower)/2;
                var range = _ranges[middle];
                if (val < range.Low)
                {
                    upper = middle - 1;
                }
                else if (range.Low <= val && val <= range.High)
                {
                    return middle;
                }
                else
                {
                    lower = middle + 1;
                }
            }

            return ~lower;
        }

        public void Add(string c)
        {
            var val = char.ConvertToUtf32(c, 0);
            if (_ranges.Count == 0)
            {
                _ranges.Add(new Range(val, val));
                return;
            }
            var index = Find(val);
            if (index >= 0)
            {
                // Already exists
                return;
            }
            index = ~index;
            Range rangeAbove;
            Range rangeBelow;
            if (index == _ranges.Count)
            {
                rangeAbove = new Range(int.MaxValue, int.MaxValue);
                rangeBelow = _ranges[index - 1];
            }
            else if (index == 0)
            {
                rangeAbove = _ranges[index];
                rangeBelow = new Range(int.MinValue, int.MinValue);
            }
            else
            {
                rangeAbove = _ranges[index];
                rangeBelow = _ranges[index - 1];
            }
            if (val == rangeBelow.High + 1)
            {
                if (val == rangeAbove.Low - 1)
                {
                    _ranges[index - 1] = new Range(rangeBelow.Low, rangeAbove.High);
                    _ranges.RemoveAt(index);
                    return;
                }
                _ranges[index - 1] = new Range(rangeBelow.Low, val);
                return;
            }
            if (val == rangeAbove.Low - 1)
            {
                _ranges[index] = new Range(val, rangeAbove.High);
                return;
            }
            _ranges.Insert(index, new Range(val, val));
        }

        public void AddRange(string a, string b)
        {
            var valA = char.ConvertToUtf32(a, 0);
            var valB = char.ConvertToUtf32(b, 0);
            if (_ranges.Count == 0)
            {
                _ranges.Add(new Range(valA, valB));
            }
            var indexA = Find(valA);
            var indexB = Find(valB);
            if (indexA == indexB && indexA >= 0)
            {
                // Range already exists
                return;
            }
            if (indexA < 0)
            {
                indexA = ~indexA;
            }
            if (indexB < 0)
            {
                indexB = ~indexB;
            }

            var rangeBelowA = indexA == 0 ? new Range(int.MinValue, int.MinValue) : _ranges[indexA - 1];
            var rangeAboveB = indexB == _ranges.Count ? new Range(int.MaxValue, int.MaxValue) : _ranges[indexB];

            int low, high, insertIndex, startIndex, endIndex;
            if (valA <= rangeBelowA.High + 1)
            {
                low = rangeBelowA.Low;
                insertIndex = indexA - 1;
                startIndex = indexA;
            }
            else
            {
                low = valA;
                insertIndex = indexA;
                startIndex = indexA + 1;
            }
            if (valB >= rangeAboveB.Low - 1)
            {
                high = rangeAboveB.High;
                endIndex = indexB;
            }
            else
            {
                high = valB;
                endIndex = indexB - 1;
            }
            endIndex = Math.Min(Math.Max(endIndex, 0), _ranges.Count - 1);
            _ranges[insertIndex] = new Range(low, high);
            _ranges.RemoveRange(startIndex, endIndex - startIndex + 1);
        }

        public void Remove(string c)
        {
            var val = char.ConvertToUtf32(c, 0);
            if (_ranges.Count == 0)
            {
                return;
            }
            var index = Find(val);
            if (index < 0)
            {
                // Doesn't exist
                return;
            }
            if (index == _ranges.Count) // TODO: Is this posible?
            {
                return;
            }
            var range = _ranges[index];
            if (val == range.High)
            {
                _ranges[index] = new Range(range.Low, val - 1);
                return;
            }
            if (val == range.Low)
            {
                _ranges[index] = new Range(val + 1, range.High);
                return;
            }
            _ranges[index] = new Range(range.Low, val - 1);
            _ranges.Insert(index + 1, new Range(val + 1, range.High));
        }

        public void RemoveRange(string a, string b)
        {
            var valA = char.ConvertToUtf32(a, 0);
            var valB = char.ConvertToUtf32(b, 0);
            var indexA = Find(valA);
            var indexB = Find(valB);
            if (indexA == indexB && indexA < 0)
            {
                // Range doesn't exist
                return;
            }
            if (indexA < 0)
            {
                indexA = ~indexA;
            }
            if (indexB < 0)
            {
                indexB = ~indexB;
            }
            Range rangeAboveA;
            Range rangeBelowA;
            Range rangeAboveB;
            Range rangeBelowB;
            if (indexA == _ranges.Count)
            {
                rangeAboveA = new Range(int.MaxValue, int.MaxValue);
                rangeBelowA = _ranges[indexA - 1];
            }
            else if (indexA == 0)
            {
                rangeAboveA = _ranges[indexA];
                rangeBelowA = new Range(int.MinValue, int.MinValue);
            }
            else
            {
                rangeAboveA = _ranges[indexA];
                rangeBelowA = _ranges[indexA];
            }
            if (indexB == _ranges.Count)
            {
                rangeAboveB = new Range(int.MaxValue, int.MaxValue);
                rangeBelowB = _ranges[indexB - 1];
            }
            else if (indexB == 0)
            {
                rangeAboveB = _ranges[indexB];
                rangeBelowB = new Range(int.MinValue, int.MinValue);
            }
            else
            {
                rangeAboveB = _ranges[indexB];
                rangeBelowB = _ranges[indexB - 1];
            }

            if (indexA == indexB)
            {
                var range = _ranges[indexA];
                if (valA == range.Low && valB == range.High)
                {
                    _ranges.RemoveAt(indexA);
                    return;
                }
                if (valA > range.Low)
                {
                    _ranges[indexA] = new Range(range.Low, valA - 1);
                    if (valB < range.High)
                    {
                        _ranges.Insert(indexA + 1, new Range(valB + 1, range.High));
                    }
                    return;
                }
                if (valB < range.High)
                {
                    _ranges[indexA] = new Range(valB + 1, range.High);
                    return;
                }
            }

            int start;
            if (valA > rangeAboveA.Low)
            {
                _ranges[indexA] = new Range(rangeAboveA.Low, valA - 1);
                start = indexA + 1;
            }
            else
            {
                start = indexA;
            }
            if (valB >= rangeAboveB.Low)
            {
                _ranges[indexB] = new Range(valB + 1, rangeAboveB.High);
            }
            var end = Math.Min(Math.Max(indexB - 1, 0), _ranges.Count - 1);
            _ranges.RemoveRange(start, end - start + 1);
        }

        public bool IsIncluded(string c)
        {
            return Find(char.ConvertToUtf32(c, 0)) >= 0;
        }

        public override string ToString()
        {
            return "[" + string.Join("", _ranges) + "]";
        }
    }
}