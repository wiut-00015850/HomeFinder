namespace HomeFinder.Models;

/// <summary>Лог просмотра карточки квартиры (для отчётов по периоду).</summary>
public class ApartmentViewLog
{
    public int Id { get; set; }
    public int ApartmentId { get; set; }
    public DateTime ViewedAt { get; set; }

    public virtual Apartment Apartment { get; set; } = null!;
}
