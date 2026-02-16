using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AspireResourceServer.Services
{
    public static class DictionaryExtension
    {
        public static TValue TryGetValue<TKey, TValue>(this Dictionary<TKey, TValue> self, TKey key)
        {
            if (self.TryGetValue(key, out var value)) return value;
            return default;
        }

        public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> self, TKey key, Func<TKey, TValue> factory)
        {
            if (!self.TryGetValue(key, out var value))
            {
                value = factory(key);
                self.Add(key, value);
            }
            return value;
        }
        public static async Task<TValue> GetOrAddAsync<TKey, TValue>(this Dictionary<TKey, TValue> self, TKey key, Func<TKey, Task<TValue>> factory)
        {
            if (!self.TryGetValue(key, out var value))
            {
                value = await factory(key);
                self.Add(key, value);
            }
            return value;
        }

    }
}