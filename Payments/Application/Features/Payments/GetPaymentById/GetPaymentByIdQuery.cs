using Application.DTOs;
using MediatR;

namespace Application.Features.Payments.GetPaymentById;

public record GetPaymentByIdQuery(Guid PaymentId) : IRequest<PaymentDto>;
