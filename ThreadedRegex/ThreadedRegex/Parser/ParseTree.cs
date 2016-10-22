using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ThreadedRegex.Utility;

namespace ThreadedRegex.Parser
{
    internal interface IParseTree : IEnumerable
    {
        bool IgnoreSpace { get; }
        Func<AST, AST> Process { get; }
        ParseFlag Flags { get; }
        IEither<IParseTree, AST> this[string prefix, ParserState state] { get; }
        void Repeat(string prefix);
    }

    internal class ParseTree : IParseTree
    {
        private readonly Dictionary<string, Lazy<IParseTree>> children = new Dictionary<string, Lazy<IParseTree>>();
        private readonly Dictionary<string, Func<AST>> astChildren = new Dictionary<string, Func<AST>>();

        private readonly List<Tuple<Func<string, bool>, string, Func<string, AST>>> astPredicates =
            new List<Tuple<Func<string, bool>, string, Func<string, AST>>>();

        private readonly List<Tuple<Func<string, bool>, string, Lazy<IParseTree>>> childPredicates =
            new List<Tuple<Func<string, bool>, string, Lazy<IParseTree>>>();

        private Func<string, AST> defaultNode;
        private Lazy<IParseTree> defaultTree;

        public ParseTree(ParseFlag flags = ParseFlag.None, bool ignoreSpace = false, Func<AST, AST> process = null,
            Lazy<IParseTree> defaultTree = null)
        {
            this.defaultTree = defaultTree;
            Flags = flags;
            Process = process;
            IgnoreSpace = ignoreSpace;
        }

        public void Add(string key, IParseTree val)
        {
            if ((val.Flags & ParseFlag.Repeat) != 0)
            {
                val.Repeat(key);
            }
            Add(key, () => val);
        }

        public void Add(string key, StringParseTree tree, string delimiter)
        {
            tree.Delimiter = delimiter;
            Add(key, tree);
        }

        public void Add(string key, Func<IParseTree> val)
        {
            children.Add(key, new Lazy<IParseTree>(val));
        }

        public void Add(string key, Func<AST> val)
        {
            astChildren.Add(key, val);
        }

        public void Add(Func<string, AST> defaultNode)
        {
            this.defaultNode = defaultNode;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return children.Cast<object>().Concat(astPredicates).GetEnumerator();
        }

        public bool IgnoreSpace { get; }
        public Func<AST, AST> Process { get; }
        public ParseFlag Flags { get; }

        public IEither<IParseTree, AST> this[string prefix, ParserState state]
        {
            get
            {
                if (children.ContainsKey(prefix))
                {
                    return Either.Of<IParseTree, AST>(children[prefix].Value);
                }
                if (astChildren.ContainsKey(prefix))
                {
                    return Either.Of<IParseTree, AST>(astChildren[prefix]());
                }
                foreach (Tuple<Func<string, bool>, string, Func<string, AST>> predicate in astPredicates)
                {
                    if (!predicate.Item1(prefix)) continue;

                    return Either.Of<IParseTree, AST>(predicate.Item3(prefix));
                }
                foreach (Tuple<Func<string, bool>, string, Lazy<IParseTree>> predicate in childPredicates)
                {
                    if (!predicate.Item1(prefix)) continue;

                    return Either.Of<IParseTree, AST>(predicate.Item3.Value);
                }
                if (defaultNode != null)
                {
                    return Either.Of<IParseTree, AST>(defaultNode(prefix));
                }
                if (defaultTree != null)
                {
                    return defaultTree.Value[prefix, state];
                }
                throw new ParseException(children.Keys);
            }
        }

        public void Add(Func<string, bool> predicate, string name, Func<string, AST> node)
        {
            astPredicates.Add(new Tuple<Func<string, bool>, string, Func<string, AST>>(predicate, name, node));
        }

        public void Add(Func<string, bool> predicate, string name, Func<IParseTree> node)
        {
            childPredicates.Add(new Tuple<Func<string, bool>, string, Lazy<IParseTree>>(predicate, name,
                new Lazy<IParseTree>(node)));
        }

        public void Add(Func<string, bool> predicate, string name, IParseTree node)
        {
            Add(predicate, name, () => node);
        }

        public void Add(IParseTree defaultTree)
        {
            Add(() => defaultTree);
        }

        public void Add(Func<IParseTree> defaultTree)
        {
            this.defaultTree = new Lazy<IParseTree>(defaultTree);
        }

        public void Repeat(string prefix)
        {
            Add(prefix, () => this);
        }
    }

    internal class StringParseTree : IParseTree
    {
        private readonly List<Tuple<Func<string, bool>, string, StringParseTree>> predicates =
            new List<Tuple<Func<string, bool>, string, StringParseTree>>();

        private readonly Dictionary<string, StringParseTree> children = new Dictionary<string, StringParseTree>();

        private Func<string, AST> match;
        public string Delimiter { get; internal set; }
        private string repeatStr;
        private Tuple<Func<string, bool>, string> repeat;
        private Lazy<StringParseTree> defaultTree;

        public StringParseTree(ParseFlag flags = ParseFlag.None, Func<string, AST> match = null)
        {
            this.match = match;
            Flags = flags;
        }

        public bool IgnoreSpace { get; } = false;
        public Func<AST, AST> Process { get; } = null;
        public ParseFlag Flags { get; }

        public IEither<IParseTree, AST> this[string prefix, ParserState state]
        {
            get
            {
                if (prefix == Delimiter)
                {
                    string str = state.Builder.ToString();
                    if (string.IsNullOrEmpty(str))
                    {
                        throw new ParseException("Delimiter was reached before any contents was found");
                    }
                    state.Builder.Clear();
                    return Either.Of<IParseTree, AST>(match(str));
                }
                if (children.ContainsKey(prefix))
                {
                    state.Builder.Append(prefix);
                    StringParseTree tree = children[prefix];
                    if (tree.Delimiter == null)
                    {
                        tree.Delimiter = Delimiter;
                    }
                    if (tree.match == null)
                    {
                        tree.match = match;
                    }
                    return Either.Of<IParseTree, AST>(tree);
                }
                foreach (Tuple<Func<string, bool>, string, StringParseTree> predicate in predicates)
                {
                    if (!predicate.Item1(prefix)) continue;

                    state.Builder.Append(prefix);
                    StringParseTree tree = predicate.Item3;
                    if (tree.Delimiter == null)
                    {
                        tree.Delimiter = Delimiter;
                    }
                    if (tree.match == null)
                    {
                        tree.match = match;
                    }
                    return Either.Of<IParseTree, AST>(tree);
                }
                if (prefix == repeatStr)
                {
                    return Either.Of<IParseTree, AST>(this);
                }

                if (repeat != null)
                {
                    if (repeat.Item1(prefix))
                    {
                        return Either.Of<IParseTree, AST>(this);
                    }
                }

                if (defaultTree != null)
                {
                    return defaultTree.Value[prefix, state];
                }

                IEnumerable<string> names = predicates.Select(predicate => predicate.Item2);
                if (repeatStr != null)
                {
                    names = names.Concat(new[] {repeatStr});
                }
                if (repeat != null)
                {
                    names = names.Concat(new[] {repeat.Item2});
                }
                throw new ParseException(names);
            }
        }

        public void Repeat(string prefix)
        {
            repeatStr = prefix;
        }

        public void Add(Func<string, bool> predicate, string name, StringParseTree tree)
        {
            if ((tree.Flags & ParseFlag.Repeat) != 0)
            {
                tree.Repeat(predicate, name);
            }
            predicates.Add(new Tuple<Func<string, bool>, string, StringParseTree>(predicate, name, tree));
        }

        private void Repeat(Func<string, bool> predicate, string name)
        {
            repeat = new Tuple<Func<string, bool>, string>(predicate, name);
        }

        public void Add(string prefix, StringParseTree tree)
        {
            children.Add(prefix, tree);
        }
        public void Add(StringParseTree defaultTree)
        {
            Add(() => defaultTree);
        }

        public void Add(Func<StringParseTree> defaultTree)
        {
            this.defaultTree = new Lazy<StringParseTree>(defaultTree);
        }

        public IEnumerator GetEnumerator()
        {
            return predicates.GetEnumerator();
        }
    }

    /**
     * Marker class for a complete match that returns nothing
     * Indicates that the nearest repeating parent should be repeated
     **/

    internal class MatchComplete : IParseTree
    {
        public IEnumerator GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public bool IgnoreSpace { get; }
        public Func<AST, AST> Process { get; }
        public ParseFlag Flags { get; }

        public IEither<IParseTree, AST> this[string prefix, ParserState state]
        {
            get { throw new NotImplementedException(); }
        }

        public void Repeat(string prefix)
        {
            throw new NotImplementedException();
        }
    }

    [Flags]
    internal enum ParseFlag
    {
        None = 0,
        Repeat = 1 << 0,
        RepeatChildren = 1 << 1
    }
}