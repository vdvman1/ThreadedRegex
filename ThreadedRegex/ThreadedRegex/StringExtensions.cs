using System.Globalization;

namespace ThreadedRegex
{
    public static class StringExtensions
    {
        public static bool IsUnicodeIdentifierStart(this string str)
        {
            return char.IsLetter(str, 0) || char.GetUnicodeCategory(str, 0) == UnicodeCategory.LetterNumber;
        }

        public static bool IsUnicodeIdentifierPart(this string str)
        {
            var cat = char.GetUnicodeCategory(str, 0);
            return char.IsLetterOrDigit(str, 0) || cat == UnicodeCategory.ConnectorPunctuation ||
                   cat == UnicodeCategory.LetterNumber || cat == UnicodeCategory.SpacingCombiningMark ||
                   cat == UnicodeCategory.NonSpacingMark;
        }
    }
}