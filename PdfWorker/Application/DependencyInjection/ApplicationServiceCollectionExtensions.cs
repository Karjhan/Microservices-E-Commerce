using Application.Features.Documents;
using Application.Features.Documents.GenerateCreditNote;
using Application.Features.Documents.GenerateInvoice;
using Application.Features.Documents.GenerateReceipt;
using Application.Mapping;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ApplicationServiceCollectionExtensions).Assembly);
        });

        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(Common.LoggingBehavior<,>));

        services.AddValidatorsFromAssemblyContaining<InvoiceDocumentRequestValidator>();
        services.AddScoped<InvoiceDocumentRequestValidator>();
        services.AddScoped<ReceiptDocumentRequestValidator>();
        services.AddScoped<CreditNoteDocumentRequestValidator>();
        services.AddScoped<PaymentEventToDocumentRequestMapper>();
        services.AddScoped<IGenerateDocumentPipeline, GenerateDocumentPipeline>();

        return services;
    }
}
