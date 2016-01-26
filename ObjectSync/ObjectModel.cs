using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Diagnostics;

namespace ObjectSync
{
    public abstract class ObjectModel
    {
        public Type Type
        {
            get; set;
        }

        public abstract void Transfer(object from, ref object to);

        public abstract object CreateInstance();

        public ObjectModel(Type ObjectType)
        {
            Type = ObjectType;
        }
    }

    public class ClassModel<T> : ObjectModel
        where T : class
    {
        public IEnumerable<FieldInfo> ValueFields { get; set; }
        public IEnumerable<FieldInfo> ReferenceFields { get; set; }

        public ClassModel(Func<T> customConstructor = null):
            base(typeof(T))
        {
            CustomConstructor = customConstructor;

            var allFields = Type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            ValueFields = (from field in allFields
                           where field.FieldType.IsValueType
                           select field)
                           .ToArray();

            ReferenceFields = (from field in allFields
                           where !field.FieldType.IsValueType
                           select field)
                           .ToArray();
        }

        public override void Transfer(object from, ref object to)
        {
            if (!(from is T) || !(to is T))
                throw new InvalidOperationException("Calling Transfer with incompatible types");

            foreach(var field in ValueFields)
            {
                var value = field.GetValue(from);
                field.SetValue(to, value);
            }

            foreach(var field in ReferenceFields)
            {
                var fromValue = field.GetValue(from);
                var toValue = field.GetValue(to);

                if (fromValue != null)
                {
                    if (toValue != null)
                        Sync.SyncState(fromValue, toValue);
                    else
                        field.SetValue(to, Sync.CreateCopy(fromValue));
                }
                else
                {
                    field.SetValue(to, null);
                }
            }
        }

        Func<T> CustomConstructor;

        public override object CreateInstance()
        {
            return CustomConstructor?.Invoke();
        }
    }

    public class StringModel : ObjectModel
    {
        public StringModel(): base(typeof(string))
        {

        }

        public override object CreateInstance()
        {
            return "";
        }

        public override void Transfer(object from, ref object to)
        {
            to = from;
        }
    }
}
