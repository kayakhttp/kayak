using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LitJson;
using System.Collections;
using System.Reflection;

namespace LitJson
{
    public static class DictionaryRepresentation
    {
        public static IDictionary ToDictionary(this object o)
        {
            if (o is IDictionary)
                return (IDictionary)o;
            else
            {
                var result = new Dictionary<string, object>();

                foreach (MemberInfo m in o.GetType().GetFieldsAndReadableProperties())
                    result.Add(m.Name, m.GetPropertyOrFieldValue(o));

                return result;
            }
        }
    }

    public static class TypedJsonMapperDefaultConversions
    {
        public static void AddDefaultInputConversions(this TypedJsonMapper mapper)
        {
            mapper.SetInputConversion<string, double>(s => double.Parse(s));
            mapper.SetInputConversion<string, float>(s => float.Parse(s));
            mapper.SetInputConversion<string, int>(s => int.Parse(s));
            mapper.SetInputConversion<string, bool>(s => bool.Parse(s));
            mapper.SetInputConversion<string, DateTime>(s => DateTime.Parse(s));
            mapper.SetInputConversion<string, Uri>(s => new Uri(s));
        }

        public static void AddDefaultOutputConversions(this TypedJsonMapper mapper)
        {
            mapper.SetOutputConversion<bool>((o, w) => w.Write(o));
            mapper.SetOutputConversion<decimal>((o, w) => w.Write(o));
            mapper.SetOutputConversion<double>((o, w) => w.Write(o));
            mapper.SetOutputConversion<float>((o, w) => w.Write(o));
            mapper.SetOutputConversion<int>((o, w) => w.Write(o));
            mapper.SetOutputConversion<long>((o, w) => w.Write(o));
            mapper.SetOutputConversion<ulong>((o, w) => w.Write(o));
            mapper.SetOutputConversion<string>((o, w) => w.Write(o));
            mapper.SetOutputConversion<DateTime>((o, w) => w.Write(o.ToString("o")));
        }
    }

    public class TypedJsonMapper
    {
        Dictionary<Type, Dictionary<Type, Func<object, object>>> inputConversions;
        Dictionary<Type, Action<object, JsonWriter>> outputConversions;

        public TypedJsonMapper()
        {
            inputConversions = new Dictionary<Type, Dictionary<Type, Func<object, object>>>();
            outputConversions = new Dictionary<Type, Action<object, JsonWriter>>();
        }

        public void SetInputConversion<TFrom, TTo>(Func<TFrom, TTo> func)
        {
            SetInputConversion(typeof(TFrom), typeof(TTo), i => func((TFrom)i));
        }

        public void SetInputConversion(Type fromType, Type toType, Func<object, object> conversion)
        {
            lock (inputConversions)
            {
                if (!inputConversions.ContainsKey(fromType))
                    inputConversions[fromType] = new Dictionary<Type, Func<object, object>>();

                inputConversions[fromType][toType] = conversion;
            }
        }

        public void SetOutputConversion<T>(Action<T, JsonWriter> conversion)
        {
            SetOutputConversion(typeof(T), (o, w) => conversion((T)o, w));
        }

        public void SetOutputConversion(Type fromType, Action<object, JsonWriter> conversion)
        {
            lock (outputConversions)
                outputConversions[fromType] = conversion;
        }

        Func<object, object> GetInputConversion(Type fromType, Type toType)
        {
            lock (inputConversions)
                return inputConversions.ContainsKey(fromType) && inputConversions[fromType].ContainsKey(toType) ?
                    inputConversions[fromType][toType] : null;
        }

        Action<object, JsonWriter> GetOutputConversion(Type fromType)
        {
            lock (outputConversions)
                return outputConversions.ContainsKey(fromType) ? outputConversions[fromType] : null;
        }

        public void WriteObjectProperties(IDictionary properties, JsonWriter writer)
        {
            foreach (var key in properties.Keys)
            {
                writer.WritePropertyName(key as string ?? key.ToString());
                Write(properties[key], writer);
            }
        }

        public void Write(object o, JsonWriter writer)
        {
            if (o == null)
            {
                writer.Write(null);
                return;
            }

            Action<object, JsonWriter> conversion = GetOutputConversion(o.GetType());

            if (conversion != null)
                conversion(o, writer);
            else if (o.GetType().IsEnum)
                writer.Write(Enum.GetName(o.GetType(), o));
            else if (o is IDictionary)
            {
                writer.WriteObjectStart();
                WriteObjectProperties((IDictionary)o, writer);
                writer.WriteObjectEnd();
            }
            else if (o is IEnumerable)
            {
                writer.WriteArrayStart();

                foreach (var i in (IEnumerable)o)
                    Write(i, writer);

                writer.WriteArrayEnd();
            }
            else
            {
                writer.WriteObjectStart();
                WriteObjectProperties(o.ToDictionary(), writer);
                writer.WriteObjectEnd();
            }
        }

        static object ArrayEnd = new object();

        public object Read(Type expectedType, JsonReader reader)
        {
            reader.Read();

            // null if type is not Nullable<>
            Type expectedNullableType = expectedType == null ? null : expectedType.GetTypeOfNullable();

            if (reader.Token == JsonToken.Null)
                if (expectedType != null && expectedType.IsValueType && expectedNullableType == null)
                    throw new Exception(string.Format("Encountered null, expected '{0}'", expectedType));
                else
                    return null;

            switch (reader.Token)
            {
                case JsonToken.Boolean:
                case JsonToken.Double:
                case JsonToken.Int:
                case JsonToken.Long:
                case JsonToken.String:
                    if (expectedType == null)
                        return null;

                    Type actualType = reader.Value.GetType();

                    if (expectedNullableType != null)
                        expectedType = expectedNullableType;

                    if (expectedType.IsAssignableFrom(actualType))
                        return reader.Value;

                    if (expectedType.IsEnum)
                        return Enum.Parse(expectedType, reader.Value as string);

                    var conversion = GetInputConversion(actualType, expectedType);

                    if (conversion != null)
                        return conversion(reader.Value);

                    // TODO: (maybe)
                    // - convert ints to byte, sbyte, short, long, ushort, ulong, uint, double, float, decimal
                    // - convert longs to uint
                    // - convert doubles to float, decimal
                    // - enums?
                    // - implicit conversion operator?

                    throw new Exception(string.Format("Encountered '{0}', expected '{1}'", actualType, expectedType));

                case JsonToken.ArrayEnd:
                    return ArrayEnd;
                case JsonToken.ArrayStart:
                    Type elementType = null;
                    IList list = null;
                    bool isArray = false;

                    if (expectedType == typeof(object))
                    {
                        elementType = typeof(object);
                        list = new ArrayList();
                        isArray = true;
                    }
                    else if (expectedType != null)
                    {
                        isArray = expectedType.GetIsArray();

                        list = isArray ? new ArrayList() : (IList)Activator.CreateInstance(expectedType);
                        elementType = isArray ? expectedType.GetElementType() : expectedType.GetElementTypeOfGenericList();

                        if (elementType == null)
                            //throw new Exception(string.Format(
                            //    "A JSON array was encountered but the type of its elements could not be inferred from the expected " +
                            //    "type ({0}) because the expected type is not an array type and it does not implement IList<T>.", expectedType));
                            elementType = typeof(object);
                    }

                    while (true)
                    {
                        //reader.Read();
                        object item = Read(elementType, reader);

                        //if (reader.Token == JsonToken.ArrayEnd)
                        if (item == ArrayEnd)
                            break;

                        if (list != null)
                            list.Add(item);
                    }

                    if (isArray)
                    {
                        Array array = Array.CreateInstance(elementType, list.Count);

                        for (int i = 0; i < list.Count; i++)
                            array.SetValue(list[i], i);

                        return array;
                    }
                    else return list;

                case JsonToken.ObjectStart:
                    object result = null;

                    if (expectedType == typeof(object))
                        result = new Dictionary<string, object>();
                    else if (expectedType != null)
                        result = Activator.CreateInstance(expectedType);

                    while (true)
                    {
                        reader.Read();

                        if (reader.Token == JsonToken.ObjectEnd)
                            break;

                        MemberInfo propertyOrField = null;

                        if (expectedType == typeof(object))
                        {
                            (result as Dictionary<string, object>).Add((string)reader.Value, Read(typeof(object), reader));
                        }
                        else
                        {
                            if (expectedType != null)
                            {
                                string propertyName = (string)reader.Value;
                                propertyOrField = expectedType.GetFieldOrWriteablePropertyNamed(propertyName);
                            }

                            if (propertyOrField == null)
                                Read(null, reader); // property was not found. calling Read with an expected type of null will simply read through the value and discard it.
                            else
                            {
                                Type propertyType = propertyOrField.GetPropertyOrFieldType();
                                propertyOrField.SetPropertyOrFieldValue(result, Read(propertyType, reader));
                            }
                        }
                    }
                    return result;
                default:
                    return null;
            }
        }
    }
}
