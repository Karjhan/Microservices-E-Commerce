using Application.Abstractions.Enrichment;
using Application.Features.Documents.GenerateCreditNote;
using Application.Mapping;
using MediatR;

namespace Application.Features.Events.HandlePaymentRefunded;

public class HandlePaymentRefundedHandler : IRequestHandler<HandlePaymentRefundedCommand>
{
    private readonly IOrderEnrichmentClient _enrichment;
    private readonly PaymentEventToDocumentRequestMapper _mapper;
    private readonly IMediator _mediator;

    public HandlePaymentRefundedHandler(
        IOrderEnrichmentClient enrichment,
        PaymentEventToDocumentRequestMapper mapper,
        IMediator mediator)
    {
        _enrichment = enrichment;
        _mapper = mapper;
        _mediator = mediator;
    }

    public async Task Handle(HandlePaymentRefundedCommand command, CancellationToken cancellationToken)
    {
        var payment = command.Payment;
        var order = payment.IsFullRefund
            ? null
            : await _enrichment.GetOrderAsync(payment.OrderId, cancellationToken);

        var creditNoteRequest = _mapper.MapToCreditNote(payment, order);

        await _mediator.Send(new GenerateCreditNoteCommand(
            creditNoteRequest,
            payment.UserId,
            command.CorrelationId), cancellationToken);
    }
}
