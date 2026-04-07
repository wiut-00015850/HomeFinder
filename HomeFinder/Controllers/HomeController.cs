using HomeFinder.Context;
using HomeFinder.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeFinder.Controllers
{
    public class HomeController : Controller
    {
        private readonly HomeFinderContext _context;
        private readonly IWebHostEnvironment _env;

        public HomeController(HomeFinderContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
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
                var fullPath = Path.Combine(_env.ContentRootPath, "App_Data", "uploads", "photos", fileName);
                return System.IO.File.Exists(fullPath);
            }

            var localPath = Path.Combine(_env.WebRootPath, normalized.TrimStart('/'));
            return System.IO.File.Exists(localPath);
        }

        public IActionResult Index(
            decimal? priceMin,
            decimal? priceMax,
            int? sizeMin,
            int? sizeMax,
            int? rooms,
            string city = "",
            string district = "",
            string address = "",
            string sortBy = "rating",
            string alltext = "")
        {
            var query = _context.Apartments
                .AsNoTracking()
                .AsSplitQuery()
                .Include(a => a.Addresses)
                .Include(a => a.Photos)
                .Include(a => a.User)
                .Include(a => a.ReviewApartments)
                .AsQueryable();

            if (priceMin.HasValue) query = query.Where(a => a.Price >= priceMin);
            if (priceMax.HasValue) query = query.Where(a => a.Price <= priceMax);
            if (sizeMin.HasValue) query = query.Where(a => a.Size >= sizeMin);
            if (sizeMax.HasValue) query = query.Where(a => a.Size <= sizeMax);
            if (rooms.HasValue) query = query.Where(a => a.Rooms >= rooms);

            if (!string.IsNullOrWhiteSpace(city))
                query = query.Where(a => a.Addresses.Any(ad => ad.City != null && ad.City.Contains(city)));

            if (!string.IsNullOrWhiteSpace(district))
                query = query.Where(a => a.Addresses.Any(ad => ad.District != null && ad.District.Contains(district)));

            if (!string.IsNullOrWhiteSpace(address))
                query = query.Where(a => a.Addresses.Any(ad => ad.StreetAddress != null && ad.StreetAddress.Contains(address)));

            if (!string.IsNullOrWhiteSpace(alltext))
            {
                var text = alltext.Trim();

                query = query.Where(a =>
                    (a.Description != null && a.Description.Contains(text)) ||
                    a.Addresses.Any(ad =>
                        (ad.City != null && ad.City.Contains(text)) ||
                        (ad.District != null && ad.District.Contains(text)) ||
                        (ad.StreetAddress != null && ad.StreetAddress.Contains(text)) ||
                        (ad.BuildingNumber != null && ad.BuildingNumber.Contains(text))) ||
                    (a.User != null &&
                        ((a.User.FirstName != null && a.User.FirstName.Contains(text)) ||
                         (a.User.LastName != null && a.User.LastName.Contains(text))))
                );
            }

            query = sortBy switch
            {
                "rating_asc" => query.OrderBy(a => a.ReviewApartments.Any()
                    ? a.ReviewApartments.Average(r => (double)(r.Rating ?? 0))
                    : 0),

                "price_asc" => query.OrderBy(a => a.Price),
                "price_desc" => query.OrderByDescending(a => a.Price),
                "newest" => query.OrderByDescending(a => a.ApartmentId),
                "reviews" => query.OrderByDescending(a => a.ReviewApartments.Count),

                _ => query.OrderByDescending(a => a.ReviewApartments.Any()
                    ? a.ReviewApartments.Average(r => (double)(r.Rating ?? 0))
                    : 0)
            };

            var viewModels = query
                .Select(a => new ApartmentViewModel
                {
                    ApartmentId = a.ApartmentId,
                    Description = a.Description,
                    Price = a.Price ?? 0,
                    Size = a.Size ?? 0,
                    Rooms = a.Rooms ?? 0,

                    StreetAddress = a.Addresses
                        .OrderBy(x => x.AddressId)
                        .Select(x => x.StreetAddress)
                        .FirstOrDefault(),

                    BuildingNumber = a.Addresses
                        .OrderBy(x => x.AddressId)
                        .Select(x => x.BuildingNumber)
                        .FirstOrDefault(),

                    District = a.Addresses
                        .OrderBy(x => x.AddressId)
                        .Select(x => x.District)
                        .FirstOrDefault(),

                    City = a.Addresses
                        .OrderBy(x => x.AddressId)
                        .Select(x => x.City)
                        .FirstOrDefault(),

                    Latitude = a.Addresses
                        .OrderBy(x => x.AddressId)
                        .Select(x => x.Latitude)
                        .FirstOrDefault(),

                    Longitude = a.Addresses
                        .OrderBy(x => x.AddressId)
                        .Select(x => x.Longitude)
                        .FirstOrDefault(),

                    PhotoPaths = a.Photos
                        .OrderBy(p => p.PhotoId)
                        .Select(p => p.PhotoPath)
                        .ToList(),

                    LandlordName = a.User != null ? (a.User.FirstName + " " + a.User.LastName) : null,
                    PhoneNumber = a.User != null ? a.User.PhoneNumber : null,

                    AverageRating = a.ReviewApartments.Any()
                        ? a.ReviewApartments.Average(r => (double)(r.Rating ?? 0))
                        : 0,

                    ReviewCount = a.ReviewApartments.Count
                })
                .Take(200)
                .ToList();

            foreach (var vm in viewModels)
            {
                vm.PhotoPaths = vm.PhotoPaths
                    .Select(NormalizePhotoPath)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Where(IsPhotoAvailable)
                    .Cast<string>()
                    .ToList();
            }

            ViewData["SortBy"] = sortBy;
            ViewData["PriceMin"] = priceMin;
            ViewData["PriceMax"] = priceMax;
            ViewData["SizeMin"] = sizeMin;
            ViewData["SizeMax"] = sizeMax;
            ViewData["Rooms"] = rooms;
            ViewData["City"] = city;
            ViewData["District"] = district;
            ViewData["Address"] = address;
            ViewData["Alltext"] = alltext;

            return View(viewModels);
        }

        [HttpPost]
        public IActionResult FilterByArea([FromBody] MapAreaRequest request)
        {
            if (request?.Polygon == null || request.Polygon.Count < 3)
            {
                return BadRequest("Polygon is required");
            }

            var minLat = request.Polygon.Min(p => p.Lat);
            var maxLat = request.Polygon.Max(p => p.Lat);
            var minLng = request.Polygon.Min(p => p.Lng);
            var maxLng = request.Polygon.Max(p => p.Lng);

            // Rough filtration by bounding box - goes to SQL
            var candidates = _context.Apartments
                .AsNoTracking()
                .Include(a => a.Addresses)
                .Include(a => a.Photos)
                .Include(a => a.User)
                .Include(a => a.ReviewApartments)
                .Where(a => a.Addresses.Any(ad =>
                    ad.Latitude  >= minLat && ad.Latitude  <= maxLat &&
                    ad.Longitude >= minLng && ad.Longitude <= maxLng))
                .Select(a => new
                {
                    ApartmentId = a.ApartmentId,
                    Price = a.Price ?? 0,
                    StreetAddress = a.Addresses
                        .OrderBy(x => x.AddressId)
                        .Select(x => x.StreetAddress)
                        .FirstOrDefault(),
                    BuildingNumber = a.Addresses
                        .OrderBy(x => x.AddressId)
                        .Select(x => x.BuildingNumber)
                        .FirstOrDefault(),
                    Latitude = a.Addresses
                        .OrderBy(x => x.AddressId)
                        .Select(x => x.Latitude)
                        .FirstOrDefault(),
                    Longitude = a.Addresses
                        .OrderBy(x => x.AddressId)
                        .Select(x => x.Longitude)
                        .FirstOrDefault()
                })
                .ToList();

            bool PointInPolygon(double lat, double lng, List<MapPointDto> poly)
            {
                var pts = poly
                    .Select(p => new { X = (double)p.Lng, Y = (double)p.Lat })
                    .ToList();

                bool inside = false;
                double x = lng;
                double y = lat;

                for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
                {
                    var pi = pts[i];
                    var pj = pts[j];

                    bool intersect = ((pi.Y > y) != (pj.Y > y)) &&
                        (x < (pj.X - pi.X) * (y - pi.Y) / (pj.Y - pi.Y) + pi.X);

                    if (intersect)
                    {
                        inside = !inside;
                    }
                }

                return inside;
            }

            var filtered = candidates
                .Where(a =>
                    a.Latitude.HasValue &&
                    a.Longitude.HasValue &&
                    PointInPolygon(
                        (double)a.Latitude.Value,
                        (double)a.Longitude.Value,
                        request.Polygon))
                .ToList();

            var result = filtered.Select(a => new
            {
                id = a.ApartmentId,
                lat = a.Latitude,
                lng = a.Longitude,
                address = $"{a.StreetAddress} {a.BuildingNumber}",
                price = a.Price,
                detailsUrl = Url.Action("Details", "Apartments", new { id = a.ApartmentId })
            });

            return Json(result);
        }
    }
}
