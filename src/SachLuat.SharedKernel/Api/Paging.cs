using FluentValidation;

namespace SachLuat.SharedKernel.Api;

/// <summary>Tham số phân trang <c>?page=1&amp;limit=24</c> (mục 11.1). Query có lọc thì kế thừa class này.</summary>
public class PagedQuery
{
    public const int DefaultLimit = 24;
    public const int MaxLimit = 100;

    public int Page { get; set; } = 1;

    public int Limit { get; set; } = DefaultLimit;

    public int Skip => (Page - 1) * Limit;
}

public class PagedQueryValidator<T> : AbstractValidator<T>
    where T : PagedQuery
{
    public PagedQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.Limit).InclusiveBetween(1, PagedQuery.MaxLimit);
    }
}

public sealed record PageMeta(int Page, int Limit, int Total);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, PageMeta Meta)
{
    public static PagedResponse<T> Create(IReadOnlyList<T> items, PagedQuery query, int total) =>
        new(items, new PageMeta(query.Page, query.Limit, total));
}
