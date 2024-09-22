namespace Easy.MessageHub;

using System;

internal sealed class ResizableMemory
{
    private int count;
    private Subscription[] memory;

    public ResizableMemory(int initialCapacity = 100) => memory = new Subscription[initialCapacity];

    public int Count => count;

    public void Add(Subscription item)
    {
        if (count == memory.Length)
        {
            Resize();
        }

        memory[count] = item;
        count++;
    }

    public bool Remove(Guid token)
    {
        for (int i = 0; i < count; i++)
        {
            if (memory[i].Token == token)
            {
                for (int j = i; j < count - 1; j++)
                {
                    memory[j] = memory[j + 1];
                }

                count--;
                memory[count] = default;
                return true;
            }
        }

        return false;
    }

    public void Clear() => count = 0;

    public bool Contains(Guid token)
    {
        for (int i = 0; i < count; i++)
        {
            if (memory[i].Token == token)
            {
                return true;
            }
        }

        return false;
    }

    private void Resize()
    {
        int newSize = memory.Length * 2;
        Subscription[] newMemory = new Subscription[newSize];
        memory.CopyTo(newMemory, 0);
        memory = newMemory;
    }

    public int CopyTo(Span<Subscription> destination)
    {
        if (destination.Length < count)
        {
            throw new ArgumentException("The destination span is too small to hold the items.");
        }

        memory.AsSpan(0, count).CopyTo(destination);
        return count;
    }
}