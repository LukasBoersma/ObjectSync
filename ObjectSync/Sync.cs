using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ObjectSync
{
    public static class Sync
    {
        static Dictionary<Type, ObjectModel> ObjectModels = new Dictionary<Type, ObjectModel>();

        static Sync()
        {
            ObjectModels[typeof(string)] = new StringModel();
        }

        static public void RegisterClass<T>(Func<T> constructor)
            where T : class
        {
            ObjectModels[typeof(T)] = new ClassModel<T>(constructor);
        }

        static public bool CanSync(object from)
        {
            var type = from.GetType();
            ObjectModel model;
            var hasModel = ObjectModels.TryGetValue(type, out model);
            return hasModel && model.CanTransfer;
        }

        static public void SyncState(object from, object to)
        {
            var type = from.GetType();
            ObjectModel model;
            var hasModel = ObjectModels.TryGetValue(type, out model);
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
            var hasModel = ObjectModels.TryGetValue(type, out model);
            if (hasModel && model.CanTransfer)
                copy = model.CreateInstance();
            else
                throw new InvalidOperationException("Creating copy of unknown or incompatible type");
            
            SyncState(prototype, copy);

            return copy;
        }
        
    }
}
