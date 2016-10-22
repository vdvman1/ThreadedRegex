using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreadedRegex;
using ThreadedRegex.Utility;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var ranges = new CharacterRange();
            ranges.Add("a");
            Console.WriteLine(ranges);
            ranges.Add("c");
            Console.WriteLine(ranges);
            ranges.Add("b");
            Console.WriteLine(ranges);
            ranges.Remove("b");
            Console.WriteLine(ranges);
            ranges.AddRange("c", "j");
            Console.WriteLine(ranges);
            ranges.RemoveRange("b", "d");
            Console.WriteLine(ranges);
            ranges.RemoveRange("a", "f");
            Console.WriteLine(ranges);
            ranges.Add("h");
            Console.WriteLine(ranges);
            Console.ReadKey();
        }
    }
}
