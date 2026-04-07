using System.ComponentModel.DataAnnotations;
using HomeFinder.Models;

namespace HomeFinder.Models
{
    public class ApartmentViewModel
    {
        public int ApartmentId { get; set; }
        public int? UserId { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; }

        [Display(Name = "Price")]
        public decimal Price { get; set; }

        [Display(Name = "Size (m²)")]
        public int Size { get; set; }

        [Display(Name = "Number of Rooms")]
        public int Rooms { get; set; }

        [Display(Name = "Street")]
        public string StreetAddress { get; set; }

        [Display(Name = "Apartment Number")]
        public string BuildingNumber { get; set; }

        [Display(Name = "Phone Number")]
        public string ApartmentNumber { get; set; }

        [Display(Name = "District")]
        public string District { get; set; }

        [Display(Name = "City")]
        public string City { get; set; }

        [Display(Name = "Region")]
        public string Region { get; set; }

        [Display(Name = "Landlord")]
        public string LandlordName { get; set; }

        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Photo")]
        public List<string> PhotoPaths { get; set; } = new();

        [Display(Name = "Rating")]
        public double AverageRating { get; set; }

        [Display(Name = "Number of Reviews")]
        public int ReviewCount { get; set; }

        [Display(Name = "Number of Views")]
        public int? Views { get; set; }

        [Display(Name = "AllText")]
        public string AllText { get; set; }

        // ✅ КРИТИЧНО: Список ВСЕх отзывов для Details страницы
        public List<ReviewApartment> Reviews { get; set; } = new();

        public ReviewSummaryViewModel? ReviewSummary { get; set; }

        // ✅ Для загрузки фото при создании/редактировании
        [Display(Name = "Upload photos")]
        public List<IFormFile> Photos { get; set; } = new();

        // ➕ ДЛЯ КАРТЫ (добавляем только это):
        [Display(Name = "Latitude")]
        public decimal? Latitude { get; set; }

        [Display(Name = "Longitude")]
        public decimal? Longitude { get; set; }
    }


    // ➕ Новый ViewModel для записи на встречу
    public class AppointmentViewModel
    {
        public int AppointmentId { get; set; }
        public int ApartmentId { get; set; }
        public int AddressId { get; set; }
        public DateTime DateTime { get; set; }

        public List<Address> AvailableAddresses { get; set; } = new();
    }
}
