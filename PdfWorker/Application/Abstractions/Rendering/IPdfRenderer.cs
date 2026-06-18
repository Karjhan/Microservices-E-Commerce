namespace Application.Abstractions.Rendering;

public interface IPdfRenderer
{
    Task<byte[]> RenderAsync(string templateVersion, object request, CancellationToken cancellationToken = default);
}
