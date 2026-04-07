using HomeFinder.Context;
using HomeFinder.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeFinder.Controllers
{
    public class AppointmentsController : Controller
    {
        private readonly HomeFinderContext _context;

        public AppointmentsController(HomeFinderContext context)
        {
            _context = context;
        }

        private bool IsTenantLoggedIn()
        {
            return HttpContext.Session.GetInt32("UserId") != null &&
                   HttpContext.Session.GetString("UserRole") == "Tenant";
        }

        private bool IsLandlordLoggedIn()
        {
            return HttpContext.Session.GetInt32("UserId") != null &&
                   HttpContext.Session.GetString("UserRole") == "Landlord";
        }

        [HttpGet]
        public IActionResult Schedule(int apartmentId)
        {
            if (!IsTenantLoggedIn())
                return RedirectToAction("Login", "Account");

            var apartment = _context.Apartments
                .Include(a => a.Addresses)
                .FirstOrDefault(a => a.ApartmentId == apartmentId);

            if (apartment == null)
                return NotFound();

            var model = new AppointmentViewModel
            {
                ApartmentId = apartmentId,
                AvailableAddresses = apartment.Addresses?.ToList() ?? new List<Address>()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Schedule(AppointmentViewModel model)
        {
            if (!IsTenantLoggedIn())
                return RedirectToAction("Login", "Account");

            if (model.DateTime <= DateTime.Now)
            {
                ModelState.AddModelError("DateTime", "Выберите дату и время в будущем");
                var apartment = _context.Apartments
                    .Include(a => a.Addresses)
                    .FirstOrDefault(a => a.ApartmentId == model.ApartmentId);
                model.AvailableAddresses = apartment?.Addresses?.ToList() ?? new List<Address>();
                return View(model);
            }

            var appointment = new Appointment
            {
                ApartmentId = model.ApartmentId,
                AddressId = model.AddressId,
                DateTime = model.DateTime,
                UserId = HttpContext.Session.GetInt32("UserId")
            };

            _context.Appointments.Add(appointment);
            _context.SaveChanges();

            return RedirectToAction("MyAppointments");
        }

        // ✅ ОДИН УНИВЕРСАЛЬНЫЙ МЕТОД ДЛЯ ОБОИХ
        public IActionResult MyAppointments()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            string userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole == null)
                return RedirectToAction("Login", "Account");

            if (userRole == "Tenant")
            {
                var appointments = _context.Appointments
                    .Include(a => a.Apartment)
                        .ThenInclude(apt => apt.User)
                    .Include(a => a.Address)
                    .OrderByDescending(a => a.DateTime)
                    .ToList();

                return View("MyAppointmentsTenant", appointments);
            }
            else if (userRole == "Landlord")
            {
                // ✅ ДЛЯ ЛЭНДЛОРДА - ТОЛЬКО ЕГО КВАРТИРЫ
                var appointments = _context.Appointments
                    .Where(a => a.Apartment.UserId == userId)
                    .Include(a => a.Apartment)
                        .ThenInclude(apt => apt.User)
                    .Include(a => a.Address)
                    .OrderByDescending(a => a.DateTime)
                    .ToList();

                return View("MyAppointmentsLandlord", appointments);
            }

            return RedirectToAction("Login", "Account");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelAppointment(int id)
        {
            if (!IsTenantLoggedIn())
                return RedirectToAction("Login", "Account");

            var appointment = _context.Appointments
                .Include(a => a.Apartment)
                .FirstOrDefault(a => a.AppointmentId == id);

            if (appointment == null)
                return NotFound();

            _context.Appointments.Remove(appointment);
            _context.SaveChanges();

            return RedirectToAction("MyAppointments");
        }
    }
}
