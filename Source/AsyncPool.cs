﻿namespace AICore;

using System.Collections.Concurrent;

public static class AsyncPool<T>
    where T : new()
{
    private static readonly ConcurrentBag<T> returnItems = new ConcurrentBag<T>();

    public static int Count => returnItems.Count;

    public static T Get()
    {
        if (returnItems.TryTake(out T item))
        {
            return item;
        }
        return Activator.CreateInstance<T>();
    }

    public static void Return(T item)
    {
        returnItems.Add(item);
    }
}
