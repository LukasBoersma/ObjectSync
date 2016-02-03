using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using System.Threading;
using System.Collections.Concurrent;

namespace ObjectSync
{
    public class StreamSync
    {
        public Stream Stream { get; set; }

        readonly byte[] MagicBytes = new byte[2] { 0xb0, 0xca };
        readonly byte[] VersionNumber = new byte[2] { 0x00, 0x01 };
        const string TypeIdSeparator = "\n---\n";

        JsonSerializer Serializer;
        JsonWriter Writer;
        JsonReader Reader;

        public bool Receiving;
        public Thread ReceivingThread;

        public ConcurrentQueue<object> UnappliedUpdates = new ConcurrentQueue<object>();

        public StreamSync(Stream stream)
        {
            Stream = stream;
            Writer = new JsonTextWriter(new StreamWriter(Stream));
            Reader = new JsonTextReader(new StreamReader(Stream));
            Serializer = new JsonSerializer();
        }

        void StartSendPackage(Int32 length)
        {
            Stream.Write(MagicBytes, 0, MagicBytes.Length);
            Stream.Write(VersionNumber, 0, VersionNumber.Length);
            Stream.Write(BitConverter.GetBytes(length),0, 4);
        }

        int StartReadPackage()
        {
            var head = new byte[2];
            var readBytes = Stream.Read(head, 0, 2);
            if (readBytes > 0)
            {
                if (!(head[0] == MagicBytes[0] && head[1] == MagicBytes[1]))
                    throw new InvalidDataException("Corrupted data received. Magic header bytes did not match.");
            
                Stream.Read(head, 0, 2);
                if (!(head[0] == VersionNumber[0] && head[1] == VersionNumber[1]))
                    throw new InvalidDataException("Received package from incompatible protocol version.");

                var lengthBytes = new byte[4];
                Stream.Read(lengthBytes, 0, 4);

                return BitConverter.ToInt32(lengthBytes, 0);
            }
            else
                return -1;
        }

        public void WriteUpdate(object obj)
        {
            var typeId = obj.GetType().AssemblyQualifiedName;
            var serialized = JsonConvert.SerializeObject(obj);
            var dataString = typeId + TypeIdSeparator + serialized;
            var data = Encoding.UTF8.GetBytes(dataString);
            StartSendPackage(data.Length);
            Stream.Write(data, 0, data.Length);
        }

        public object ReadUpdate()
        {
            var packageLength = StartReadPackage();
            if (packageLength > 0)
            {
                var data = new byte[packageLength];
                Stream.Read(data, 0, packageLength);

                var dataString = Encoding.UTF8.GetString(data);

                var packageItems = dataString.Split(new string[1] { TypeIdSeparator }, 2, StringSplitOptions.None);

                if (packageItems.Length != 2)
                    throw new InvalidDataException("Invalid package format, unable to separate type id from object data");

                var typeId = packageItems[0];
                var type = Type.GetType(typeId);
                if(type == null)
                    throw new InvalidDataException("Received update for unknown type: " + typeId);

                var serialized = packageItems[1];

                return JsonConvert.DeserializeObject(serialized, type);
            }
            else
                return null;
        }
        
        public void WriteUpdates(IEnumerable<object> objects)
        {
            foreach(var obj in objects)
            {
                WriteUpdate(obj);
            }
        }

        public void BeginReceive()
        {
            if (Receiving)
                throw new InvalidOperationException("Already receiving");

            Receiving = true;

            ReceivingThread = new Thread(() =>
            {
                while (Receiving)
                {
                    var obj = ReadUpdate();
                    if(obj != null)
                    {
                        UnappliedUpdates.Enqueue(obj);
                    }
                    else
                        Thread.Sleep(1);
                }
            });
            ReceivingThread.Start();
        }

        public void StopReceiving()
        {
            if (!Receiving)
                return;

            Receiving = false;
            ReceivingThread.Join();
        }

        public int ApplyReceivedUpdates(Func<object,object> identify)
        {
            int count = 0;
            object update;
            while(UnappliedUpdates.TryDequeue(out update))
            {
                var target = identify(update);
                Sync.SyncState(update, target);
                count++;
            }

            return count;
        }
    }
}
