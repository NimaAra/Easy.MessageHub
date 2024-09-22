namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit;
}

namespace Easy.MessageHub
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
    internal sealed class Subscriptions
    {
        private readonly ResizableMemory AllSubscriptions = new();

        public int Count
        {
            get
            {
                lock (AllSubscriptions)
                {
                    return AllSubscriptions.Count;
                }
            }
        }

        public Guid Register<T>(TimeSpan throttleBy, Action<T> action)
        {
            Type type = typeof(T);
            Guid key = Guid.NewGuid();
            Subscription subscription = new(type, key, throttleBy, action);

            lock (AllSubscriptions)
            {
                AllSubscriptions.Add(subscription);
            }

            return key;
        }

        public bool IsRegistered(Guid token)
        {
            lock (AllSubscriptions) { return AllSubscriptions.Contains(token); }
        }

        public int GetTheLatestSubscriptions(Span<Subscription> buffer)
        {
            lock (AllSubscriptions)
            {
                return AllSubscriptions.CopyTo(buffer);
            }
        }

        public void UnRegister(Guid token)
        {
            lock (AllSubscriptions)
            {
                AllSubscriptions.Remove(token);
            }
        }

        public void Clear()
        {
            lock (AllSubscriptions)
            {
                AllSubscriptions.Clear();
            }
        }
    }
}