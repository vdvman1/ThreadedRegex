using System;
using System.Globalization;
using System.Linq;
using System.Text;
using ThreadedRegex.Utility;

namespace ThreadedRegex.Parser
{
    internal class RegexParser
    {
        private static readonly ParseTree ExprTree = new ParseTree(ParseFlag.RepeatChildren)
        {
            {
                @"\", () => EscapeTree
            },
            c => new StrNode(c)
        };

        private static readonly ParseTree EscapeTree = new ParseTree
        {
            {"A", () => new BeginString()},
            {"z", () => new EndString(false)},
            {"Z", () => new EndString(true)},
            {"G", () => new BeginStringOrMatch()},
            {"b", () => new WordBoundry()},
            {"B", () => new Not(new WordBoundry())},
            {
                "k", new ParseTree
                {
                    {"<", BackRefTree(true), ">"}
                }
            },
            {
                "g", new ParseTree
                {
                    {"{", BackRefTree(false), "}"}
                }
            },
            {
                "Q", new ParseTree(ParseFlag.RepeatChildren)
                {
                    {
                        @"\", new ParseTree
                        {
                            {"E", new MatchComplete()}
                        }
                    },
                    c => new StrNode(c)
                }
            },
            {"d", () => new Digit()},
            {"D", () => new Not(new Digit())},
            {"w", () => new Word()},
            {"W", () => new Not(new Word())},
            {"s", () => new Space()},
            {"S", () => new Not(new Space())},
            {"t", () => new Tab()},
            {"r", () => new CarriageReturn()},
            {"R", () => new LineBreak()},
            {"n", () => new LineFeed()},
            {"N", () => new Not(new LineFeed())},
            {"h", () => new HorizontalSpace()},
            {"H", () => new Not(new HorizontalSpace())},
            {"v", () => new VerticalSpace()},
            {"V", () => new Not(new VerticalSpace())},
            {"K", () => new ClearMatch()},
            {"a", () => new AlarmChar()},
            {
                "c", new ParseTree
                {
                    {c => c.All(character => character < 128), "ASCII Character", c => new Control(c)}
                }
            },
            {"e", () => new EscapeChar()},
            {
                "o", new ParseTree
                {
                    {
                        "{", new StringParseTree(ParseFlag.RepeatChildren)
                        {
                            {
                                c => char.IsDigit(c, 0), "Digit",
                                new StringParseTree(ParseFlag.Repeat,
                                    num => new StrNode(char.ConvertFromUtf32(Convert.ToInt32(num, 8))))
                            }
                        },
                        "}"
                    }
                }
            },
            {
                "x", new ParseTree
                {
                    {
                        "{", new StringParseTree(ParseFlag.RepeatChildren)
                        {
                            {
                                c => char.IsDigit(c, 0), "Digit",
                                new StringParseTree(ParseFlag.Repeat,
                                    num => new StrNode(char.ConvertFromUtf32(Convert.ToInt32(num, 8))))
                            }
                        },
                        "}"
                    },
                    new StringParseTree
                    {
                        {
                            c => char.IsDigit(c, 0), "Digit", new StringParseTree
                            {
                                {
                                    c => char.IsDigit(c, 0), "Digit",
                                    new StringParseTree(
                                        match: num => new StrNode(char.ConvertFromUtf32(Convert.ToInt32(num, 8))))
                                }
                            }
                        }
                    }
                }
            },
            {
                "p", new ParseTree
                {
                    {
                        "{", new StringParseTree(match: cat => new Category(cat))
                        {
                            {c => true, "Anything", new StringParseTree(ParseFlag.Repeat)}
                        },
                        "}"
                    }
                }
            },
            {c => !char.IsDigit(c, 0), "Not digit", c => new StrNode(c)},
            new StringParseTree
            {
                {
                    "0", new StringParseTree
                    {
                        {
                            c => char.IsDigit(c, 0), "Digit", new StringParseTree
                            {
                                {
                                    c => char.IsDigit(c, 0), "Digit",
                                    new StringParseTree(
                                        match: num => new StrNode(char.ConvertFromUtf32(Convert.ToInt32(num, 8))))
                                }
                            }
                        }
                    }
                },
                BackRefTree(false) // Determine at match time if octal character or backreference
            },
        };

        private static StringParseTree BackRefTree(bool named)
        {
            var tree = new StringParseTree
            {
                {
                    c => char.IsDigit(c, 0), "Digit",
                    new StringParseTree(ParseFlag.Repeat, name => new BackReference(int.Parse(name)))
                }
            };
            if (named)
            {
                tree.Add(c => c.IsUnicodeIdentifierStart(), "Unicode Identifier Start",
                    new StringParseTree(match: name => new BackReference(name))
                    {
                        {
                            c => c.IsUnicodeIdentifierPart(), "Unicode Identifier Part",
                            new StringParseTree(ParseFlag.Repeat)
                        }
                    });
            }
            return tree;
        }

        private readonly int[] _indexs;
        // TODO: Error reporting
        // TODO: Group numbering

        private readonly string _regex;
        private readonly StringInfo _regexInfo;
        private int _pos;

        internal RegexParser(string regex)
        {
            _regex = regex;
            _regexInfo = new StringInfo(_regex);
            _indexs = StringInfo.ParseCombiningCharacters(regex);
        }

        private string Look => StringInfo.GetNextTextElement(_regex, _indexs[_pos]);

        private string Next()
        {
            string c = Look;
            _pos++;
            return c;
        }

        private string LookAhead(int length)
        {
            return _regexInfo.SubstringByTextElements(_pos, length);
        }


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
                AST atom = Atom();
                if (atom == null)
                {
                    break;
                }
                AST quantifier = Quantifier(atom);
                concat.Add(quantifier);
            }
            return concat.Build();
        }

        private AST Quantifier(AST atom)
        {
            string c = Look;
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
            string c = Next();
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
            string c = Look;
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
                    Modifiers mods = ParseModifiers();
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
            string c = Next();
            if (c == "P")
            {
                c = Next();
            }
            if (c != "<")
            {
                return null;
            }
            c = Next();
            if ((c == "=") || (c == "!"))
            {
                return new LookBehind(GroupContent(), c == "!");
            }
            _pos--;
            string name = Name(">");
            _pos--;
            if ((name == null) || (Next() != ">"))
            {
                return null;
            }
            return new Group(GroupContent(), name: name);
        }

        private AST GroupContent()
        {
            AST inner = Parse();
            return Next() == ")" ? new Group(inner) : null;
        }

        private Modifiers ParseModifiers()
        {
            string c = Next();
            var mods = new Modifiers();
            var negated = false;
            while ((c != ")") && (c != ":"))
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
            string c = Next();
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
                        CharacterClass innerClass = CharClass();
                        classBuilder.Subtract(innerClass);
                        if (Look != "]")
                        {
                            return null;
                        }
                    }
                    else
                    {
                        string second = Next();
                        classBuilder.AddRange(c, second);
                    }
                }
                else if (LookAhead(2) == "&&")
                {
                    _pos += 2;
                    CharacterClass innerClass = CharClass();
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

        private AST Escape(string c)
        {
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
                    string end = LookAhead(2);
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
                    if (!char.IsDigit(c, 0))
                    {
                        return new StrNode(c);
                    }

                    _pos--;
                    if (c != "0")
                    {
                        return BackRef(false); // Determine at match time if octal character
                    }

                    // Definetely octal, by specicification
                    c = Num(8, 3);
                    return new StrNode(c);
            }
        }

        private AST Escape()
        {
            string c = Next();
            return Escape(c);
        }

        private string Num(int numBase)
        {
            var builder = new StringBuilder();
            string c = Look;
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
            string c = Look;
            while (char.IsDigit(c, 0) && (builder.Length < maxLength))
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
            string c = Next();
            if (!c.IsUnicodeIdentifierStart())
            {
                return null;
            }
            builder.Append(c);
            c = Next();
            while (c.IsUnicodeIdentifierPart() && (c != end))
            {
                builder.Append(c);
                c = Next();
            }
            return (c == end) || (end == null) ? builder.ToString() : null;
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
            string c = Next();
            var builder = new StringBuilder();
            if (!char.IsDigit(c, 0))
            {
                return null;
            }

            while (char.IsDigit(c, 0) && (c != end))
            {
                builder.Append(c);
                c = Next();
            }
            int num = int.Parse(builder.ToString());
            return new BackReference(num);
        }
    }
}