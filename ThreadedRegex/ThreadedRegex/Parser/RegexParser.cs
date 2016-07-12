using System;
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
        private readonly StringInfo _regexInfo;
        private readonly int[] _indexs;
        private int _pos;
        internal RegexParser(string regex)
        {
            _regex = regex;
            _regexInfo = new StringInfo(_regex);
            _indexs = StringInfo.ParseCombiningCharacters(regex);
        }

        private string Next()
        {
            var c = Look;
            _pos++;
            return c;
        }

        private string LookAhead(int length)
        {
            return _regexInfo.SubstringByTextElements(_pos, length);
        }

        private string Look => StringInfo.GetNextTextElement(_regex, _indexs[_pos]);

        internal AST Parse()
        {
            var alternation = new AlternationBuilder();
            alternation.Add(Expr()); // TODO
            return null;
        }

        private AST Expr()
        {
            var concat = new ConcatBuilder();
            while (Look != "|")
            {
                var atom = Atom();
                if (atom == null)
                {
                    break;
                }
                var quantifier = Quantifier(atom);
                concat.Add(quantifier);
            }
            return concat.Build();
        }

        private AST Quantifier(AST atom)
        {
            var c = Look;
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
            _pos++;
            c = Look;
            switch (c)
            {
                case "?":
                    quant.laziness = Quantified.Laziness.Lazy;
                    _pos++;
                    break;
                case "+":
                    quant.laziness = Quantified.Laziness.Possesive;
                    _pos++;
                    break;
                default:
                    quant.laziness = Quantified.Laziness.Greedy;
                    break;
            }
            return quant;
        }

        private AST Atom()
        {
            if (Look == ")")
            {
                return null;
            }
            var c = Next();
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
            var c = Look;
            if (c != "?")
            {
                return new Group(GroupContent());
            }
            _pos++;
            c = Next();
            switch (c)
            {
                case "-":
                case "i":
                case "s":
                case "m":
                case "x":
                case "n":
                case "d":
                    _pos--;
                    var mods = ParseModifiers();
                    _pos--;
                    if (Next() == ")")
                    {
                        return mods;
                    }
                    return new Group(GroupContent(), mods);
                case "=":
                    return new LookAhead(GroupContent(), false);
                case "!":
                    return new LookAhead(GroupContent(), true);
                case "(":
                    if (LookAhead(7) == "DEFINE)")
                    {
                        _pos += 7;
                        var concatBuilder = new ConcatBuilder();
                        while (c != ")")
                        {
                            c = Next();
                            if (c != "(")
                            {
                                return null;
                            }
                            concatBuilder.Add(NamedGroup());
                            c = Look;
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
                        c = Next();
                    }
                    return null;
                case "C":
                    // TODO: callouts
                    break;
                default:
                    var named = NamedGroup();
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
            var c = Next();
            if (c == "P")
            {
                c = Next();
            }
            if (c != "<") return null;
            c = Next();
            if (c == "=" || c == "!") return new LookBehind(GroupContent(), c == "!");
            _pos--;
            var name = Name(">");
            _pos--;
            if (name == null || Next() != ">")
            {
                return null;
            }
            return new Group(GroupContent(), name: name);
        }

        private AST GroupContent()
        {
            var inner = Parse();
            return Next() == ")" ? new Group(inner) : null;
        }

        private Modifiers ParseModifiers()
        {
            var c = Next();
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
                    case "x": // TODO: enable freespacing
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
                c = Next();
            }
            return mods;
        }

        private CharacterClass CharClass()
        {
            // TODO: handle inbuilt character classes (e.g. \d, \p{IsArabic}, ...) and other escaped characters
            var classBuilder = new CharacterClassBuilder();
            var c = Next();
            if (c == "^")
            {
                classBuilder.Negate = true;
                c = Next();
            }

            // Special case at beggining of class
            if (c == "-")
            {
                if (Look == "-")
                {
                    _pos++;
                    classBuilder.AddRange(c, Next());
                }
                else
                {
                    classBuilder.Add(c); // Includes ']'
                }
            }
            else if (c == "]")
            {
                classBuilder.Add(c);
            }

            while (c != "]")
            {
                if (Look == "-")
                {
                    _pos++;
                    if (Look == "[")
                    {
                        _pos++;
                        var innerClass = CharClass();
                        classBuilder.Subtract(innerClass);
                        if (Look != "]")
                        {
                            return null;
                        }
                    }
                    else
                    {
                        var second = Next();
                        classBuilder.AddRange(c, second);
                    }
                }
                else if (LookAhead(2) == "&&")
                {
                    _pos += 2;
                    var innerClass = CharClass();
                    classBuilder.Intersect(innerClass);
                    if (Look != "]")
                    {
                        return null;
                    }
                }
                else
                {
                    classBuilder.Add(c);
                    c = Next();
                }
            }
            return classBuilder.Build();
        }

        private AST Escape()
        {
            var c = Next();
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
                    if (Next() != "<")
                    {
                        return null;
                    }
                    node = BackRef(true, ">");
                    _pos--;
                    return Next() == ">" ? node : null;
                case "g":
                    if (Next() != "{")
                    {
                        return null;
                    }
                    node = BackRef(false, "}");
                    _pos--;
                    return Next() == "}" ? node : null;
                case "Q":
                    var end = LookAhead(2);
                    var builder = new ConcatBuilder();
                    while (end != @"\E")
                    {
                        if (end == null)
                        {
                            return null;
                        }
                        builder.Add(new StrNode(Next()));
                        end = LookAhead(2);
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
                    c = Next();
                    return c.All(character => character <= 127) ? new Control(c) : null; // Is ascii
                case "e":
                    return new EscapeChar();
                case "o":
                    if (Next() != "{")
                    {
                        return null;
                    }
                    c = Num(8);
                    return Next() == "}" ? new StrNode(c) : null;
                case "x":
                    if (Look == "{")
                    {
                        Next();
                        c = Num(16);
                        return Next() == "}" ? new StrNode(c) : null;
                    }
                    c = Num(16, 2);
                    return new StrNode(c);
                case "p":
                    if (Next() != "{")
                    {
                        return null;
                    }
                    var strBuilder = new StringBuilder();
                    c = Next();
                    while (c != "}")
                    {
                        strBuilder.Append(c);
                        c = Next();
                    }
                    return new Category(strBuilder.ToString());
                default:
                    if (!char.IsDigit(c, 0)) return new StrNode(c);

                    _pos--;
                    if (c != "0") return BackRef(false); // Determine at match time if octal character

                    // Definetely octal, by specicification
                    c = Num(8, 3);
                    return new StrNode(c);
            }
        }

        private string Num(int numBase)
        {
            var builder = new StringBuilder();
            var c = Look;
            while (char.IsDigit(c, 0))
            {
                _pos++;
                builder.Append(c);
                c = Look;
            }
            return Convert.ToInt32(builder.ToString(), numBase).ToString();
        }

        private string Num(int numBase, int maxLength)
        {
            var builder = new StringBuilder();
            var c = Look;
            while (char.IsDigit(c, 0) && builder.Length < maxLength)
            {
                _pos++;
                builder.Append(c);
                c = Look;
            }
            return Convert.ToInt32(builder.ToString(), numBase).ToString();
        }

        private string Name(string end)
        {
            var builder = new StringBuilder();
            var c = Next();
            if (!c.IsUnicodeIdentifierStart())
            {
                return null;
            }
            builder.Append(c);
            c = Next();
            while (c.IsUnicodeIdentifierPart() && c != end)
            {
                builder.Append(c);
                c = Next();
            }
            return builder.ToString();
        }

        private AST BackRef(bool named, string end = null)
        {
            var start = _pos;
            if (named)
            {
                var name = Name(end);
                if (name != null)
                {
                    return new BackReference(name);
                }
            }
            _pos = start;
            var c = Next();
            var builder = new StringBuilder();
            if (!char.IsDigit(c, 0)) return null;
            
            while (char.IsDigit(c, 0) && c != end)
            {
                builder.Append(c);
                c = Next();
            }
            var num = int.Parse(builder.ToString());
            return new BackReference(num);
        }
    }
}