using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ObjectSync;
using System.IO;

namespace ObjectSync.Demo
{
    class Boo
    {
        [Synced]
        public int a;
    }

    class Foo
    {
        [Synced]
        public int x;
        [Synced]
        public int y;
        [Synced]
        public int z;

        [Synced]
        public Boo b;

        public override string ToString()
        {
            return String.Format("({0}|{1}|{2}|{3})", x, y, z, b.a);
        }
    }

    class StreamedObject
    {
        [Synced]
        public int Id;

        [Synced]
        public int B;
    }

    class Program
    {
        static void Main(string[] args)
        {
            // All synced classes must be registered
            Sync.RegisterClass<Foo>(() => new Foo());
            Sync.RegisterClass<Boo>(() => new Boo());
            Sync.RegisterClass<StreamedObject>(() => new StreamedObject());

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

            // Synchronize the state of a to b
            Sync.SyncState(a, b);

            // Now all values in a and b should be the same
            Console.WriteLine(a);
            Console.WriteLine(b);

            // Synchronization via stream, see below
            var stream = new MemoryStream();
            StreamServer(stream);
            stream.Seek(0, SeekOrigin.Begin);
            StreamClient(stream);
        }

        static void StreamServer(Stream stream)
        {
            var objsById = new Dictionary<int, StreamedObject>();
            for (int i = 0; i < 10; i++)
            {
                objsById[i] = new StreamedObject
                {
                    Id = i,
                    B = 15 * i * i
                };
            }

            var streamSync = new StreamSync(stream);

            streamSync.WriteUpdates(objsById.Values);
        }

        static void StreamClient(Stream stream)
        {
            var objsById = new Dictionary<int, StreamedObject>();
            var streamSync = new StreamSync(stream);
            streamSync.BeginReceive();

            Func<object,object> identify = (o) =>
            {
                var obj = o as StreamedObject;
                if (objsById.ContainsKey(obj.Id))
                    return objsById[obj.Id];
                else
                    return new StreamedObject();
            };

            while (true)
            {
                var updateCount = streamSync.ApplyReceivedUpdates(identify);
                if (updateCount > 0)
                    Console.WriteLine(String.Format("Received {0} updates", updateCount));
                System.Threading.Thread.Sleep(100);
            }
        }
    }
}
