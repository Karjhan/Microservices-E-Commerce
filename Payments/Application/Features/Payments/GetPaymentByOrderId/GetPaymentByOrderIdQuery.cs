using Application.DTOs;
using MediatR;

namespace Application.Features.Payments.GetPaymentByOrderId;

public record GetPaymentByOrderIdQuery(Guid OrderId) : IRequest<PaymentDto>;
