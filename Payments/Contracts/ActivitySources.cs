using System.Diagnostics;

namespace Contracts;

public static class ActivitySources
{
    public static readonly ActivitySource Messaging =
        new("Payments.Messaging");
}
