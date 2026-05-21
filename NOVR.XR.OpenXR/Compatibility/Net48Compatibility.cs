using System;
using System.Collections.Generic;

namespace System
{
    internal static class HashCode
    {
        public static int Combine<T1, T2>(T1 value1, T2 value2)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + (value1?.GetHashCode() ?? 0);
                hash = (hash * 31) + (value2?.GetHashCode() ?? 0);
                return hash;
            }
        }

        public static int Combine<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
        {
            unchecked
            {
                var hash = Combine(value1, value2);
                hash = (hash * 31) + (value3?.GetHashCode() ?? 0);
                return hash;
            }
        }

        public static int Combine<T1, T2, T3, T4>(T1 value1, T2 value2, T3 value3, T4 value4)
        {
            unchecked
            {
                var hash = Combine(value1, value2, value3);
                hash = (hash * 31) + (value4?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}

namespace System.Collections.Generic
{
    internal static class DictionaryCompatibilityExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        {
            if (dictionary == null)
            {
                return default;
            }

            return dictionary.TryGetValue(key, out var value) ? value : default;
        }
    }
}
