using System.Text;
using Names = Domain.Commons.NormalizedName;

namespace Domain.Entities;

public class FilamentType : ReferenceEntity
{
    public const int CodeMaxLength = 40;

    /// <summary>
    /// Stable identifier, unique. For the materials the store already sold it equals the old enum name (for example
    /// <c>PLA</c>), so storefront links and filters keep working (research R13).
    /// </summary>
    public string Code { get; private set; } = default!;

    public decimal? DiameterMm { get; private set; }

    public List<string> ColorOptions { get; private set; } = new();

    private FilamentType() { }

    public FilamentType(string name, string? code, decimal? diameterMm, IEnumerable<string>? colorOptions)
        : base(name)
    {
        Code = string.IsNullOrWhiteSpace(code) ? CodeFrom(name) : code.Trim();
        DiameterMm = diameterMm;
        ColorOptions = CleanColors(colorOptions);
    }

    /// <summary>Edits everything except the code, which stays stable once created.</summary>
    public void Update(string name, decimal? diameterMm, IEnumerable<string>? colorOptions)
    {
        Rename(name);
        DiameterMm = diameterMm;
        ColorOptions = CleanColors(colorOptions);
    }

    /// <summary>"Silk PLA" becomes "SilkPLA": letters, digits, dash and underscore only.</summary>
    public static string CodeFrom(string name)
    {
        var code = new StringBuilder();

        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_')
                code.Append(c);
        }

        return code.Length > CodeMaxLength ? code.ToString(0, CodeMaxLength) : code.ToString();
    }

    private static List<string> CleanColors(IEnumerable<string>? colors) =>
        (colors ?? [])
            .Select(Names.Clean)
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
