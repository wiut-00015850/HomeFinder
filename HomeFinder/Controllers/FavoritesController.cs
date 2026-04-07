using HomeFinder.Context;
using HomeFinder.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeFinder.Controllers
{
    public class FavoritesController : Controller
    {
        private readonly HomeFinderContext _context;

        public FavoritesController(HomeFinderContext context)
        {
            _context = context;
        }

        // ✅ Проверка авторизации
        private bool IsTenantLoggedIn()
        {
            return HttpContext.Session.GetInt32("UserId") != null &&
                   HttpContext.Session.GetString("UserRole") == "Tenant";
        }

        // ✅ Добавить/Удалить из избранного (TOGGLE)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddToFavorites(int apartmentId)
        {
            if (!IsTenantLoggedIn())
                return RedirectToAction("Login", "Account");

            int userId = HttpContext.Session.GetInt32("UserId").Value;

            var exists = _context.Favorites
                .FirstOrDefault(f => f.UserId == userId && f.ApartmentId == apartmentId);

            if (exists == null)
            {
                // ✅ Добавляем
                _context.Favorites.Add(new Favorite
                {
                    UserId = userId,
                    ApartmentId = apartmentId
                });
            }
            else
            {
                // ✅ Удаляем (toggle)
                _context.Favorites.Remove(exists);
            }

            _context.SaveChanges();

            return RedirectToAction("Details", "Home", new { id = apartmentId });
        }

        // ✅ Удалить из избранного
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveFromFavorites(int apartmentId)
        {
            if (!IsTenantLoggedIn())
                return RedirectToAction("Login", "Account");

            int userId = HttpContext.Session.GetInt32("UserId").Value;

            var favorite = _context.Favorites
                .FirstOrDefault(f => f.UserId == userId && f.ApartmentId == apartmentId);

            if (favorite != null)
            {
                _context.Favorites.Remove(favorite);
                _context.SaveChanges();
            }

            return RedirectToAction("MyFavorites");
        }

        // ✅ Список моих избранных
        public IActionResult MyFavorites()
        {
            if (!IsTenantLoggedIn())
                return RedirectToAction("Login", "Account");

            int userId = HttpContext.Session.GetInt32("UserId").Value;

            var favorites = _context.Favorites
                .Where(f => f.UserId == userId)
                .Include(f => f.Apartment)
                    .ThenInclude(a => a.User)
                .Include(f => f.Apartment)
                    .ThenInclude(a => a.Addresses)
                .Include(f => f.Apartment)
                    .ThenInclude(a => a.Photos)
                .Include(f => f.Apartment)
                    .ThenInclude(a => a.ReviewApartments)
                .ToList();

            var viewModels = favorites.Select(f => new ApartmentViewModel
            {
                ApartmentId = f.Apartment.ApartmentId,
                Description = f.Apartment.Description,
                Price = f.Apartment.Price ?? 0,
                Size = f.Apartment.Size ?? 1,
                Rooms = f.Apartment.Rooms ?? 1,
                StreetAddress = f.Apartment.Addresses?.FirstOrDefault()?.StreetAddress,
                BuildingNumber = f.Apartment.Addresses?.FirstOrDefault()?.BuildingNumber,
                ApartmentNumber = f.Apartment.Addresses?.FirstOrDefault()?.ApartmentNumber,
                District = f.Apartment.Addresses?.FirstOrDefault()?.District,
                City = f.Apartment.Addresses?.FirstOrDefault()?.City,
                Region = f.Apartment.Addresses?.FirstOrDefault()?.Region,
                LandlordName = $"{f.Apartment.User?.FirstName} {f.Apartment.User?.LastName}".Trim(),
                PhoneNumber = f.Apartment.User?.PhoneNumber,
                PhotoPaths = f.Apartment.Photos?.Select(p => p.PhotoPath).ToList() ?? new(),
                AverageRating = f.Apartment.ReviewApartments.Any() ? f.Apartment.ReviewApartments.Average(r => r.Rating ?? 0) : 0,
                ReviewCount = f.Apartment.ReviewApartments.Count
            }).ToList();

            return View(viewModels);
        }

        // ✅ Проверить статус избранного (JSON)
        [HttpGet]
        public IActionResult IsFavorited(int apartmentId)
        {
            if (!IsTenantLoggedIn())
                return Json(new { favorited = false });

            int userId = HttpContext.Session.GetInt32("UserId").Value;

            var exists = _context.Favorites
                .Any(f => f.UserId == userId && f.ApartmentId == apartmentId);

            return Json(new { favorited = exists });
        }
    }
}