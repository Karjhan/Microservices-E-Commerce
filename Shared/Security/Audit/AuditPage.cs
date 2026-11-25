namespace Shared.Security.Audit;

public sealed record AuditPage(IReadOnlyList<AuditEntry> Items, int Page, int PageSize, int TotalCount);

public static class AuditPaging
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) Normalize(int? page, int? pageSize)
    {
        var p = page is null or < 1 ? 1 : page.Value;
        var s = pageSize is null or < 1 ? DefaultPageSize : Math.Min(pageSize.Value, MaxPageSize);
        return (p, s);
    }
}
