using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ThreadedRegex.Parser
{
    internal class RegexParser
    {
        // TODO: Error reporting
        // TODO: Group numbering

        private readonly string _regex;
        private readonly int[] _indexs;
        private int _pos;
        private readonly Stack<bool> _freeSpacing = new Stack<bool>();
        internal RegexParser(string regex, bool freeSpacing)
        {
            _regex = regex;
            _indexs = StringInfo.ParseCombiningCharacters(regex);
            _freeSpacing.Push(freeSpacing);
        }

        private string Next(bool allowFreeSpace, int count = 1)
        {
            var result = new StringBuilder();
            for (var i = 0; i < count; i++)
            {
                string c;
                bool isWhite;
                do
                {
                    c = StringInfo.GetNextTextElement(_regex, _indexs[_pos]);
                    if (allowFreeSpace && _freeSpacing.Peek() && char.IsWhiteSpace(c, 0))
                    {
                        isWhite = true;
                        _pos++;
                    }
                    else
                    {
                        isWhite = false;
                    }
                } while (isWhite);
                result.Append(c);
                _pos++;
            }
            return result.ToString();
        }

        private void MovePrev(bool allowFreeSpace)
        {
            string c;
            do
            {
                _pos--;
                c = Look(false);
            } while (allowFreeSpace && _freeSpacing.Peek() && char.IsWhiteSpace(c, 0));
        }

        private string Look(bool allowFreeSpace, int count = 1)
        {
            int start = _pos;
            string str = Next(allowFreeSpace, count);
            _pos = start;
            return str;
        }

        internal AST Parse()
        {
            var alternation = new AlternationBuilder();
            do
            {
                alternation.Add(Expr());
            } while (Next(true) == "|");
            MovePrev(true);
            return null;
        }

        private AST Expr()
        {
            var concat = new ConcatBuilder();
            string c = Look(true);
            while (c != "|" && c != ")")
            {
                AST atom = Atom();
                if (atom == null)
                {
                    break;
                }
                AST quantifier = Quantifier(atom);
                concat.Add(quantifier);
                c = Look(true);
            }
            return concat.Build();
        }

        private AST Quantifier(AST atom)
        {
            string c = Look(false);
            Quantified quant;
            switch (c)
            {
                case "?":
                    quant = new ZeroOrOne(atom);
                    break;
                case "*":
                    quant = new ZeroOrMore(atom);
                    break;
                case "+":
                    quant = new OneOrMore(atom);
                    break;
                default:
                    return atom;
            }
            Next(false);
            c = Next(false);
            switch (c)
            {
                case "?":
                    quant.laziness = Quantified.Laziness.Lazy;
                    break;
                case "+":
                    quant.laziness = Quantified.Laziness.Possesive;
                    break;
                default:
                    quant.laziness = Quantified.Laziness.Greedy;
                    MovePrev(false);
                    break;
            }
            return quant;
        }

        private AST Atom()
        {
            if (Look(true) == ")")
            {
                return null;
            }
            string c = Next(true);
            switch (c)
            {
                case @"\":
                    return Escape();
                case "^":
                    return new StartStringOrLine();
                case "$":
                    return new EndStringOrLine();
                case "[":
                    return CharClass();
                case "(":
                    return Group();
                default:
                    return new StrNode(c);
            }
        }

        private AST Group()
        {
            string c = Look(false);
            if (c != "?")
            {
                return new Group(GroupContent());
            }
            Next(false);
            c = Next(true);
            switch (c)
            {
                case "-":
                case "i":
                case "s":
                case "m":
                case "x":
                case "n":
                case "d":
                    MovePrev(true);
                    bool hasX;
                    bool enableFreeSpacing;
                    Modifiers mods = ParseModifiers(out enableFreeSpacing, out hasX);
                    MovePrev(true);
                    if (Next(true) == ")")
                    {
                        if (hasX)
                        {
                            _freeSpacing.Pop();
                            _freeSpacing.Push(enableFreeSpacing);
                        }
                        return mods;
                    }
                    _freeSpacing.Push(enableFreeSpacing);
                    return new Group(GroupContent(hasX), mods);
                case "=":
                    return new LookAhead(GroupContent(), false);
                case "!":
                    return new LookAhead(GroupContent(), true);
                case "(":
                    const string define = "DEFINE)";
                    if (Look(false, define.Length) == define)
                    {
                        _pos += define.Length;
                        var concatBuilder = new ConcatBuilder();
                        while (c != ")")
                        {
                            c = Next(true);
                            if (c != "(")
                            {
                                return null;
                            }
                            concatBuilder.Add(NamedGroup());
                            c = Look(true);
                        }
                        return concatBuilder.Build();
                    }
                    // TODO: Conditional
                    break;
                case "|":
                    return GroupContent(); // TODO: actually reset group numbers
                case "#":
                    while (c != ")")
                    {
                        c = Next(false); // Save extra looping and checking
                    }
                    return null;
                case "C":
                    // TODO: callouts
                    break;
                default:
                    AST named = NamedGroup();
                    if (named != null)
                    {
                        return named;
                    }
                    // TODO: Subroutines
                    break;
            }
            return null;
        }

        private AST NamedGroup()
        {
            string c = Next(false);
            var isDefinetelyNamed = false;
            if (c == "P")
            {
                isDefinetelyNamed = true;
                c = Next(false);
            }
            if (c != "<") return null;
            c = Next(false);
            if (!isDefinetelyNamed && (c == "=" || c == "!")) return new LookBehind(GroupContent(), c == "!");
            MovePrev(false);
            string name = Name(">");
            MovePrev(false);
            if (name == null || Next(false) != ">")
            {
                return null;
            }
            return new Group(GroupContent(), name: name);
        }

        private AST GroupContent(bool hasX = false)
        {
            AST inner = Parse();
            if (hasX)
            {
                _freeSpacing.Pop();
            }
            return Next(true) == ")" ? inner : null;
        }

        private Modifiers ParseModifiers(out bool enableFreeSpacing, out bool hasX)
        {
            enableFreeSpacing = false;
            hasX = false;
            string c = Next(true);
            var mods = new Modifiers();
            var negated = false;
            while (c != ")" && c != ":")
            {
                switch (c)
                {
                    case "-":
                        if (negated)
                        {
                            return null;
                        }
                        negated = true;
                        break;
                    case "i":
                        mods.Add(Modifier.ModifierType.I, negated);
                        negated = false;
                        break;
                    case "s":
                        mods.Add(Modifier.ModifierType.S, negated);
                        negated = false;
                        break;
                    case "m":
                        mods.Add(Modifier.ModifierType.M, negated);
                        negated = false;
                        break;
                    case "x":
                        hasX = true;
                        enableFreeSpacing = !negated;
                        mods.Add(Modifier.ModifierType.X, negated);
                        negated = false;
                        break;
                    case "n":
                        mods.Add(Modifier.ModifierType.N, negated);
                        negated = false;
                        break;
                    case "d":
                        mods.Add(Modifier.ModifierType.D, negated);
                        negated = false;
                        break;
                    default:
                        return null;
                }
                c = Next(!negated);
            }
            return mods;
        }

        private CharacterClass CharClass()
        {
            // TODO: handle inbuilt character classes (e.g. \d, \p{IsArabic}, ...) and other escaped characters
            var classBuilder = new CharacterClassBuilder();
            string c = Next(false);
            if (c == "^")
            {
                classBuilder.Negate = true;
                c = Next(false);
            }

            // Special case at beggining of class
            if (c == "-")
            {
                if (Look(false) == "-")
                {
                    Next(false);
                    classBuilder.AddRange(c, Next(false));
                }
                else
                {
                    classBuilder.Add(c);
                }
            }
            else if (c == "]")
            {
                classBuilder.Add(c);
                if (Look(false) == "-")
                {
                    Next(false);
                    classBuilder.AddRange(c, Next(false));
                }
            }

            while (c != "]")
            {
                const string and = "&&";
                if (Look(false) == "-")
                {
                    Next(false);
                    if (Look(false) == "[")
                    {
                        Next(false);
                        CharacterClass innerClass = CharClass();
                        classBuilder.Subtract(innerClass);
                        if (Look(false) != "]")
                        {
                            return null;
                        }
                    }
                    else
                    {
                        classBuilder.AddRange(c, Next(false));
                    }
                }
                else if (Look(false, and.Length) == and)
                {
                    _pos += and.Length;
                    CharacterClass innerClass = CharClass();
                    classBuilder.Intersect(innerClass);
                    if (Look(false) != "]")
                    {
                        return null;
                    }
                }
                else
                {
                    classBuilder.Add(c);
                    c = Next(false);
                }
            }
            return classBuilder.Build();
        }

        private AST Escape()
        {
            string c = Next(false);
            AST node;
            switch (c)
            {
                case "A":
                    return new BeginString();
                case "z":
                    return new EndString(false);
                case "Z":
                    return new EndString(true);
                case "G":
                    return new BeginStringOrMatch();
                case "b":
                    return new WordBoundry();
                case "B":
                    return new Not(new WordBoundry());
                case "k":
                    if (Next(false) != "<")
                    {
                        return null;
                    }
                    node = BackRef(true, ">");
                    MovePrev(false);
                    return Next(false) == ">" ? node : null;
                case "g":
                    if (Next(false) != "{")
                    {
                        return null;
                    }
                    node = BackRef(false, "}");
                    MovePrev(false);
                    return Next(false) == "}" ? node : null;
                case "Q":
                    const string ending = @"\E";
                    string end = Look(false, ending.Length);
                    var builder = new ConcatBuilder();
                    while (end != ending)
                    {
                        if (end == null)
                        {
                            return null;
                        }
                        builder.Add(new StrNode(Next(false)));
                        end = Look(false, ending.Length);
                    }
                    return builder.Build();
                case "d":
                    return new Digit();
                case "D":
                    return new Not(new Digit());
                case "w":
                    return new Word();
                case "W":
                    return new Not(new Word());
                case "s":
                    return new Space();
                case "S":
                    return new Not(new Space());
                case "t":
                    return new Tab();
                case "r":
                    return new CarriageReturn();
                case "R":
                    return new LineBreak();
                case "n":
                    return new LineFeed();
                case "N":
                    return new Not(new LineFeed());
                case "h":
                    return new HorizontalSpace();
                case "H":
                    return new Not(new HorizontalSpace());
                case "v":
                    return new VerticalSpace();
                case "V":
                    return new Not(new VerticalSpace());
                case "K":
                    return new ClearMatch();
                case "a":
                    return new AlarmChar();
                case "c":
                    c = Next(false);
                    return c.All(character => character <= 127) ? new Control(c) : null; // Is ascii
                case "e":
                    return new EscapeChar();
                case "o":
                    if (Next(false) != "{")
                    {
                        return null;
                    }
                    c = Num(8);
                    return Next(false) == "}" ? new StrNode(c) : null;
                case "x":
                    if (Look(false) == "{")
                    {
                        Next(false);
                        c = Num(16);
                        return Next(false) == "}" ? new StrNode(c) : null;
                    }
                    c = Num(16, 2);
                    return new StrNode(c);
                case "p":
                    if (Next(false) != "{")
                    {
                        return null;
                    }
                    var strBuilder = new StringBuilder();
                    c = Next(false);
                    while (c != "}")
                    {
                        strBuilder.Append(c);
                        c = Next(false);
                    }
                    return new Category(strBuilder.ToString());
                default:
                    if (!char.IsDigit(c, 0)) return new StrNode(c);

                    MovePrev(false);
                    if (c != "0") return BackRef(false); // Determine at match time if octal character

                    // Definetely octal, by specicification
                    c = Num(8, 3);
                    return new StrNode(c);
            }
        }

        private string Num(int numBase)
        {
            var builder = new StringBuilder();
            string c = Look(false);
            while (char.IsDigit(c, 0))
            {
                Next(false);
                builder.Append(c);
                c = Look(false);
            }
            return Convert.ToInt32(builder.ToString(), numBase).ToString();
        }

        private string Num(int numBase, int maxLength)
        {
            var builder = new StringBuilder();
            string c = Look(false);
            while (char.IsDigit(c, 0) && builder.Length < maxLength)
            {
                Next(false);
                builder.Append(c);
                c = Look(false);
            }
            return Convert.ToInt32(builder.ToString(), numBase).ToString();
        }

        private string Name(string end)
        {
            var builder = new StringBuilder();
            string c = Next(false);
            if (!c.IsUnicodeIdentifierStart())
            {
                return null;
            }
            builder.Append(c);
            c = Next(false);
            while (c.IsUnicodeIdentifierPart() && c != end)
            {
                builder.Append(c);
                c = Next(false);
            }
            return builder.ToString();
        }

        private AST BackRef(bool named, string end = null)
        {
            int start = _pos;
            if (named)
            {
                string name = Name(end);
                if (name != null)
                {
                    return new BackReference(name);
                }
            }
            _pos = start;
            string c = Next(false);
            var builder = new StringBuilder();
            if (!char.IsDigit(c, 0)) return null;
            
            while (char.IsDigit(c, 0) && c != end)
            {
                builder.Append(c);
                c = Next(false);
            }
            int num = int.Parse(builder.ToString());
            return new BackReference(num);
        }
    }
}