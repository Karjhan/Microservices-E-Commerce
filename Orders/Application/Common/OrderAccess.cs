using Domain.Exceptions;
using Shared.Security.Authorization;

namespace Application.Common;

/// <summary>Owner-or-admin rule for order operations (contracts/admin-api.md, owner rows).</summary>
public static class OrderAccess
{
    /// <summary>
    /// Admins may act on any order; everyone else only on their own. A non-owner receives the same
    /// "not found" result as a missing order so existence is not revealed.
    /// </summary>
    public static void EnsureOwnerOrAdmin(ICurrentUser user, Guid ownerId, Guid orderId)
    {
        if (user.IsAdmin)
        {
            return;
        }

        if (user.IsAuthenticated && user.Id == ownerId)
        {
            return;
        }

        throw new OrderNotFoundException(orderId);
    }
}