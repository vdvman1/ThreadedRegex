using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ThreadedRegex.Utility;

namespace ThreadedRegex.Parser
{
    public class ParserState
    {
        internal readonly StringBuilder Builder = new StringBuilder();
    }

    internal class Prefix
    {
        public Prefix(IEither<string, Func<string, bool>> either)
        {
            Either = either;
        }

        public Prefix(string prefix)
        {
            Either = Utility.Either.Of<string, Func<string, bool>>(prefix);
        }

        public Prefix(Func<string, bool> predicate)
        {
            Either = Utility.Either.Of<string, Func<string, bool>>(predicate);
        }

        public IEither<string, Func<string, bool>> Either { get; }

        public bool Matches(string prefix)
        {
            return Either.Case(pref => prefix == pref, predicate => predicate(prefix));
        }
    }

    public class ParseTree {}

    public abstract class ParseNode
    {
        internal virtual bool IgnoreSpace { get; set; }
        public virtual int PrefixSize => 1;
        protected bool shouldRepeat = false;

        internal abstract IParseResult this[string prefix, int repeatCount] { get; }

        internal virtual bool ShouldRepeat(int repeatCount)
        {
            return shouldRepeat;
        }

        public static ThenNode For(string prefix)
        {
            return new ThenNode(prefix);
        }

        public static ThenNode For(Func<string, bool> predicate)
        {
            return new ThenNode(predicate);
        }
    }

    public class SingleChild : ParseNode
    {
        private int prefixSize = -1;

        internal SingleChild(Prefix prefix, ParseNode child)
        {
            Prefix = prefix;
            Child = child;
        }

        internal Prefix Prefix { get; }
        internal ParseNode Child { get; }

        public override int PrefixSize
        {
            get
            {
                if (prefixSize == -1)
                {
                    prefixSize = Prefix.Either.Case(
                        prefix => new StringInfo(prefix).LengthInTextElements,
                        predicate => 1
                        );
                }
                return prefixSize;
            }
        }

        internal override IParseResult this[string prefix, int repeatCount]
        {
            get
            {
                if (Prefix.Matches(prefix))
                {
                    return new ParseNext(Child);
                }
                return new MatchFail();
            }
        }
    }

    internal interface IResultNode {}

    public class ResultNode : SingleChild, IResultNode
    {
        internal ResultNode(Prefix prefix, Func<AST, AST> result) : base(prefix, null)
        {
            Result = result;
        }

        internal Func<AST, AST> Result { get; }

        internal override IParseResult this[string prefix, int repeatCount]
        {
            get
            {
                if (Prefix.Matches(prefix))
                {
                    return new ParseMatch(this);
                }
                return new MatchFail();
            }
        }
    }

    internal interface IParseResult {}

    internal class ParseMatch : IParseResult
    {
        internal ParseMatch(IResultNode result)
        {
            Result = result;
        }

        internal IResultNode Result { get; }
    }

    public class MatchFail : IParseResult {}

    public class ParseNext : IParseResult
    {
        public ParseNext(ParseNode next)
        {
            Next = next;
        }

        internal ParseNode Next { get; }
    }

    public class RetryParse : IParseResult
    {
        public RetryParse(ParseNode next)
        {
            Next = next;
        }

        public ParseNode Next { get; }
    }

    public class ThenNode
    {
        internal readonly List<Prefix> Nodes = new List<Prefix>();
        internal OrBuilder Parent { get; }

        internal ThenNode(string prefix)
        {
            Then(prefix);
        }

        internal ThenNode(Func<string, bool> predicate)
        {
            Nodes.Add(new Prefix(predicate));
        }

        internal ThenNode(OrBuilder parent, string prefix) : this(prefix)
        {
            Parent = parent;
        }

        public ParseNode ParseAs(Func<AST, AST> result)
        {
            int i = Nodes.Count - 1;
            ParseNode res = new ResultNode(Nodes[i], result);
            for (i--; i >= 0; i--)
            {
                res = new SingleChild(Nodes[i], res);
            }
            return res;
        }

        public ThenNode Then(string prefix)
        {
            Prefix node = Nodes[Nodes.Count - 1];
            string newPref = node.Either.Case(pref => pref + prefix, pred => null);
            if (newPref == null)
            {
                Nodes.Add(new Prefix(prefix));
            }
            else
            {
                Nodes[Nodes.Count - 1] = new Prefix(newPref);
            }
            return this;
        }

        public ThenNode Then(Func<string, bool> predicate)
        {
            Nodes.Add(new Prefix(predicate));
            return this;
        }

        public OrBuilder Then(SingleChild node)
        {
            return new OrBuilder(this).Or(node);
        }
    }

    public class OrBuilder
    {
        private readonly Dictionary<Func<string, bool>, ParseNode> dynNodes =
            new Dictionary<Func<string, bool>, ParseNode>();

        private readonly Dictionary<string, ParseNode> nodes = new Dictionary<string, ParseNode>();

        public OrBuilder(ThenNode parent)
        {
            Parent = parent;
        }

        private ThenNode Parent { get; }

        public OrBuilder Or(SingleChild node)
        {
            node.Prefix.Either.Case(
                prefix => nodes.Add(prefix, node.Child),
                predicate => dynNodes.Add(predicate, node.Child)
                );
            return this;
        }

        public ParseNode Compile()
        {
            ParseNode node = new OrNode(dynNodes, nodes);
            for (int i = Parent.Nodes.Count - 1; i >= 0; i--)
            {
                node = new SingleChild(Parent.Nodes[i], node);
            }
            return node;
        }

        public ThenNode Then(string prefix)
        {
            return new ThenNode(this, prefix);
        }
    }

    public class OrNode : ParseNode
    {
        private readonly Dictionary<Func<string, bool>, ParseNode> dynNodes;

        private readonly Dictionary<string, ParseNode> nodes;
        private ParseNode Next { get; }

        internal OrNode(Dictionary<Func<string, bool>, ParseNode> dynNodes, Dictionary<string, ParseNode> nodes)
        {
            this.dynNodes = dynNodes;
            this.nodes = nodes;
        }

        internal OrNode(Dictionary<Func<string, bool>, ParseNode> dynNodes, Dictionary<string, ParseNode> nodes, ParseNode next) : this(dynNodes, nodes)
        {
            Next = next;
        }

        internal override bool ShouldRepeat(int repeatCount)
        {
            return base.ShouldRepeat(repeatCount) || ((Next != null) && (repeatCount == 0));
        }

        internal override IParseResult this[string prefix, int repeatCount]
        {
            get
            {
                if ((Next != null) && (repeatCount == 1))
                {
                    return new RetryParse(Next);
                }
                if (nodes.ContainsKey(prefix))
                {
                    return new ParseNext(nodes[prefix]);
                }
                foreach (KeyValuePair<Func<string, bool>, ParseNode> node in dynNodes)
                {
                    if (node.Key(prefix))
                    {
                        return new ParseNext(node.Value);
                    }
                }
                return new MatchFail();
            }
        }
    }
}