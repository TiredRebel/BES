using System.Collections;

namespace TaskManagement.Application;

/// <summary>
/// Represents a paginated page of items with an optional keyset cursor for the next page.
/// </summary>
/// <typeparam name="T">The type of elements on the page.</typeparam>
public sealed class PagedResult<T> : IReadOnlyList<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PagedResult{T}"/> class.
    /// </summary>
    /// <param name="items">The items on the current page.</param>
    /// <param name="nextCursor">The cursor to fetch the next page, or <see langword="null"/> if there are no more items.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    public PagedResult(IReadOnlyList<T> items, TaskCursor? nextCursor)
    {
        ArgumentNullException.ThrowIfNull(items);

        Items = items;
        NextCursor = nextCursor;
    }

    /// <summary>
    /// Gets the collection of items on this page.
    /// </summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>
    /// Gets the keyset cursor for fetching the next page, or <see langword="null"/> if this is the last page.
    /// </summary>
    public TaskCursor? NextCursor { get; }

    /// <summary>
    /// Gets a value indicating whether another page of items exists after this page.
    /// </summary>
    public bool HasNextPage => NextCursor is not null;

    /// <inheritdoc />
    public int Count => Items.Count;

    /// <inheritdoc />
    public T this[int index] => Items[index];

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => Items.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
