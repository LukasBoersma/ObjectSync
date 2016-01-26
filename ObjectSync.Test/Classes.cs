using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ObjectSync.Test
{
    public class Foo
    {
        [Synced]
        public int A;

        [Synced]
        public double B { get; set; }

        [Synced]
        public string C;

        public override bool Equals(object obj)
        {
            var foo = obj as Foo;
            if (foo == null)
                return false;
            else
                return foo.A == A && foo.B == B && foo.C == C;
        }
    }

    public class Bar
    {
        [Synced]
        public Foo A;

        [Synced]
        public Foo B;

        [Synced]
        public bool C;

        public override bool Equals(object obj)
        {
            var bar = obj as Bar;
            if (bar == null)
                return false;
            else
                return bar.A.Equals(A) && bar.B.Equals(B) && bar.C == C;
        }
    }

    [TestClass]
    public class Classes
    {
        [TestMethod]
        public void ClassCopies()
        {
            Sync.RegisterClass<Foo>(() => new Foo());
            Sync.RegisterClass<Bar>(() => new Bar());

            var bar = new Bar
            {
                A = new Foo
                {
                    A = 1,
                    B = 2,
                    C = "Hey"
                },
                B = new Foo
                {
                    A = 4,
                    B = 5,
                    C = "Ho!"
                },
                C = false
            };

            var copy = Sync.CreateCopy(bar);

            Assert.AreEqual(copy, bar);
        }
    }
}
