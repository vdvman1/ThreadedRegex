using System;
using System.Collections.Generic;
using System.Text;

namespace ThreadedRegex.Parser
{
    internal class ParseException : Exception
    {
        private readonly IEnumerable<string> expected;

        public ParseException(IEnumerable<string> expected)
        {
            this.expected = expected;
        }

        public ParseException(string message) : base(message) {}

        public string ParseMessage(string prefix, string line, int position)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Got {prefix}, expected [{string.Join(",", expected)}]");
            builder.AppendLine(line);
            builder.AppendLine("^".PadLeft(position));
            return builder.ToString();
        }
    }
}