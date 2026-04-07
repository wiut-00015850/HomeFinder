using HomeFinder.Context;
using HomeFinder.Models;
using HomeFinder.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;

namespace HomeFinder.Controllers
{
    public class ApartmentsController : Controller
    {
        private readonly HomeFinderContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IAiReviewSummaryService _aiReviewSummaryService;

        public ApartmentsController(
            HomeFinderContext context,
            IWebHostEnvironment env,
            IAiReviewSummaryService aiReviewSummaryService)
        {
            _context = context;
            _env = env;
            _aiReviewSummaryService = aiReviewSummaryService;
        }

        // Проверка авторизации владельца
        private bool IsLandlordLoggedIn()
        {
            return HttpContext.Session.GetInt32("UserId") != null &&
                   HttpContext.Session.GetString("UserRole") == "Landlord";
        }

        private static string? NormalizePhotoPath(string? rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
                return rawPath;

            var path = rawPath.Trim().Replace('\\', '/');
            var lower = path.ToLowerInvariant();

            if (lower.StartsWith("http://") || lower.StartsWith("https://"))
                return path;

            var wwwrootIdx = lower.IndexOf("wwwroot/", StringComparison.Ordinal);
            if (wwwrootIdx >= 0)
                path = path[(wwwrootIdx + "wwwroot/".Length)..];

            if (path.StartsWith("photos/", StringComparison.OrdinalIgnoreCase))
                path = "/" + path;

            if (!path.StartsWith("/", StringComparison.Ordinal))
                path = "/" + path;

            return path;
        }

        private bool IsPhotoAvailable(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return true;

            var normalized = NormalizePhotoPath(path) ?? string.Empty;
            if (normalized.StartsWith("/user-photos/", StringComparison.OrdinalIgnoreCase))
            {
                var fileName = Path.GetFileName(normalized);
                var fullPath = Path.Combine(GetUploadStoragePath(), fileName);
                return System.IO.File.Exists(fullPath);
            }

            var fullLocalPath = Path.Combine(_env.WebRootPath, normalized.TrimStart('/'));
            return System.IO.File.Exists(fullLocalPath);
        }

        private string GetUploadStoragePath()
        {
            // На проде wwwroot часто read-only. App_Data обычно безопаснее для записи.
            var path = Path.Combine(_env.ContentRootPath, "App_Data", "uploads", "photos");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }

        private string SavePhotoAndGetPublicPath(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = Guid.NewGuid() + ext;
            var storagePath = GetUploadStoragePath();
            var fullPath = Path.Combine(storagePath, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                file.CopyTo(stream);
            }

            // Отдаем через action, не зависим от StaticFiles/wwwroot.
            return "/user-photos/" + fileName;
        }

        [HttpGet("/user-photos/{fileName}")]
        public IActionResult UserPhoto(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return NotFound();

            fileName = Path.GetFileName(fileName);
            var fullPath = Path.Combine(GetUploadStoragePath(), fileName);
            if (!System.IO.File.Exists(fullPath))
                return NotFound();

            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(fileName, out var contentType))
                contentType = "application/octet-stream";

            return PhysicalFile(fullPath, contentType);
        }

        // Мои квартиры
        public async Task<IActionResult> MyApartments()
        {
            if (!IsLandlordLoggedIn())
                return RedirectToAction("Login", "Account");

            int userId = HttpContext.Session.GetInt32("UserId").Value;
            var apartments = _context.Apartments
                .Where(a => a.UserId == userId)
                .Include(a => a.Addresses)
                .Include(a => a.Photos)
                .Include(a => a.ReviewApartments)
                .ToList();

            var viewModels = apartments.Select(a => new ApartmentViewModel
            {
                ApartmentId = a.ApartmentId,
                UserId = a.UserId,
                Description = a.Description,
                Price = a.Price ?? 0,
                Size = a.Size ?? 0,
                Rooms = a.Rooms ?? 0,
                StreetAddress = a.Addresses?.FirstOrDefault()?.StreetAddress,
                BuildingNumber = a.Addresses?.FirstOrDefault()?.BuildingNumber,
                ApartmentNumber = a.Addresses?.FirstOrDefault()?.ApartmentNumber,
                District = a.Addresses?.FirstOrDefault()?.District,
                City = a.Addresses?.FirstOrDefault()?.City,
                Region = a.Addresses?.FirstOrDefault()?.Region,
                Latitude = a.Addresses?.FirstOrDefault()?.Latitude,        // ✅ Координаты
                Longitude = a.Addresses?.FirstOrDefault()?.Longitude,     // ✅ Координаты
                PhotoPaths = a.Photos?.Select(p => p.PhotoPath).ToList() ?? new(),
                AverageRating = a.ReviewApartments.Any() ? a.ReviewApartments.Average(r => r.Rating ?? 0) : 0,
                ReviewCount = a.ReviewApartments.Count
            }).ToList();

            // Чистим "битые" локальные фото у уже существующих квартир этого владельца.
            bool hasRemovedBrokenPhotos = false;
            foreach (var apt in apartments)
            {
                var broken = apt.Photos?
                    .Where(p =>
                    {
                        var normalized = NormalizePhotoPath(p.PhotoPath);
                        if (string.IsNullOrWhiteSpace(normalized))
                            return true;
                        if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                            return false;
                        return !IsPhotoAvailable(normalized);
                    })
                    .ToList() ?? new List<Photo>();

                if (broken.Count > 0)
                {
                    _context.Photos.RemoveRange(broken);
                    hasRemovedBrokenPhotos = true;
                }
            }
            if (hasRemovedBrokenPhotos)
            {
                await _context.SaveChangesAsync();
            }

            foreach (var vm in viewModels)
            {
                vm.PhotoPaths = vm.PhotoPaths
                    .Select(NormalizePhotoPath)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Where(IsPhotoAvailable)
                    .Cast<string>()
                    .ToList();
            }
            var canAdd = await CanAddApartment(userId);
            ViewBag.CanAddApartment = canAdd;

            return View(viewModels);
        }

        private const int MaxReviewsOnDetailsPage = 50;

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var apartment = await _context.Apartments
                .Include(a => a.Addresses)
                .Include(a => a.Photos)
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.ApartmentId == id);

            if (apartment == null)
                return NotFound();

            _context.ApartmentViewLogs.Add(new ApartmentViewLog
            {
                ApartmentId = apartment.ApartmentId,
                ViewedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();

            var address = apartment.Addresses.FirstOrDefault();
            var totalViews = await _context.ApartmentViewLogs
                .AsNoTracking()
                .CountAsync(v => v.ApartmentId == id);

            var reviewCount = await _context.ReviewApartments
                .AsNoTracking()
                .CountAsync(r => r.ApartmentId == id);

            var reviewsPage = await _context.ReviewApartments
                .AsNoTracking()
                .Where(r => r.ApartmentId == id)
                .OrderByDescending(r => r.CreatedAt ?? DateTime.MinValue)
                .Take(MaxReviewsOnDetailsPage)
                .Include(r => r.User)
                .ToListAsync();

            double averageRating = 0;
            if (reviewCount > 0)
            {
                averageRating = await _context.ReviewApartments
                    .AsNoTracking()
                    .Where(r => r.ApartmentId == id)
                    .AverageAsync(r => (double)(r.Rating ?? 0));
            }

            var model = new ApartmentViewModel
            {
                ApartmentId = apartment.ApartmentId,
                Description = apartment.Description,
                Price = apartment.Price ?? 0,
                Size = apartment.Size ?? 0,
                Rooms = apartment.Rooms ?? 0,
                Views = totalViews,

                StreetAddress = address?.StreetAddress,
                BuildingNumber = address?.BuildingNumber,
                ApartmentNumber = address?.ApartmentNumber,
                District = address?.District,
                City = address?.City,
                Region = address?.Region,

                Latitude = address?.Latitude,
                Longitude = address?.Longitude,

                PhotoPaths = apartment.Photos
                    .Select(p => p.PhotoPath)
                    .ToList(),

                LandlordName = apartment.User != null
                    ? apartment.User.FirstName + " " + apartment.User.LastName
                    : "Unknown",

                PhoneNumber = apartment.User?.PhoneNumber,

                AverageRating = averageRating,

                ReviewCount = reviewCount,

                Reviews = reviewsPage
            };

            model.PhotoPaths = model.PhotoPaths
                .Select(NormalizePhotoPath)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Where(IsPhotoAvailable)
                .Cast<string>()
                .ToList();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetReviewSummary(int id, [FromQuery] bool refresh = false, CancellationToken cancellationToken = default)
        {
            // Один round-trip к БД: квартира + все отзывы с полными текстами
            var apartment = await _context.Apartments
                .AsNoTracking()
                .Include(a => a.ReviewApartments)
                .FirstOrDefaultAsync(a => a.ApartmentId == id, cancellationToken);

            if (apartment == null)
                return NotFound();

            var reviews = apartment.ReviewApartments
                .OrderByDescending(r => r.CreatedAt ?? DateTime.MinValue)
                .ToList();

            var result = await _aiReviewSummaryService.GetSummaryAsync(
                id,
                reviews,
                refresh,
                cancellationToken);

            return Json(result);
        }


        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (!IsLandlordLoggedIn())
                return RedirectToAction("Login", "Account");

            int userId = HttpContext.Session.GetInt32("UserId").Value;

            if (!await CanAddApartment(userId))
                return RedirectToAction("Premium", "Payment");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ApartmentViewModel model)
        {
            if (!IsLandlordLoggedIn())
                return RedirectToAction("Login", "Account");

            int userId = HttpContext.Session.GetInt32("UserId").Value;

            var apartmentsCount = await _context.Apartments
                .CountAsync(a => a.UserId == userId);

            var subscription = await _context.LandlordSubscriptions
                .FirstOrDefaultAsync(x => x.UserId == userId);

            bool isPremium =
                subscription != null &&
                subscription.Status == "active" &&
                (subscription.CurrentPeriodEndUtc == null ||
                 subscription.CurrentPeriodEndUtc > DateTime.UtcNow);

            if (!isPremium && apartmentsCount >= 1)
                return RedirectToAction("Premium", "Payment");

            var apartment = new Apartment
            {
                UserId = userId,
                Description = model.Description,
                Price = model.Price,
                Size = model.Size,
                Rooms = model.Rooms,
                Photos = new List<Photo>(),
                Addresses = new List<Address>
        {
            new Address
            {
                StreetAddress = model.StreetAddress,
                BuildingNumber = model.BuildingNumber,
                ApartmentNumber = model.ApartmentNumber,
                District = model.District,
                City = model.City,
                Region = model.Region,
                Latitude = model.Latitude,
                Longitude = model.Longitude
            }
        }
            };

            if (model.Photos != null && model.Photos.Any())
            {
                foreach (var file in model.Photos)
                {
                    if (file.Length == 0)
                        continue;

                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                    var ext = Path.GetExtension(file.FileName).ToLower();

                    if (!allowedExtensions.Contains(ext))
                        continue;

                    apartment.Photos.Add(new Photo
                    {
                        PhotoPath = SavePhotoAndGetPublicPath(file)
                    });
                }
            }

            _context.Apartments.Add(apartment);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(MyApartments));
        }


        // Редактирование квартиры
        [HttpGet]
        public IActionResult Edit(int id)
        {
            if (!IsLandlordLoggedIn())
                return RedirectToAction("Login", "Account");

            int userId = HttpContext.Session.GetInt32("UserId").Value;
            var apartment = _context.Apartments
                .Include(a => a.Addresses)
                .Include(a => a.Photos)
                .FirstOrDefault(a => a.ApartmentId == id && a.UserId == userId);

            if (apartment == null)
                return NotFound();

            var address = apartment.Addresses?.FirstOrDefault();
            var viewModel = new ApartmentViewModel
            {
                ApartmentId = apartment.ApartmentId,
                Description = apartment.Description,
                Price = apartment.Price ?? 0,
                Size = apartment.Size ?? 0,
                Rooms = apartment.Rooms ?? 0,
                StreetAddress = address?.StreetAddress,
                BuildingNumber = address?.BuildingNumber,
                ApartmentNumber = address?.ApartmentNumber,
                District = address?.District,
                City = address?.City,
                Region = address?.Region,
                Latitude = address?.Latitude,       // ✅ Для карты Edit
                Longitude = address?.Longitude,    // ✅ Для карты Edit
                PhotoPaths = apartment.Photos?.Select(p => p.PhotoPath).ToList() ?? new()
            };

            viewModel.PhotoPaths = viewModel.PhotoPaths
                .Select(NormalizePhotoPath)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Where(IsPhotoAvailable)
                .Cast<string>()
                .ToList();

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, ApartmentViewModel model)
        {
            if (!IsLandlordLoggedIn())
                return RedirectToAction("Login", "Account");

            if (id <= 0 && model.ApartmentId > 0)
                id = model.ApartmentId;

            int userId = HttpContext.Session.GetInt32("UserId").Value;
            var apartment = _context.Apartments
                .Include(a => a.Addresses)
                .Include(a => a.Photos)
                .FirstOrDefault(a => a.ApartmentId == id && a.UserId == userId);

            if (apartment == null)
                return NotFound();

            // ✅ Обновить основные параметры
            apartment.Description = model.Description;
            apartment.Price = model.Price;
            apartment.Size = model.Size;
            apartment.Rooms = model.Rooms;

            // ✅ Обновить адрес + координаты
            var address = apartment.Addresses?.FirstOrDefault();
            if (address != null)
            {
                address.StreetAddress = model.StreetAddress;
                address.BuildingNumber = model.BuildingNumber;
                address.ApartmentNumber = model.ApartmentNumber;
                address.District = model.District;
                address.City = model.City;
                address.Region = model.Region;
                address.Latitude = model.Latitude;     // ✅ Новые с карты
                address.Longitude = model.Longitude;   // ✅ Новые с карты
            }

            // ✅ Добавить новые фото в /photos
            if (model.Photos != null && model.Photos.Any())
            {
                foreach (var file in model.Photos)
                {
                    if (file.Length == 0)
                        continue;

                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                    var ext = Path.GetExtension(file.FileName).ToLower();

                    if (!allowedExtensions.Contains(ext))
                        continue;

                    apartment.Photos.Add(new Photo
                    {
                        PhotoPath = SavePhotoAndGetPublicPath(file)
                    });
                }
            }

            _context.SaveChanges();
            return RedirectToAction("MyApartments");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePhoto(int apartmentId, string photoPath)
        {
            if (!IsLandlordLoggedIn())
                return Unauthorized(new { success = false, message = "Not authorized" });

            if (string.IsNullOrWhiteSpace(photoPath))
                return BadRequest(new { success = false, message = "Photo path is required" });

            int userId = HttpContext.Session.GetInt32("UserId").Value;
            var photo = _context.Photos
                .Include(p => p.Apartment)
                .Where(p =>
                    p.ApartmentId == apartmentId &&
                    p.Apartment != null &&
                    p.Apartment.UserId == userId)
                .AsEnumerable()
                .FirstOrDefault(p =>
                    string.Equals(
                        NormalizePhotoPath(p.PhotoPath),
                        NormalizePhotoPath(photoPath),
                        StringComparison.OrdinalIgnoreCase));

            if (photo == null)
                return NotFound(new { success = false, message = "Photo not found" });

            if (!string.IsNullOrEmpty(photo.PhotoPath))
            {
                var normalized = NormalizePhotoPath(photo.PhotoPath) ?? string.Empty;
                string filePath;
                if (normalized.StartsWith("/user-photos/", StringComparison.OrdinalIgnoreCase))
                {
                    var fileName = Path.GetFileName(normalized);
                    filePath = Path.Combine(GetUploadStoragePath(), fileName);
                }
                else
                {
                    filePath = Path.Combine(_env.WebRootPath, normalized.TrimStart('/'));
                }

                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            _context.Photos.Remove(photo);
            _context.SaveChanges();

            return Json(new { success = true });
        }

        // Удаление квартиры
        [HttpGet]
        public IActionResult Delete(int id)
        {
            if (!IsLandlordLoggedIn())
                return RedirectToAction("Login", "Account");

            int userId = HttpContext.Session.GetInt32("UserId").Value;

            var apartment = _context.Apartments
                .Include(a => a.Addresses)
                .Include(a => a.Photos)
                .Include(a => a.ReviewApartments)
                .FirstOrDefault(a => a.ApartmentId == id && a.UserId == userId);

            if (apartment == null)
                return NotFound();

            var address = apartment.Addresses?.FirstOrDefault();
            var viewModel = new ApartmentViewModel
            {
                ApartmentId = apartment.ApartmentId,
                Description = apartment.Description,
                Price = apartment.Price ?? 0,
                Size = apartment.Size ?? 0,
                Rooms = apartment.Rooms ?? 0,
                StreetAddress = address?.StreetAddress,
                BuildingNumber = address?.BuildingNumber,
                District = address?.District,
                City = address?.City,
                PhotoPaths = apartment.Photos?.Select(p => p.PhotoPath).ToList() ?? new(),
                ReviewCount = apartment.ReviewApartments.Count,
                Latitude = address?.Latitude,       // ✅ Координаты
                Longitude = address?.Longitude     // ✅ Координаты
            };

            viewModel.PhotoPaths = viewModel.PhotoPaths
                .Select(NormalizePhotoPath)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Where(IsPhotoAvailable)
                .Cast<string>()
                .ToList();

            return View(viewModel);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            if (!IsLandlordLoggedIn())
                return RedirectToAction("Login", "Account");

            int userId = HttpContext.Session.GetInt32("UserId").Value;

            var apartment = _context.Apartments
                .Include(a => a.Addresses)
                    .ThenInclude(ad => ad.Appointments)
                .Include(a => a.Photos)
                .Include(a => a.ReviewApartments)
                .FirstOrDefault(a => a.ApartmentId == id && a.UserId == userId);

            if (apartment == null)
                return NotFound();

            // ✅ 1️⃣ Удалить фото из файловой системы
            if (apartment.Photos != null && apartment.Photos.Any())
            {
                foreach (var photo in apartment.Photos)
                {
                    if (!string.IsNullOrEmpty(photo.PhotoPath))
                    {
                        string filePath = Path.Combine(_env.WebRootPath, photo.PhotoPath.TrimStart('/'));
                        if (System.IO.File.Exists(filePath))
                        {
                            System.IO.File.Delete(filePath);
                        }
                    }
                }
            }

            // ✅ 2️⃣ DELETE appointments (зависит от Address)
            foreach (var address in apartment.Addresses ?? new List<Address>())
            {
                _context.Appointments.RemoveRange(address.Appointments ?? new List<Appointment>());
            }

            // ✅ 3️⃣ Delete photos from DB
            _context.Photos.RemoveRange(apartment.Photos ?? new List<Photo>());

            // ✅ 4️⃣ Delete reviews
            _context.ReviewApartments.RemoveRange(apartment.ReviewApartments ?? new List<ReviewApartment>());

            // ✅ 5️⃣ Delete addresses
            _context.Addresses.RemoveRange(apartment.Addresses ?? new List<Address>());

            // ✅ 6️⃣ Delete apartment
            _context.Apartments.Remove(apartment);

            _context.SaveChanges();

            return RedirectToAction(nameof(MyApartments));
     
        }

        private async Task<bool> IsPremiumLandlord(int userId)
        {
            var sub = await _context.LandlordSubscriptions
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (sub == null) return false;

            if (sub.Status != "active") return false;

            return true;
        }

        private async Task<bool> CanAddApartment(int userId)
        {
            var count = await _context.Apartments
                .CountAsync(a => a.UserId == userId);

            if (count == 0) return true;

            var premium = await IsPremiumLandlord(userId);

            return premium;
        }
    }


}
