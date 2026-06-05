namespace Application.Abstractions.Inventory;

public interface IReservationPolicy
{
    TimeSpan ReservationTimeout { get; }
}
