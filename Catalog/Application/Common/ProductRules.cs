using Domain.Exceptions;
using Infrastructure.Abstractions.Persistence;

namespace Application.Common;

public static class ProductRules
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 120;
    public const string Currency = "EUR";

    public static async Task ValidateAsync(
        IProductRepository repo,
        Guid? productId,
        string? name,
        decimal price,
        string? currency,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();

        var trimmed = name?.Trim() ?? string.Empty;

        if (trimmed.Length < NameMinLength || trimmed.Length > NameMaxLength)
        {
            errors["name"] = [$"Name must be {NameMinLength} to {NameMaxLength} characters."];
        }
        else if (await repo.NameExistsAsync(trimmed, productId, ct))
        {
            errors["name"] = ["A product with this name already exists."];
        }

        if (price < 0)
            errors["price"] = ["Price cannot be negative."];
        else if (decimal.Round(price, 2) != price)
            errors["price"] = ["Price can have at most two decimal places."];

        if (!string.IsNullOrWhiteSpace(currency)
            && !string.Equals(currency.Trim(), Currency, StringComparison.OrdinalIgnoreCase))
        {
            errors["currency"] = [$"Only {Currency} is supported."];
        }

        if (errors.Count > 0)
            throw new ValidationFailedException(errors);
    }

    public static string NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? Currency : currency.Trim().ToUpperInvariant();
}
