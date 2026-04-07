using HomeFinder.Context;
using HomeFinder.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeFinder.Controllers
{
    public class AdminController : Controller
    {
        private readonly HomeFinderContext _context;

        public AdminController(HomeFinderContext context)
        {
            _context = context;
        }

        // Проверка авторизации админа
        private bool IsAdminLoggedIn()
        {
            return HttpContext.Session.GetInt32("AdminId") != null &&
                   HttpContext.Session.GetString("UserRole") == "Admin";
        }

        // Панель админа
        public IActionResult Index()
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Account");

            var apartments = _context.Apartments
                .Include(a => a.User)
                .Include(a => a.Addresses)
                .Include(a => a.ReviewApartments)
                .Include(a => a.Photos)
                .ToList();

            var viewModels = apartments.Select(a => new ApartmentViewModel
            {
                ApartmentId = a.ApartmentId,
                Description = a.Description,
                StreetAddress = a.Addresses?.FirstOrDefault()?.StreetAddress,
                BuildingNumber = a.Addresses?.FirstOrDefault()?.BuildingNumber,
                ApartmentNumber = a.Addresses?.FirstOrDefault()?.ApartmentNumber,
                District = a.Addresses?.FirstOrDefault()?.District,
                City = a.Addresses?.FirstOrDefault()?.City,
                Region = a.Addresses?.FirstOrDefault()?.Region,
                LandlordName = $"{a.User?.FirstName} {a.User?.LastName}".Trim(),
                PhoneNumber = a.User?.PhoneNumber,
                PhotoPaths = a.Photos?.Select(p => p.PhotoPath).ToList() ?? new(),
                AverageRating = a.ReviewApartments.Any() ? a.ReviewApartments.Average(r => r.Rating ?? 0) : 0,
                ReviewCount = a.ReviewApartments.Count
            }).ToList();

            return View(viewModels);
        }

        // Удаление квартиры (только админ)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteApartment(int id)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Account");

            var apartment = _context.Apartments
                .Include(a => a.Addresses)
                    .ThenInclude(ad => ad.Appointments)
                .Include(a => a.Photos)
                .Include(a => a.ReviewApartments)
                .FirstOrDefault(a => a.ApartmentId == id);

            if (apartment == null)
                return NotFound();

            // 1️⃣ Delete appointments (MOST IMPORTANT)
            foreach (var address in apartment.Addresses)
            {
                _context.Appointments.RemoveRange(address.Appointments);
            }

            // 2️⃣ Delete related entities
            _context.Photos.RemoveRange(apartment.Photos);
            _context.ReviewApartments.RemoveRange(apartment.ReviewApartments);
            _context.Addresses.RemoveRange(apartment.Addresses);

            // 3️⃣ Delete apartment LAST
            _context.Apartments.Remove(apartment);

            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        // Все пользователи
        public IActionResult Users()
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Account");

            var users = _context.Users.ToList();
            return View(users);
        }

        // Удаление пользователя
        public IActionResult DeleteUser(int id)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Account");

            var user = _context.Users.FirstOrDefault(u => u.UserId == id);
            if (user == null)
                return NotFound();

            _context.Users.Remove(user);
            _context.SaveChanges();
            return RedirectToAction("Users");
        }
    }
}