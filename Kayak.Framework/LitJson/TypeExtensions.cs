using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections;

namespace LitJson
{
    // TODO: cache all of this
    static class TypeExtensions
    {
        public static bool GetIsNullable(this Type t)
        {
            return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        public static Type GetTypeOfNullable(this Type t)
        {
            return t.GetIsNullable() ? t.GetGenericArguments()[0] : null;
        }

        public static bool GetIsArray(this Type t)
        {
            return t.IsArray;
        }

        public static bool GetIsGenericList(this Type t)
        {
            foreach (Type i in t.GetInterfaces())
                if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>))
                    return true;

            return false;
        }

        public static Type GetElementTypeOfGenericList(this Type t)
        {
            return t.GetIsGenericList() ? t.GetGenericArguments()[0] : null;
        }

        public static MemberInfo GetFieldOrWriteablePropertyNamed(this Type t, string fieldOrPropertyName)
        {
            return (MemberInfo)t.GetField(fieldOrPropertyName) ?? (MemberInfo)t.GetProperty(fieldOrPropertyName);
        }

        public static Type GetPropertyOrFieldType(this MemberInfo m)
        {
            return m is PropertyInfo ? ((PropertyInfo)m).PropertyType : ((FieldInfo)m).FieldType;
        }

        public static void SetPropertyOrFieldValue(this MemberInfo m, object obj, object value)
        {
            if (m is PropertyInfo && (m as PropertyInfo).CanWrite)
                ((PropertyInfo)m).SetValue(obj, value, null);
            else if (m is FieldInfo)
                ((FieldInfo)m).SetValue(obj, value);
        }

        public static object GetPropertyOrFieldValue(this MemberInfo m, object obj)
        {
            return m is PropertyInfo ? ((PropertyInfo)m).GetValue(obj, null) : ((FieldInfo)m).GetValue(obj);
        }

        public static MemberInfo[] GetFieldsAndReadableProperties(this Type t)
        {
            var result = new List<MemberInfo>();
            result.AddRange(t.GetFields());
            result.AddRange(t.GetProperties().Where(p => p.CanRead).Cast<MemberInfo>());
            return result.ToArray();
        }
    }
}
