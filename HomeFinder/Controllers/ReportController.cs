using HomeFinder.Context;
using HomeFinder.Models.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace HomeFinder.Controllers
{
    public class ReportController : Controller
    {
        private readonly HomeFinderContext _context;

        public ReportController(HomeFinderContext context)
        {
            _context = context;
        }

        private bool IsLoggedIn()
        {
            return !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("UserRole"));
        }

        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("UserRole") == "Admin";
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult MostViewedApartments(
            int top = 5,
            string? dateFrom = null,
            string? dateTo = null,
            decimal? priceMin = null,
            decimal? priceMax = null,
            string? district = null)
        {
            top = ClampTop(top);
            var (from, to) = NormalizePeriod(ParseDate(dateFrom), ParseDate(dateTo));

            var vm = BuildMostViewedApartmentsVm(top, from, to, priceMin, priceMax, district);
            return View(vm);
        }

        [HttpGet]
        public IActionResult MostViewedApartmentsData(
            int top = 5,
            string? dateFrom = null,
            string? dateTo = null,
            decimal? priceMin = null,
            decimal? priceMax = null,
            string? district = null)
        {
            top = ClampTop(top);
            var (from, to) = NormalizePeriod(ParseDate(dateFrom), ParseDate(dateTo));

            var vm = BuildMostViewedApartmentsVm(top, from, to, priceMin, priceMax, district);
            return PartialView("_MostViewedApartmentsDataResponse", vm);
        }

        public IActionResult MostViewedDistricts(int top = 5, string? dateFrom = null, string? dateTo = null)
        {
            top = ClampTop(top);
            var (from, to) = NormalizePeriod(ParseDate(dateFrom), ParseDate(dateTo));
            var vm = BuildMostViewedDistrictsVm(top, from, to);
            return View(vm);
        }

        [HttpGet]
        public IActionResult MostViewedDistrictsData(int top = 5, string? dateFrom = null, string? dateTo = null)
        {
            top = ClampTop(top);
            var (from, to) = NormalizePeriod(ParseDate(dateFrom), ParseDate(dateTo));
            var vm = BuildMostViewedDistrictsVm(top, from, to);
            return PartialView("_MostViewedDistrictsDataResponse", vm);
        }

        public IActionResult ApartmentInteractivity(
            int top = 20,
            string? dateFrom = null,
            string? dateTo = null,
            decimal? priceMin = null,
            decimal? priceMax = null,
            decimal? priceBucketSize = null)
        {
            top = ClampTop(top);
            var (from, to) = NormalizePeriod(ParseDate(dateFrom), ParseDate(dateTo));
            var bucket = NormalizeBucket(priceBucketSize);

            var vm = BuildApartmentInteractivityVm(top, from, to, priceMin, priceMax, bucket);
            return View(vm);
        }

        [HttpGet]
        public IActionResult ApartmentInteractivityData(
            int top = 20,
            string? dateFrom = null,
            string? dateTo = null,
            decimal? priceMin = null,
            decimal? priceMax = null,
            decimal? priceBucketSize = null)
        {
            top = ClampTop(top);
            var (from, to) = NormalizePeriod(ParseDate(dateFrom), ParseDate(dateTo));
            var bucket = NormalizeBucket(priceBucketSize);

            var vm = BuildApartmentInteractivityVm(top, from, to, priceMin, priceMax, bucket);
            return PartialView("_ApartmentInteractivityDataResponse", vm);
        }

        public IActionResult ApartmentPriceAnalytics(
            string? dateFrom = null,
            string? dateTo = null,
            string? district = null,
            int? rooms = null,
            string? apartmentType = null,
            string granularity = "daily")
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Account");

            // Админам этот отчёт скрыт по требованиям.
            if (IsAdmin())
                return RedirectToAction("Index");

            var (from, to) = NormalizePeriod(ParseDate(dateFrom), ParseDate(dateTo));
            var normalizedGranularity = NormalizeGranularity(granularity);

            var vm = BuildApartmentPriceAnalyticsVm(from, to, district, rooms, apartmentType, normalizedGranularity);
            return View(vm);
        }

        [HttpGet]
        public IActionResult ApartmentPriceAnalyticsData(
            string? dateFrom = null,
            string? dateTo = null,
            string? district = null,
            int? rooms = null,
            string? apartmentType = null,
            string granularity = "daily")
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Account");

            // Админам этот отчёт скрыт по требованиям.
            if (IsAdmin())
                return RedirectToAction("Index");

            var (from, to) = NormalizePeriod(ParseDate(dateFrom), ParseDate(dateTo));
            var normalizedGranularity = NormalizeGranularity(granularity);

            var vm = BuildApartmentPriceAnalyticsVm(from, to, district, rooms, apartmentType, normalizedGranularity);
            return PartialView("_ApartmentPriceAnalyticsDataResponse", vm);
        }

        // ---------------------------
        // Админский отчёт: активные арендаторы
        // ---------------------------
        public IActionResult MostActiveTenants(
            int top = 20,
            string? dateFrom = null,
            string? dateTo = null)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Account");

            if (!IsAdmin())
                return RedirectToAction("Index");

            top = ClampTop(top);
            var (from, to) = NormalizePeriod(ParseDate(dateFrom), ParseDate(dateTo));

            var vm = BuildMostActiveTenantsVm(top, from, to);
            return View(vm);
        }

        [HttpGet]
        public IActionResult MostActiveTenantsData(
            int top = 20,
            string? dateFrom = null,
            string? dateTo = null)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Account");

            if (!IsAdmin())
                return RedirectToAction("Index");

            top = ClampTop(top);
            var (from, to) = NormalizePeriod(ParseDate(dateFrom), ParseDate(dateTo));

            var vm = BuildMostActiveTenantsVm(top, from, to);
            return PartialView("_MostActiveTenantsTable", vm);
        }

        private MostActiveTenantsReportVm BuildMostActiveTenantsVm(int top, DateTime fromDate, DateTime toDate)
        {
            var periodStart = fromDate.Date;
            var periodEnd = toDate.Date.AddDays(1);

            // Считаем количество входов в аккаунт (по логам логина) за период.
            var grouped = _context.UserLoginLogs
                .AsNoTracking()
                .Where(l =>
                    l.UserId != null &&
                    l.LoginTime != null &&
                    l.LoginTime >= periodStart &&
                    l.LoginTime < periodEnd &&
                    l.User != null &&
                    l.User.IsTenant == true)
                .GroupBy(l => l.UserId!.Value)
                .Select(g => new
                {
                    UserId = g.Key,
                    LoginCount = g.Count(),
                    LastLoginTime = g.Max(x => x.LoginTime)
                })
                .OrderByDescending(x => x.LoginCount)
                .Take(top)
                .ToList();

            var userIds = grouped.Select(x => x.UserId).ToList();

            // Количество аппойнтментов по tenant (за тот же период).
            var appointmentCounts = _context.Appointments
                .AsNoTracking()
                .Where(a =>
                    a.UserId != null &&
                    userIds.Contains(a.UserId.Value) &&
                    a.DateTime != null &&
                    a.DateTime >= periodStart &&
                    a.DateTime < periodEnd)
                .GroupBy(a => a.UserId!.Value)
                .Select(g => new { UserId = g.Key, AppointmentCount = g.Count() })
                .ToList();

            var appointmentCountDict = appointmentCounts.ToDictionary(x => x.UserId, x => x.AppointmentCount);

            var users = _context.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.UserId) && u.IsTenant == true)
                .Select(u => new
                {
                    u.UserId,
                    u.Login,
                    u.FirstName,
                    u.LastName
                })
                .ToList();

            var userDict = users.ToDictionary(u => u.UserId, u => u);

            var items = grouped.Select(g =>
            {
                userDict.TryGetValue(g.UserId, out var u);
                var name = u == null
                    ? null
                    : string.Join(" ", new[] { u.FirstName, u.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (string.IsNullOrWhiteSpace(name))
                    name = u?.Login;

                return new MostActiveTenantsReportVm.Row
                {
                    UserId = g.UserId,
                    Login = u?.Login ?? $"User {g.UserId}",
                    TenantName = name ?? $"User {g.UserId}",
                    LoginCount = g.LoginCount,
                    LastLoginTime = g.LastLoginTime,
                    AppointmentCount = appointmentCountDict.TryGetValue(g.UserId, out var cnt) ? cnt : 0
                };
            }).ToList();

            return new MostActiveTenantsReportVm
            {
                Top = top,
                DateFrom = fromDate,
                DateTo = toDate,
                Items = items
            };
        }

        private MostViewedDistrictsReportVm BuildMostViewedDistrictsVm(int top, DateTime fromDate, DateTime toDate)
        {
            var periodStart = fromDate.Date;
            var periodEnd = toDate.Date.AddDays(1);

            var viewsByApartment = _context.ApartmentViewLogs
                .AsNoTracking()
                .Where(v => v.ViewedAt >= periodStart && v.ViewedAt < periodEnd)
                .GroupBy(v => v.ApartmentId)
                .Select(g => new { ApartmentId = g.Key, Views = g.Count() })
                .ToList();

            var districtApartments = _context.Addresses
                .AsNoTracking()
                .Where(ad => ad.ApartmentId != null && ad.District != null && ad.District != "")
                .Select(ad => new { District = ad.District!, ApartmentId = ad.ApartmentId!.Value })
                .ToList();

            var viewCounts = viewsByApartment.ToDictionary(x => x.ApartmentId, x => x.Views);

            var items = districtApartments
                .GroupBy(x => x.District)
                .Select(g => new MostViewedDistrictsReportVm.Row
                {
                    District = g.Key,
                    TotalViews = g.Sum(x => viewCounts.TryGetValue(x.ApartmentId, out var c) ? c : 0),
                    ApartmentsCount = g.Select(x => x.ApartmentId).Distinct().Count()
                })
                .OrderByDescending(x => x.TotalViews)
                .Take(top)
                .ToList();

            return new MostViewedDistrictsReportVm
            {
                Top = top,
                DateFrom = fromDate,
                DateTo = toDate,
                Items = items
            };
        }

        private MostViewedApartmentsReportVm BuildMostViewedApartmentsVm(
            int top,
            DateTime fromDate,
            DateTime toDate,
            decimal? priceMin,
            decimal? priceMax,
            string? district)
        {
            var periodStart = fromDate.Date;
            var periodEnd = toDate.Date.AddDays(1);

            var viewsByApartment = _context.ApartmentViewLogs
                .AsNoTracking()
                .Where(v => v.ViewedAt >= periodStart && v.ViewedAt < periodEnd)
                .GroupBy(v => v.ApartmentId)
                .Select(g => new { ApartmentId = g.Key, Views = g.Count() })
                .ToList();

            var allViewedApartmentIds = viewsByApartment.Select(x => x.ApartmentId).ToList();
            if (allViewedApartmentIds.Count == 0)
            {
                return new MostViewedApartmentsReportVm
                {
                    Top = top,
                    DateFrom = fromDate,
                    DateTo = toDate,
                    SelectedDistrict = string.IsNullOrWhiteSpace(district) ? null : district.Trim(),
                    Items = new List<MostViewedApartmentsReportVm.Row>()
                };
            }

            // Список районов строим по всем квартирам, которые имели просмотры за период
            var districts = _context.Addresses
                .AsNoTracking()
                .Where(ad => ad.ApartmentId != null &&
                             allViewedApartmentIds.Contains(ad.ApartmentId.Value) &&
                             ad.District != null &&
                             ad.District != "")
                .Select(ad => ad.District!)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var selectedDistrict = string.IsNullOrWhiteSpace(district) ? null : district.Trim();

            var apartmentsQuery = _context.Apartments
                .AsNoTracking()
                .Where(a => allViewedApartmentIds.Contains(a.ApartmentId));

            if (priceMin.HasValue)
            {
                apartmentsQuery = apartmentsQuery.Where(a => a.Price == null || a.Price >= priceMin.Value);
            }
            if (priceMax.HasValue)
            {
                apartmentsQuery = apartmentsQuery.Where(a => a.Price == null || a.Price <= priceMax.Value);
            }
            if (!string.IsNullOrWhiteSpace(selectedDistrict))
            {
                apartmentsQuery = apartmentsQuery.Where(a => a.Addresses.Any(ad => ad.District != null && ad.District == selectedDistrict));
            }

            var apartments = apartmentsQuery
                .Select(a => new MostViewedApartmentsReportVm.Row
                {
                    ApartmentId = a.ApartmentId,
                    Views = 0,
                    Price = a.Price,
                    District = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.District).FirstOrDefault(),
                    City = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.City).FirstOrDefault(),
                    StreetAddress = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.StreetAddress).FirstOrDefault(),
                    BuildingNumber = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.BuildingNumber).FirstOrDefault(),
                    PhotoPath = a.Photos.OrderBy(p => p.PhotoId).Select(p => p.PhotoPath).FirstOrDefault()
                })
                .ToList();

            var viewCounts = viewsByApartment.ToDictionary(x => x.ApartmentId, x => x.Views);
            foreach (var row in apartments)
            {
                row.Views = viewCounts.TryGetValue(row.ApartmentId, out var c) ? c : 0;
            }
            var items = apartments
                .OrderByDescending(r => r.Views)
                .Take(top)
                .ToList();

            return new MostViewedApartmentsReportVm
            {
                Top = top,
                DateFrom = fromDate,
                DateTo = toDate,
                SelectedDistrict = selectedDistrict,
                Districts = districts,
                Items = items
            };
        }

        private ApartmentInteractivityReportVm BuildApartmentInteractivityVm(
            int top,
            DateTime fromDate,
            DateTime toDate,
            decimal? priceMin,
            decimal? priceMax,
            decimal priceBucketSize)
        {
            var periodStart = fromDate.Date;
            var periodEnd = toDate.Date.AddDays(1);

            var viewsByApartment = _context.ApartmentViewLogs
                .AsNoTracking()
                .Where(v => v.ViewedAt >= periodStart && v.ViewedAt < periodEnd)
                .GroupBy(v => v.ApartmentId)
                .Select(g => new { ApartmentId = g.Key, Views = g.Count() })
                .ToList();

            var inquiriesByApartment = _context.Appointments
                .AsNoTracking()
                .Where(a => a.ApartmentId != null && a.DateTime != null && a.DateTime >= periodStart && a.DateTime < periodEnd)
                .GroupBy(a => a.ApartmentId!.Value)
                .Select(g => new { ApartmentId = g.Key, Inquiries = g.Count() })
                .ToList();

            var favoritesByApartment = _context.Favorites
                .AsNoTracking()
                .Where(f => f.ApartmentId != null)
                .GroupBy(f => f.ApartmentId!.Value)
                .Select(g => new { ApartmentId = g.Key, Favorites = g.Count() })
                .ToList();

            var viewsDict = viewsByApartment.ToDictionary(x => x.ApartmentId, x => x.Views);
            var inqDict = inquiriesByApartment.ToDictionary(x => x.ApartmentId, x => x.Inquiries);
            var favDict = favoritesByApartment.ToDictionary(x => x.ApartmentId, x => x.Favorites);

            // Берём квартиры, у которых была хоть какая-то активность за период (просмотры или обращения)
            var activeApartmentIds = viewsDict.Keys
                .Union(inqDict.Keys)
                .Distinct()
                .ToList();

            if (activeApartmentIds.Count == 0)
            {
                return new ApartmentInteractivityReportVm
                {
                    Top = top,
                    DateFrom = fromDate,
                    DateTo = toDate,
                    PriceMin = priceMin,
                    PriceMax = priceMax,
                    PriceBucketSize = priceBucketSize,
                    Items = new List<ApartmentInteractivityReportVm.Row>()
                };
            }

            var apartmentsQuery = _context.Apartments
                .AsNoTracking()
                .Where(a => activeApartmentIds.Contains(a.ApartmentId));

            if (priceMin.HasValue)
                apartmentsQuery = apartmentsQuery.Where(a => a.Price == null || a.Price >= priceMin.Value);
            if (priceMax.HasValue)
                apartmentsQuery = apartmentsQuery.Where(a => a.Price == null || a.Price <= priceMax.Value);

            var rows = apartmentsQuery
                .Select(a => new ApartmentInteractivityReportVm.Row
                {
                    ApartmentId = a.ApartmentId,
                    Views = 0,
                    Inquiries = 0,
                    FavoritesTotal = 0,
                    ConversionRate = 0,

                    Price = a.Price,
                    Rooms = a.Rooms,
                    District = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.District).FirstOrDefault(),
                    City = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.City).FirstOrDefault(),
                    StreetAddress = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.StreetAddress).FirstOrDefault(),
                    BuildingNumber = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.BuildingNumber).FirstOrDefault(),
                    PhotoPath = a.Photos.OrderBy(p => p.PhotoId).Select(p => p.PhotoPath).FirstOrDefault(),
                    DetailsUrl = Url.Action("Details", "Apartments", new { id = a.ApartmentId })
                })
                .ToList();

            foreach (var r in rows)
            {
                r.Views = viewsDict.TryGetValue(r.ApartmentId, out var v) ? v : 0;
                r.Inquiries = inqDict.TryGetValue(r.ApartmentId, out var i) ? i : 0;
                r.FavoritesTotal = favDict.TryGetValue(r.ApartmentId, out var f) ? f : 0;
                r.ConversionRate = r.Views > 0 ? (double)r.Inquiries / r.Views : 0;
            }

            // Топ по конверсии: чтобы не “обманывали” единичные просмотры, требуем минимум просмотров
            var items = rows
                .OrderByDescending(r => r.Views >= 10 ? r.ConversionRate : -1)
                .ThenByDescending(r => r.Inquiries)
                .ThenByDescending(r => r.Views)
                .Take(top)
                .ToList();

            var byPrice = rows
                .Where(r => r.Price.HasValue && r.Price.Value > 0)
                .GroupBy(r => PriceBucketKey(r.Price!.Value, priceBucketSize))
                .Select(g => BuildGroupRow(g.Key, g))
                .OrderByDescending(x => x.ConversionRate)
                .ToList();

            var byDistrict = rows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.District) ? "—" : r.District!.Trim())
                .Select(g => BuildGroupRow(g.Key, g))
                .OrderByDescending(x => x.ConversionRate)
                .ToList();

            var byRooms = rows
                .GroupBy(r => RoomsKey(r.Rooms))
                .Select(g => BuildGroupRow(g.Key, g))
                .OrderByDescending(x => x.ConversionRate)
                .ToList();

            return new ApartmentInteractivityReportVm
            {
                Top = top,
                DateFrom = fromDate,
                DateTo = toDate,
                PriceMin = priceMin,
                PriceMax = priceMax,
                PriceBucketSize = priceBucketSize,
                Items = items,
                ByPriceRange = byPrice,
                ByDistrict = byDistrict,
                ByRooms = byRooms
            };
        }

        private ApartmentPriceAnalyticsReportVm BuildApartmentPriceAnalyticsVm(
            DateTime fromDate,
            DateTime toDate,
            string? district,
            int? rooms,
            string? apartmentType,
            string granularity)
        {
            var periodStart = fromDate.Date;
            var periodEnd = toDate.Date.AddDays(1);

            var normalizedDistrict = string.IsNullOrWhiteSpace(district) ? null : district.Trim();
            var normalizedType = string.IsNullOrWhiteSpace(apartmentType) ? null : apartmentType.Trim().ToLowerInvariant();
            var normalizedRooms = rooms;

            // Готовим срез просмотров по периоду — именно они дают нам "время" для тренда.
            // Цена берётся из текущего Apartment.price (истории цен нет).
            var viewRows = _context.ApartmentViewLogs
                .AsNoTracking()
                .Where(v => v.ViewedAt >= periodStart && v.ViewedAt < periodEnd)
                .Select(v => new
                {
                    v.ApartmentId,
                    v.ViewedAt,
                    Price = v.Apartment.Price,
                    Rooms = v.Apartment.Rooms,
                    District = v.Apartment.Addresses
                        .OrderBy(ad => ad.AddressId)
                        .Select(ad => ad.District)
                        .FirstOrDefault()
                })
                .ToList();

            var priced = viewRows
                .Where(x => x.Price != null)
                .Select(x => new
                {
                    x.ApartmentId,
                    x.ViewedAt,
                    Price = x.Price!.Value,
                    x.Rooms,
                    District = x.District
                })
                .ToList();

            // Опции районов строим по тем же фильтрам, кроме selectedDistrict (чтобы выбор показывал реальные варианты)
            var apartmentsSnapshots = priced
                .GroupBy(x => x.ApartmentId)
                .Select(g => g.First())
                .Where(x => ApartmentTypeMatches(x.Rooms, normalizedRooms, normalizedType))
                .ToList();

            var availableDistricts = apartmentsSnapshots
                .Select(x => x.District)
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct()
                .OrderBy(x => x!)
                .Select(x => x!)
                .ToList();

            // byDistrict и тренды считаем по отфильтрованным просмотрам (с выбранным районом)
            var filteredViewRows = priced
                .Where(x =>
                    ApartmentTypeMatches(x.Rooms, normalizedRooms, normalizedType) &&
                    (normalizedDistrict == null ||
                        string.Equals((string?)x.District, normalizedDistrict, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var distinctApartmentsFiltered = filteredViewRows
                .GroupBy(x => x.ApartmentId)
                .Select(g => g.First())
                .ToList();

            var avgByDistrict = distinctApartmentsFiltered
                .Where(x => !string.IsNullOrWhiteSpace(x.District))
                .GroupBy(x => x.District!)
                .Select(g => new ApartmentPriceAnalyticsReportVm.AverageByDistrictRow
                {
                    District = g.Key,
                    AveragePrice = g.Average(x => x.Price),
                    ApartmentsCount = g.Count()
                })
                .OrderByDescending(x => x.AveragePrice)
                .ToList();

            // Тренд по времени: bucket => средняя цена по просмотрам
            var trend = filteredViewRows
                .Select(x =>
                {
                    var bucketStart = GetBucketStart(x.ViewedAt, granularity);
                    var label = FormatBucketLabel(bucketStart, granularity);
                    return new { x.ViewedAt, x.Price, x.ApartmentId, bucketStart, label };
                })
                .GroupBy(x => x.bucketStart)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var first = g.First();
                    return new ApartmentPriceAnalyticsReportVm.PriceTrendRow
                    {
                        Label = first.label,
                        AveragePrice = g.Average(x => x.Price),
                        ViewsCount = g.Count()
                    };
                })
                .OrderBy(x => x.Label)
                .ToList();

            return new ApartmentPriceAnalyticsReportVm
            {
                DateFrom = fromDate,
                DateTo = toDate,
                SelectedDistrict = normalizedDistrict,
                Districts = availableDistricts,
                Rooms = normalizedRooms,
                SelectedApartmentType = normalizedType,
                Granularity = granularity,
                AverageByDistrict = avgByDistrict,
                PriceTrend = trend
            };
        }

        private static bool ApartmentTypeMatches(int? rooms, int? filterRooms, string? apartmentType)
        {
            if (filterRooms.HasValue)
            {
                if (!rooms.HasValue) return false;
                if (rooms.Value != filterRooms.Value) return false;
            }

            if (string.IsNullOrWhiteSpace(apartmentType) || apartmentType == "all")
                return true;

            if (!rooms.HasValue) return false;

            return apartmentType switch
            {
                "studio" => rooms.Value == 1,
                "family" => rooms.Value >= 2 && rooms.Value <= 3,
                "large" => rooms.Value >= 4,
                _ => true
            };
        }

        private static string NormalizeGranularity(string granularity)
        {
            var g = string.IsNullOrWhiteSpace(granularity) ? "daily" : granularity.Trim().ToLowerInvariant();
            return g switch
            {
                "weekly" => "weekly",
                "monthly" => "monthly",
                _ => "daily"
            };
        }

        private static DateTime GetBucketStart(DateTime dt, string granularity)
        {
            var d = dt.Date;
            if (granularity == "monthly")
                return new DateTime(d.Year, d.Month, 1);

            if (granularity == "weekly")
            {
                // Неделя с понедельника
                var diff = ((int)d.DayOfWeek + 6) % 7;
                return d.AddDays(-diff);
            }

            return d; // daily
        }

        private static string FormatBucketLabel(DateTime bucketStart, string granularity)
        {
            var inv = CultureInfo.InvariantCulture;
            if (granularity == "monthly")
                return bucketStart.ToString("MM.yyyy", inv);
            if (granularity == "weekly")
                return bucketStart.ToString("dd.MM.yyyy", inv);
            return bucketStart.ToString("dd.MM.yyyy", inv);
        }

        private static ApartmentInteractivityReportVm.GroupRow BuildGroupRow(
            string key,
            IEnumerable<ApartmentInteractivityReportVm.Row> rows)
        {
            var list = rows.ToList();
            var views = list.Sum(x => x.Views);
            var inq = list.Sum(x => x.Inquiries);

            return new ApartmentInteractivityReportVm.GroupRow
            {
                Key = key,
                ApartmentsCount = list.Count,
                Views = views,
                Inquiries = inq,
                ConversionRate = views > 0 ? (double)inq / views : 0
            };
        }

        private static string RoomsKey(int? rooms)
        {
            if (!rooms.HasValue) return "Не указано";
            if (rooms.Value <= 0) return "Не указано";
            if (rooms.Value >= 4) return "4+ комнат";
            return $"{rooms.Value} комнат";
        }

        private static string PriceBucketKey(decimal price, decimal bucketSize)
        {
            if (bucketSize <= 0) bucketSize = 1;
            var idx = (int)Math.Floor(price / bucketSize);
            var from = idx * bucketSize;
            var to = from + bucketSize;
            return $"{from:n0} – {to:n0}";
        }

        private static decimal NormalizeBucket(decimal? bucket)
        {
            if (!bucket.HasValue) return 1_000_000m;
            if (bucket.Value <= 0) return 1_000_000m;
            return bucket.Value;
        }

        private static (DateTime from, DateTime to) NormalizePeriod(DateTime? dateFrom, DateTime? dateTo)
        {
            var today = DateTime.Today;

            var to = (dateTo ?? today).Date;
            if (to > today) to = today;

            var from = (dateFrom ?? to.AddMonths(-1)).Date;

            if (from > to)
            {
                var tmp = from;
                from = to;
                to = tmp;
            }

            return (from, to);
        }

        private static DateTime? ParseDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            if (DateTime.TryParseExact(
                value.Trim(),
                "dd.MM.yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
            {
                return parsed.Date;
            }

            return null;
        }

        private static int ClampTop(int top)
        {
            if (top <= 0) return 5;
            if (top > 50) return 50;
            return top;
        }
    }
}