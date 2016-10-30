using System;
using System.Collections.Generic;
using ThreadedRegex.Utility;

namespace ThreadedRegex.Parser
{
    public class AST
    {
    }

    internal class StrNode : AST
    {
        internal string Str { get; private set; }
        public StrNode(string c)
        {
            Str = c;
        }
    }

    internal abstract class Builder
    {
        internal List<AST> Nodes = new List<AST>();

        internal void Add(AST node)
        {
            Nodes.Add(node);
        }

        internal abstract AST Build();
    }

    internal class AlternationBuilder : Builder
    {
        internal override AST Build()
        {
            throw new System.NotImplementedException();
        }
    }

    internal class ConcatBuilder : Builder
    {
        internal override AST Build()
        {
            throw new System.NotImplementedException();
        }
    }

    internal class BeginString : AST
    {
    }

    internal class EndString : AST
    {
        private bool _ignoreNewLine;

        public EndString(bool ignoreNewLine)
        {
            _ignoreNewLine = ignoreNewLine;
        }
    }

    internal class BeginStringOrMatch : AST
    {
    }

    internal class WordBoundry : AST
    {
    }

    internal class Not : AST
    {
        private AST _node;

        public Not(AST node)
        {
            _node = node;
        }
    }

    internal class BackReference : AST
    {
        private string _name;
        private int _num;

        public BackReference(string name)
        {
            _name = name;
        }

        public BackReference(int num)
        {
            _num = num;
        }
    }

    internal class Digit : AST
    {
    }

    internal class Word : AST
    {
    }

    internal class Space : AST
    {
        
    }

    internal class Tab : AST
    {
    }

    internal class CarriageReturn : AST
    {
    }

    internal class LineBreak : AST
    {
    }

    internal class LineFeed : AST
    {
    }

    internal class HorizontalSpace : AST
    {
    }

    internal class VerticalSpace : AST
    {
    }

    internal class ClearMatch : AST
    {
    }

    internal class AlarmChar : AST
    {
    }

    internal class Control : AST
    {
        private readonly string _c;

        public Control(string c)
        {
            _c = c;
        }
    }

    internal class EscapeChar : AST
    {
    }

    internal class Category : AST
    {
        private string _category;

        public Category(string category)
        {
            _category = category;
        }
    }

    internal class StartStringOrLine : AST
    {
    }

    internal class EndStringOrLine : AST
    {
    }

    internal class CharacterClassBuilder
    {

        internal CharacterRange Chars = new CharacterRange();
        internal CharacterRange Other;
        internal bool Subtracting = false;

        internal CharacterClass Build()
        {
            if (Negate)
            {
                Chars.Negate();
            }
            if (Other != null)
            {
                if (Subtracting)
                {
                    Chars.Subtract(Other);
                }
                else
                {
                    Chars.Intersection(Other);
                }
            }
            return new CharacterClass(Chars);
        }

        internal void Add(string c)
        {
            Chars.Add(c);
        }

        internal void AddRange(string a, string b)
        {
            Chars.AddRange(a, b);
        }

        internal void Subtract(CharacterClass other)
        {
            Other = other.Chars;
            Subtracting = true;
        }

        internal void Intersect(CharacterClass other)
        {
            Other = other.Chars;
        }

        public bool Negate { get; internal set; } = false;
    }

    internal class CharacterClass : AST
    {
        public CharacterClass(CharacterRange chars)
        {
            Chars = chars;
        }

        internal CharacterRange Chars { get; }
    }

    internal class Quantified : AST
    {
        internal enum Laziness
        {
            Lazy, Greedy, Possesive
        }

        // ReSharper disable once InconsistentNaming
        internal Laziness laziness;
        protected AST Node;

        public Quantified(AST node)
        {
            Node = node;
        }
    }

    internal class ZeroOrOne : Quantified
    {
        public ZeroOrOne(AST node) : base(node)
        {
        }
    }

    internal class ZeroOrMore : Quantified
    {
        public ZeroOrMore(AST node) : base(node)
        {
        }
    }

    internal class OneOrMore : Quantified
    {
        public OneOrMore(AST node) : base(node)
        {
        }
    }

    internal class Group : AST
    {
        private AST _node;
        private Modifiers _mods;
        private string _name;

        public Group(AST inner, Modifiers mods = null, string name = null)
        {
            _node = inner;
            _mods = mods;
            _name = name;
        }
    }

    internal struct Modifier
    {
        public Modifier(ModifierType type, bool negate)
        {
            Negate = negate;
            Type = type;
        }

        internal enum ModifierType
        {
            I, S, M, X, N, D
        }

        public bool Negate { get; }
        public ModifierType Type { get; }
    }

    internal class Modifiers : AST
    {
        private List<Modifier> _modifiers = new List<Modifier>();

        public void Add(Modifier.ModifierType type, bool negate)
        {
            _modifiers.Add(new Modifier(type, negate));
        }
    }

    internal class LookAhead : AST
    {
        private AST _inner;
        private bool _negate;

        public LookAhead(AST inner, bool negate)
        {
            _inner = inner;
            _negate = negate;
        }
    }
    internal class LookBehind : AST
    {
        private AST _inner;
        private bool _negate;

        public LookBehind(AST inner, bool negate)
        {
            _inner = inner;
            _negate = negate;
        }
    }
}