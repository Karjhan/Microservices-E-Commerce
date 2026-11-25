using System.Text.Json;
using Shared.Security.Authorization;

namespace Shared.Security.Audit;

public static class AuditEntryBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static AuditEntry From(
        IAuditedRequest request,
        ICurrentUser user,
        string outcome,
        object? response = null,
        object? extraDetails = null)
    {
        var targetId = request.AuditTargetId ?? (response as Guid?)?.ToString();

        var details = new Dictionary<string, object?>();
        MergeInto(details, request.AuditDetails);
        MergeInto(details, extraDetails);

        return AuditEntry.Create(
            request.AuditAction,
            request.AuditTargetType,
            targetId,
            outcome,
            user.Id,
            user.Email,
            JsonSerializer.Serialize(details, JsonOptions),
            user.CorrelationId);
    }

    private static void MergeInto(Dictionary<string, object?> target, object? source)
    {
        if (source is null)
        {
            return;
        }

        var element = JsonSerializer.SerializeToElement(source, JsonOptions);
        if (element.ValueKind != JsonValueKind.Object)
        {
            target["value"] = element;
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            target[property.Name] = property.Value.Clone();
        }
    }
}
