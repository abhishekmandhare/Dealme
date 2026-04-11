namespace DealMe.Core.Interfaces;

using DealMe.Core.DTOs;

public interface IProductSearchProvider
{
    string Name { get; }
    Task<IReadOnlyList<ProductSearchResult>> SearchAsync(string query, CancellationToken ct = default);
}
