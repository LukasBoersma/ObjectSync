using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ObjectSync
{
    public static class Sync
    {
        static Dictionary<Type, ObjectModel> ModelsByType = new Dictionary<Type, ObjectModel>();
        static Dictionary<UInt16, ObjectModel> ModelsByTypeId = new Dictionary<UInt16, ObjectModel>();

        static Sync()
        {
            RegisterModel(new StringModel());
        }
        

        static public ObjectModel FindModelFromType(Type type)
        {
            ObjectModel model = null;
            ModelsByType.TryGetValue(type, out model);
            return model;
        }

        static public ObjectModel FindModelFromTypeId(UInt16 typeId)
        {
            ObjectModel model = null;
            ModelsByTypeId.TryGetValue(typeId, out model);
            return model;
        }

        static public void RegisterModel(ObjectModel model)
        {
            ModelsByType[model.Type] = model;
            ModelsByTypeId[model.TypeId] = model;
        }

        static public void RegisterClass<T>(Func<T> constructor)
            where T : class
        {
            RegisterModel(new ClassModel<T>(constructor));
        }

        static public bool CanSync(object from)
        {
            var type = from.GetType();
            ObjectModel model;
            var hasModel = ModelsByType.TryGetValue(type, out model);
            return hasModel && model.CanTransfer;
        }

        static public void SyncState(object from, object to)
        {
            var type = from.GetType();
            ObjectModel model;
            var hasModel = ModelsByType.TryGetValue(type, out model);
            if (hasModel && model.CanTransfer)
            {
                model.Transfer(from, ref to);
            }
            else if (type.IsValueType)
                from = to;
            else
                throw new InvalidOperationException("Syncing state of unknown or incompatible type");
        }

        static public object CreateCopy(object prototype)
        {
            var type = prototype.GetType();
            object copy = null;

            ObjectModel model;
            var hasModel = ModelsByType.TryGetValue(type, out model);
            if (hasModel && model.CanTransfer)
                copy = model.CreateInstance();
            else
                throw new InvalidOperationException("Creating copy of unknown or incompatible type");
            
            SyncState(prototype, copy);

            return copy;
        }
        
    }
}
