using Application.Abstractions.Persistence;
using Application.DTOs;
using MediatR;

namespace Application.Features.Payments.GetPayments;

public record GetPaymentsQuery(PaymentFilter Filter) : IRequest<List<PaymentDto>>;
