using System.Diagnostics;

namespace Contracts;

public static class ActivitySources
{
    public static readonly ActivitySource Messaging =
        new("Catalog.Messaging");

    public static readonly ActivitySource Storage =
        new("Catalog.Storage");
}