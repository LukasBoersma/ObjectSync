using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace ObjectSync
{
    public static class Serializer
    {
        public static byte[] Serialize(object obj, ObjectModel model)
        {
            //ProtoBuf.Serializer.NonGeneric.Serialize(stream, obj);
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj));
        }

        public static object Deserialize(byte[] data, ObjectModel model)
        {
            //return ProtoBuf.Serializer.NonGeneric.Deserialize(model.Type, stream);
            var json = Encoding.UTF8.GetString(data);
            return JsonConvert.DeserializeObject(json, model.Type);
        }
    }
}
