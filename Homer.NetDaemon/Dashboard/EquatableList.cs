using System.Collections;
using System.Runtime.CompilerServices;

namespace Homer.NetDaemon.Dashboard;

/// <summary>
/// Read-only list with value equality, so dashboard records compare by content and an unchanged snapshot is not
/// republished (and re-rendered) just because it was rebuilt.
/// </summary>
[CollectionBuilder(typeof(EquatableList), nameof(EquatableList.Create))]
public sealed class EquatableList<T> : IReadOnlyList<T>, IEquatable<EquatableList<T>>
{
    public static readonly EquatableList<T> Empty = new([]);

    private readonly T[] _items;

    public EquatableList(IEnumerable<T> items)
    {
        _items = items.ToArray();
    }

    public int Count => _items.Length;

    public T this[int index] => _items[index];

    public bool Equals(EquatableList<T>? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null || other._items.Length != _items.Length) return false;

        var comparer = EqualityComparer<T>.Default;
        for (var i = 0; i < _items.Length; i++)
        {
            if (!comparer.Equals(_items[i], other._items[i])) return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableList<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in _items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}

public static class EquatableList
{
    public static EquatableList<T> Create<T>(ReadOnlySpan<T> items) => new(items.ToArray());

    public static EquatableList<T> ToEquatableList<T>(this IEnumerable<T> items) => new(items);
}
