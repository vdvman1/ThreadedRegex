using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreadedRegex.Utility
{
    public interface IEither<out TL, out TR>
    {
        TU Case<TU>(Func<TL, TU> left, Func<TR, TU> right);
        void Case(Action<TL> left, Action<TR> right);
    }

    public static class Either
    {
        private sealed class LeftImpl<TL, TR> : IEither<TL, TR>
        {
            private readonly TL value;

            public LeftImpl(TL value)
            {
                this.value = value;
            }

            public TU Case<TU>(Func<TL, TU> left, Func<TR, TU> right)
            {
                if (left == null)
                {
                    throw new ArgumentNullException(nameof(left));
                }
                return left(value);
            }

            public void Case(Action<TL> left, Action<TR> right)
            {
                if (left == null)
                {
                    throw new ArgumentNullException(nameof(left));
                }
                left(value);
            }
        }
        private sealed class RightImpl<TL, TR> : IEither<TL, TR>
        {
            private readonly TR value;

            public RightImpl(TR value)
            {
                this.value = value;
            }

            public TU Case<TU>(Func<TL, TU> left, Func<TR, TU> right)
            {
                if (right == null)
                {
                    throw new ArgumentNullException(nameof(right));
                }
                return right(value);
            }

            public void Case(Action<TL> left, Action<TR> right)
            {
                if (right == null)
                {
                    throw new ArgumentNullException(nameof(right));
                }
                right(value);
            }
        }

        public static IEither<TL, TR> Of<TL, TR>(TL value)
        {
            return new LeftImpl<TL, TR>(value);
        }

        public static IEither<TL, TR> Of<TL, TR>(TR value)
        {
            return new RightImpl<TL, TR>(value);
        }
    }
}
