using System.Diagnostics;

namespace PdfWorker.Contracts;

public static class ActivitySources
{
    public static readonly ActivitySource Messaging = new("PdfWorker.Messaging");
    public static readonly ActivitySource Storage = new("PdfWorker.Storage");
    public static readonly ActivitySource Rendering = new("PdfWorker.Rendering");
}
