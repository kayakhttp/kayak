using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak
{
    #region NameValuePair (enumeration support class)

    /// <summary>
    /// Defines a pair of a single name and one or more values assocated with that name.
    /// </summary>
    public struct NameValuePair
    {
        string name;
        IList<string> values;

        public string Name { get { return name; } }
        public IList<string> Values { get { return values; } }

        /// <summary>
        /// Gets the value associated with this name. If there are multiple values, they will be
        /// returned as one comma-separated string (without padding).
        /// </summary>
        public string Value
        {
            get { return Values.ToCommaSeparatedString(); }
        }

        /// <summary>
        /// Creates a new NameValuePair with the given name and collection of values.
        /// </summary>
        /// <param name="values">The collection of values associated with the given name.
        /// Note that they will not be copied to a new collection.</param>
        public NameValuePair(string name, IList<string> values)
        {
            this.name = name;
            this.values = values;
        }

        /// <summary>
        /// Creates a new NameValuePair with the given name and single value.
        /// </summary>
        public NameValuePair(string name, string value)
        {
            this.name = name;
            this.values = new string[] { value };
        }

        public override string ToString()
        {
            return "{" + name + ": " + Value + "}";
        }
    }

    #endregion

    /// <summary>
    /// Implements a sorted list of string name-value pairs, with semantics like a string-string
    /// hashtable. Names are permitted to have multiple values.
    /// </summary>
    public class NameValueDictionary : ICollection<NameValuePair>
    {
        SortedList<string, List<string>> dictionary;

        /// <summary>
        /// Initializes a new empty instance of NameValueDictionary.
        /// </summary>
        public NameValueDictionary()
        {
            dictionary = new SortedList<string, List<string>>();
        }

        /// <summary>
        /// Gets whether this dictionary is read-only.
        /// </summary>
        public bool IsReadOnly { get; protected set; }

        /// <summary>
        /// Makes this dictionary read-only. This is a one-way street.
        /// </summary>
        public void BecomeReadOnly()
        {
            ThrowIfReadOnly();
            IsReadOnly = true;
        }

        public IEnumerator<NameValuePair> GetEnumerator()
        {
            foreach (var pair in dictionary)
                yield return new NameValuePair(pair.Key, pair.Value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Gets a list of all the names in this dictionary.
        /// </summary>
        public IList<string> Names
        {
            get { return dictionary.Keys; }
        }

        #region CRUD Methods

        /// <summary>
        /// Adds a name-value pair to this dictionary. If the name exists in the dictionary already,
        /// the value will be added to the list of values for that name.
        /// </summary>
        public void Add(string name, string value)
        {
            ThrowIfReadOnly();

            var list = GetValues(name) as List<string>;

            if (list == null)
            {
                list = new List<string>(1);
                list.Add(value);
                dictionary.Add(name, list);
            }
            else list.Add(value);
        }

        /// <summary>
        /// Adds the name-value pairs in the dictionary to this dictionary. If a name already
        /// exists, the values will be appended to the list of values for that name.
        /// </summary>
        /// <param name="dict"></param>
        public void Add(NameValueDictionary dict)
        {
            foreach (var name in dict.Names)
                AddRange(name, dict.GetValues(name));
        }

        /// <summary>
        /// Adds a name-values pair to this dictionary. If the name exists in the dictionary already,
        /// the values will be appended to the list of values for that name.
        /// </summary>
        public void AddRange(string name, IEnumerable<string> values)
        {
            ThrowIfReadOnly();

            var list = GetValues(name) as List<string>;

            if (list == null)
                dictionary.Add(name, values.ToList());
            else
                list.AddRange(values);
        }

        /// <summary>
        /// Removes the values associated with the given name from this dictionary.
        /// </summary>
        public void Remove(string name)
        {
            ThrowIfReadOnly();
            dictionary.Remove(name);
        }

        /// <summary>
        /// Gets a value for a name as a single string. If you query a name that has
        /// multiple values, the values will be combined to a comma-separated-string using
        /// the ToCommaSeparatedString() extension method. If you query a name that is not 
        /// present in the dictionary, null will be returned.
        /// </summary>
        public string Get(string name)
        {
            return dictionary.ContainsKey(name) ? dictionary[name].ToCommaSeparatedString() : null;
        }

        /// <summary>
        /// Gets a list of all values associated with the given name.
        /// </summary>
        public IList<string> GetValues(string name)
        {
            return dictionary.ContainsKey(name) ? dictionary[name] : null;
        }

        /// <summary>
        /// Sets the value associated with the given name to the given string. The value will not
        /// be inspected in any way (for instance, you cannot use this method to set multiple values).
        /// This will replace out any existing values for the name.
        /// </summary>
        public void Set(string name, string value)
        {
            ThrowIfReadOnly();

            var list = new List<string>(1);
            list.Add(value);
            dictionary[name] = list;
        }

        /// <summary>
        /// Associates the given set of values with the given name. This will replace any existing
        /// values for the name. The values will be copied into a new list.
        /// </summary>
        public void Set(string name, IEnumerable<string> values)
        {
            ThrowIfReadOnly();
            dictionary[name] = values.ToList();
        }

        /// <summary>
        /// Gets or sets a name-value pair as a single string. See the Get and Set methods on this
        /// class for additional information about behavior.
        /// </summary>
        public string this[string name]
        {
            get { return Get(name); }
            set { Set(name, value); }
        }

        #endregion

        /// <summary>
        /// Returns true if this dictionary contains the given string.
        /// </summary>
        public bool Contains(string name)
        {
            return dictionary.ContainsKey(name);
        }

        void ThrowIfReadOnly()
        {
            if (IsReadOnly)
                throw new InvalidOperationException("You cannot modify this collection because it has been marked as read-only.");
        }

        #region ICollection<NameValuePair> Members

        /// <summary>
        /// Adds the given NameValuePair to this dictionary. If the name exists already, the values
        /// will be appended to the existing list of values.
        /// </summary>
        /// <param name="item"></param>
        public void Add(NameValuePair item)
        {
            ThrowIfReadOnly();
            AddRange(item.Name, item.Values);
        }

        /// <summary>
        /// Removes all elements from our dictionary.
        /// </summary>
        public void Clear()
        {
            ThrowIfReadOnly();
            dictionary.Clear();
        }

        bool ICollection<NameValuePair>.Contains(NameValuePair item)
        {
            return dictionary.ContainsKey(item.Name);
        }

        void ICollection<NameValuePair>.CopyTo(NameValuePair[] array, int arrayIndex)
        {
            foreach (NameValuePair pair in this)
                array[arrayIndex++] = pair;
        }

        /// <summary>
        /// Gets the number of name-value pairs in this dictionary.
        /// </summary>
        public int Count
        {
            get { return dictionary.Count; }
        }

        bool ICollection<NameValuePair>.Remove(NameValuePair item)
        {
            ThrowIfReadOnly();
            return dictionary.Remove(item.Name);
        }

        #endregion
    }

    public static partial class Extensions
    {
        public static string ToQueryString(this NameValueDictionary dict)
        {
            var sb = new StringBuilder();

            foreach (NameValuePair pair in dict)
            {
                foreach (string value in pair.Values)
                {
                    if (sb.Length > 0)
                        sb.Append(AmpersandChar);

                    sb.Append(Uri.EscapeDataString(pair.Name))
                        .Append(EqualsChar)
                        .Append(Uri.EscapeDataString(value));
                }
            }

            return sb.ToString();
        }

        public static string ToQueryString(this IDictionary dict)
        {
            var sb = new StringBuilder();

            foreach (var key in dict.Keys)
            {
                if (sb.Length > 0)
                    sb.Append(AmpersandChar);

                sb.Append(Uri.EscapeDataString(key.ToString()))
                    .Append(EqualsChar)
                    .Append(Uri.EscapeDataString(dict[key].ToString()));
            }

            return sb.ToString();
        }

        public static string ToCommaSeparatedString(this IList<string> list)
        {
            switch (list.Count)
            {
                case 0: return null;
                case 1: return list[0];
                default:
                    var sb = new StringBuilder();

                    foreach (string item in list)
                    {
                        if (sb.Length > 0)
                            sb.Append(',');

                        sb.Append(item);
                    }

                    return sb.ToString();
            }
        }
    }
}
