namespace CSharpLatest.Events;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

/// <summary>
/// Represents a collection of delegates, implemented to store them as weak references.
/// All methods are thread-safe.
/// </summary>
/// <typeparam name="TItem"></typeparam>
internal sealed class WeakCollection<TItem>
    where TItem : Delegate
{
    /// <summary>
    /// The list of delegates. This is enough to run the algorithm, but not to keep them in memory.
    /// </summary>
    private readonly List<WeakReference<TItem>> weakReferences = [];

    /// <summary>
    /// A store that keeps references to delegate targets.
    /// This makes sure that when the target disappears from memory, delegates can go away, but not before.
    /// </summary>
    private readonly ConditionalWeakTable<object, List<TItem>> _store = new();

    /// <summary>
    /// A target for delegates without target (handler.Target is null for static handlers).
    /// This key never goes away, but static event handlers stay when instances are disposed anyway.
    /// </summary>
    private readonly object _allStatic = new();

    /// <inheritdoc cref="List{TItem}.Count" />
    public int Count => weakReferences.Count;

    /// <summary>
    /// Attempts to add the specified item to the collection if it is not already present.
    /// </summary>
    /// <param name="handler">The handler to add to the collection. Cannot be null.</param>
    public void TryAdd(TItem handler)
    {
        WeakReference<TItem> weakReference = new(handler);

        lock (weakReferences)
        {
            foreach (WeakReference<TItem> item in weakReferences)
                if (item.TryGetTarget(out TItem? target))
                    if (target.Equals(handler))
                        return;

            weakReferences.Add(weakReference);

            object handlerTarget = handler.Target ?? _allStatic;
            List<TItem> targetHandlers = _store.GetOrCreateValue(handlerTarget);
            targetHandlers.Add(handler);
        }
    }

    /// <summary>
    /// Attempts to remove the specified item from the collection. Does nothing if not present.
    /// </summary>
    /// <param name="handler"></param>
    public void TryRemove(TItem handler)
    {
        lock (weakReferences)
        {
            for (int i = 0; i < weakReferences.Count; i++)
                if (weakReferences[i].TryGetTarget(out TItem? target))
                    if (target.Equals(handler))
                    {
                        weakReferences.RemoveAt(i);

                        object handlerTarget = handler.Target ?? _allStatic;
                        bool isFound = _store.TryGetValue(handlerTarget, out List<TItem> targetHandlers);
                        Debug.Assert(isFound, "Since the target exists, the corresponding key also exists");
                        bool isRemoved = targetHandlers.Remove(handler);
                        Debug.Assert(isRemoved, "Since the hander was found in weakReferences (and removed), it has to exist in this list of handlers as well");

                        return;
                    }
        }
    }

    /// <summary>
    /// Performs the specified action on each element of the collection.
    /// </summary>
    /// <param name="action">The action to perform on each element of the collection.</param>
    /// <returns><see langword="true"/> if the collection contains items that have been collection and deserves a cleanup; Otherwise, <see langword="false"/>.</returns>
    public bool ForEach(Action<TItem> action)
    {
        List<WeakReference<TItem>> listCopy;

        lock (weakReferences)
        {
            listCopy = [.. weakReferences];
        }

        bool isCleanupNeeded = false;

        listCopy.ForEach((weakReference) =>
        {
            if (weakReference.TryGetTarget(out TItem? item))
                action(item);
            else
                isCleanupNeeded = true;
        });

        return isCleanupNeeded;
    }

    /// <summary>
    /// Removes items that have been collected by the garbage collector.
    /// </summary>
    public void Cleanup()
    {
        List<WeakReference<TItem>> toRemove = [];

        lock (weakReferences)
        {
            foreach (WeakReference<TItem> item in weakReferences)
            {
                if (!item.TryGetTarget(out _))
                    toRemove.Add(item);
            }

            // Handlers for targets that gone are explicitely removed from the list here.
            // They have also been implicitely removed from the store.
            foreach (WeakReference<TItem> item in toRemove)
                _ = weakReferences.Remove(item);
        }
    }
}
