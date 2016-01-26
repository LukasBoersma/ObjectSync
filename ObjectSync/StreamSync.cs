using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using System.Threading;

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

        public Queue<object> UnappliedUpdates = new Queue<object>();

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
            Stream.Read(head, 0, 2);
            if (!head.Equals(MagicBytes))
                throw new InvalidDataException("Corrupted data received. Magic header bytes did not match.");
            
            Stream.Read(head, 0, 2);
            if (!head.Equals(VersionNumber))
                throw new InvalidDataException("Received package from incompatible protocol version.");

            var lengthBytes = new byte[4];
            Stream.Read(lengthBytes, 0, 4);

            return BitConverter.ToInt32(lengthBytes, 0);
        }

        public void Serialize(object obj)
        {
            var typeId = obj.GetType().FullName;
            var serialized = JsonConvert.SerializeObject(obj);
            var dataString = typeId + TypeIdSeparator + serialized;
            var data = Encoding.UTF8.GetBytes(dataString);
            StartSendPackage(data.Length);
            Stream.Write(data, 0, data.Length);
        }

        public object Deserialize()
        {
            var packageLength = StartReadPackage();
            var data = new byte[packageLength];
            Stream.Read(data, 0, packageLength);

            var dataString = Encoding.UTF8.GetString(data);

            var packageItems = dataString.Split(new string[1] { TypeIdSeparator }, 1, StringSplitOptions.None);

            if (packageItems.Length != 2)
                throw new InvalidDataException("Invalid package format, unable to separate type id from object data");

            var typeId = packageItems[0];
            var type = Type.GetType(typeId);

            var serialized = packageItems[1];

            return JsonConvert.DeserializeObject(serialized, type);
        }
        
        public void WriteUpdates(IEnumerable<object> objects)
        {
            foreach(var obj in objects)
            {
                Serialize(obj);
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
                    var obj = Deserialize();
                    UnappliedUpdates.Enqueue(obj);
                }
            });
        }

        public void StopReceiving()
        {
            if (!Receiving)
                return;

            Receiving = false;
            ReceivingThread.Join();
        }

        public void ApplyReceivedUpdates(Func<object,object> identify)
        {
            while(UnappliedUpdates.Count > 0)
            {
                var update = UnappliedUpdates.Dequeue();
                var target = identify(update);
                Sync.SyncState(update, target);
            }
        }
    }
}
