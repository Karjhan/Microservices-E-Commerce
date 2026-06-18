using Application.Abstractions.Enrichment;
using Application.Features.Documents.GenerateInvoice;
using Application.Features.Documents.GenerateReceipt;
using Application.Mapping;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Events.HandlePaymentCompleted;

public class HandlePaymentCompletedHandler : IRequestHandler<HandlePaymentCompletedCommand>
{
    private readonly IOrderEnrichmentClient _enrichment;
    private readonly PaymentEventToDocumentRequestMapper _mapper;
    private readonly IMediator _mediator;
    private readonly ILogger<HandlePaymentCompletedHandler> _logger;

    public HandlePaymentCompletedHandler(
        IOrderEnrichmentClient enrichment,
        PaymentEventToDocumentRequestMapper mapper,
        IMediator mediator,
        ILogger<HandlePaymentCompletedHandler> logger)
    {
        _enrichment = enrichment;
        _mapper = mapper;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(HandlePaymentCompletedCommand command, CancellationToken cancellationToken)
    {
        var payment = command.Payment;
        var order = await _enrichment.GetOrderAsync(payment.OrderId, cancellationToken);

        var invoiceRequest = _mapper.MapToInvoice(payment, order);
        try
        {
            await _mediator.Send(new GenerateInvoiceCommand(
                invoiceRequest,
                payment.UserId,
                command.CorrelationId), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invoice generation failed for payment {PaymentId}", payment.PaymentId);
        }

        var receiptRequest = _mapper.MapToReceipt(payment, order);
        try
        {
            await _mediator.Send(new GenerateReceiptCommand(
                receiptRequest,
                payment.UserId,
                command.CorrelationId), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Receipt generation failed for payment {PaymentId}", payment.PaymentId);
        }
    }
}
