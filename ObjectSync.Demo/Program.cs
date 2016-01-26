using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ObjectSync;

namespace ObjectSync.Demo
{
    class Boo
    {
        public int a;
    }

    class Foo
    {
        public int x;
        public int y;
        public int z;

        public Boo b;

        public override string ToString()
        {
            return String.Format("({0}|{1}|{2}|{3})", x, y, z, b.a);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Sync.RegisterClass<Foo>(()=>new Foo());
            Sync.RegisterClass<Boo>(() => new Boo());

            var a = new Foo
            {
                x = 1,
                y = 2,
                z = 3,
                b = new Boo
                {
                    a = 456
                }
            };
            
            var b = new Foo();

            Sync.SyncState(a, b);

            Console.WriteLine(a);
            Console.WriteLine(b);
        }
    }
}
