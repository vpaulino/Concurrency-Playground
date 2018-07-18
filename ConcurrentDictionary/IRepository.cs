using System;
using System.Collections.Concurrent;

namespace ConcurrentDictionary
{
    public interface IRepository
    {
        ConcurrentBag<TimeSpan> ExecutionTimes { get; set; }

        string Type { get; set; }

        void Add(DisposableObject instance);
        void Clear();
        void Remove(string id);
    }
}