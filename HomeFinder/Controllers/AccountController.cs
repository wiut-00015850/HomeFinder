using HomeFinder.Context;
using HomeFinder.Models;
using HomeFinder.Security;
using Microsoft.AspNetCore.Mvc;

public class AccountController : Controller
{
    private readonly HomeFinderContext _context;

    public AccountController(HomeFinderContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public IActionResult Login(LoginViewModel model)
    {
        if (string.IsNullOrEmpty(model.UserType))
        {
            ViewBag.Error = "Выберите тип пользователя";
            return View();
        }

        if (model.UserType == "admin")
        {
            var admin = _context.Administrators
                .FirstOrDefault(a => a.Login == model.Login);

            if (admin == null || string.IsNullOrEmpty(admin.Password))
            {
                ViewBag.Error = "Неверный логин или пароль администратора";
                return View();
            }

            bool ok = PasswordHasher.VerifyAndUpgrade(model.Password, admin.Password, out var upgradedHash);
            if (ok && !string.IsNullOrEmpty(upgradedHash))
            {
                admin.Password = upgradedHash;
                _context.SaveChanges();
            }

            if (!ok)
            {
                ViewBag.Error = "Неверный логин или пароль администратора";
                return View();
            }

            HttpContext.Session.SetInt32("AdminId", admin.AdministratorId);
            HttpContext.Session.SetString("UserRole", "Admin");
            return RedirectToAction("Index", "Admin");
        }
        else if (model.UserType == "landlord")
        {
            var user = _context.Users
                .FirstOrDefault(u => u.Login == model.Login && u.IsLandlord == true);

            if (user == null || string.IsNullOrEmpty(user.Password))
            {
                ViewBag.Error = "Неверный логин или пароль владельца";
                return View();
            }

            bool ok = PasswordHasher.VerifyAndUpgrade(model.Password, user.Password, out var upgradedHash);
            if (ok && !string.IsNullOrEmpty(upgradedHash))
            {
                user.Password = upgradedHash;
                _context.SaveChanges();
            }

            if (!ok)
            {
                ViewBag.Error = "Неверный логин или пароль владельца";
                return View();
            }

            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("UserRole", "Landlord");
            HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");

            var sub = _context.LandlordSubscriptions
                .FirstOrDefault(x => x.UserId == user.UserId);

            if (sub != null && sub.Status == "active")
                HttpContext.Session.SetString("IsPremium", "1");
            else
                HttpContext.Session.SetString("IsPremium", "0");

            return RedirectToAction("MyApartments", "Apartments");
        }
        else if (model.UserType == "tenant")
        {
            var user = _context.Users
                .FirstOrDefault(u => u.Login == model.Login && u.IsTenant == true);

            if (user == null || string.IsNullOrEmpty(user.Password))
            {
                ViewBag.Error = "Неверный логин или пароль арендатора";
                return View();
            }

            bool ok = PasswordHasher.VerifyAndUpgrade(model.Password, user.Password, out var upgradedHash);
            if (ok && !string.IsNullOrEmpty(upgradedHash))
            {
                user.Password = upgradedHash;
                _context.SaveChanges();
            }

            if (!ok)
            {
                ViewBag.Error = "Неверный логин или пароль арендатора";
                return View();
            }

            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("UserRole", "Tenant");
            HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");

            // Для админского отчёта фиксируем вход арендатора в лог авторизации.
            _context.UserLoginLogs.Add(new UserLoginLog
            {
                UserId = user.UserId,
                LoginTime = System.DateTime.Now
            });
            _context.SaveChanges();

            return RedirectToAction("Index", "Home");
        }

        ViewBag.Error = "Неверный тип пользователя";
        return View();
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }
}
