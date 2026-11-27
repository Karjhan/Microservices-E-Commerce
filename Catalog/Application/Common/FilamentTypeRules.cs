using Domain.Entities;
using Domain.Exceptions;
using Infrastructure.Abstractions.Persistence;

namespace Application.Common;

public static class FilamentTypeRules
{
    public const decimal MaxDiameterMm = 10m;
    public const int MaxColorOptions = 50;
    public const int MaxColorLength = 40;

    /// <summary>Checks the fields that are specific to filament types and returns the cleaned colors.</summary>
    public static IReadOnlyList<string> ValidateDetails(decimal? diameterMm, IEnumerable<string>? colorOptions)
    {
        var errors = new Dictionary<string, string[]>();

        if (diameterMm is <= 0 or > MaxDiameterMm)
            errors["diameterMm"] = [$"Diameter must be greater than 0 and at most {MaxDiameterMm} mm."];
        else if (diameterMm.HasValue && decimal.Round(diameterMm.Value, 2) != diameterMm.Value)
            errors["diameterMm"] = ["Diameter can have at most two decimal places."];

        var colors = (colorOptions ?? [])
            .Select(Domain.Commons.NormalizedName.Clean)
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (colors.Count > MaxColorOptions)
            errors["colorOptions"] = [$"At most {MaxColorOptions} colors are allowed."];
        else if (colors.Any(c => c.Length > MaxColorLength))
            errors["colorOptions"] = [$"A color can be at most {MaxColorLength} characters."];

        if (errors.Count > 0)
            throw new ValidationFailedException(errors);

        return colors;
    }

    /// <summary>Uses the given code, or derives one from the name, and makes sure it is valid and unused.</summary>
    public static async Task<string> ResolveCodeAsync(
        IFilamentTypeRepository repo,
        string? requestedCode,
        string name,
        CancellationToken ct)
    {
        var code = string.IsNullOrWhiteSpace(requestedCode) ? FilamentType.CodeFrom(name) : requestedCode.Trim();

        if (code.Length == 0 || code.Length > FilamentType.CodeMaxLength
            || !code.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
        {
            throw new ValidationFailedException(
                "code",
                $"Code must be 1 to {FilamentType.CodeMaxLength} letters, digits, dashes or underscores.");
        }

        if (await repo.CodeExistsAsync(code, null, ct))
            throw new ConflictException("duplicate_code", $"The code '{code}' is already used.", "code", "Code must be unique.");

        return code;
    }
}
