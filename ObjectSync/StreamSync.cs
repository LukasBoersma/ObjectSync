using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Threading;
using System.Collections.Concurrent;

namespace ObjectSync
{
    public class StreamSync
    {
        public Stream Stream { get; set; }

        readonly byte[] MagicBytes = new byte[2] { 0xb0, 0xca };
        readonly byte[] VersionNumber = new byte[2] { 0x00, 0x02 };

        public bool Receiving;
        public Thread ReceivingThread;

        public ConcurrentQueue<object> UnappliedUpdates = new ConcurrentQueue<object>();

        public bool HasUnappliedUpdates {  get { return !UnappliedUpdates.IsEmpty; } }

        public StreamSync(Stream stream)
        {
            Stream = stream;
        }

        void SendPackageHeader(ObjectModel model, Int32 length)
        {
            Stream.Write(MagicBytes, 0, MagicBytes.Length);
            Stream.Write(VersionNumber, 0, VersionNumber.Length);
            Stream.Write(BitConverter.GetBytes(model.TypeId), 0, 2);
            Stream.Write(BitConverter.GetBytes(length), 0, 4);
        }

        void ReadPackageHead(out int dataLength, out ObjectModel model)
        {
            var head = new byte[2];
            var readBytes = Stream.Read(head, 0, 2);
            if (readBytes > 0)
            {
                // Check Magic bytes
                if (!(head[0] == MagicBytes[0] && head[1] == MagicBytes[1]))
                    throw new InvalidDataException("Corrupted data received. Magic header bytes did not match.");

                // Check version number
                Stream.Read(head, 0, 2);
                if (!(head[0] == VersionNumber[0] && head[1] == VersionNumber[1]))
                    throw new InvalidDataException("Received package from incompatible protocol version.");

                // Get the type id
                Stream.Read(head, 0, 2);
                UInt16 typeId = BitConverter.ToUInt16(head, 0);

                model = Sync.FindModelFromTypeId(typeId);

                var lengthBytes = new byte[4];
                Stream.Read(lengthBytes, 0, 4);

                dataLength = BitConverter.ToInt32(lengthBytes, 0);
            }
            else
            {
                dataLength = -1;
                model = null;
            }
        }

        public void WriteUpdate(object obj)
        {
            var model = Sync.FindModelFromType(obj.GetType());
            var data = Serializer.Serialize(obj, model);
            SendPackageHeader(model, data.Length);
            Stream.Write(data, 0, data.Length);
        }

        public object ReadUpdate()
        {
            ObjectModel model;
            int packageLength;
            ReadPackageHead(out packageLength, out model);
            if (packageLength > 0)
            {
                var data = new byte[packageLength];
                Stream.Read(data, 0, packageLength);
                
                return Serializer.Deserialize(data, model);
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
                if(target != null)
                    Sync.SyncState(update, target);
                count++;
            }

            return count;
        }
    }
}
