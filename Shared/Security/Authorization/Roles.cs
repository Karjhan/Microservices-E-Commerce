namespace Shared.Security.Authorization;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";

    public static string NormalizeRole(string? stored) =>
        string.Equals(stored, Admin, StringComparison.Ordinal) ? Admin : Customer;
}
