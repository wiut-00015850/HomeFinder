using HomeFinder.Context;
using HomeFinder.Models;
using Microsoft.AspNetCore.Mvc;

namespace HomeFinder.Controllers
{
    public class ReviewsController : Controller
    {
        private readonly HomeFinderContext _context;

        public ReviewsController(HomeFinderContext context)
        {
            _context = context;
        }

        private bool IsTenantLoggedIn()
        {
            return HttpContext.Session.GetInt32("UserId") != null &&
                   HttpContext.Session.GetString("UserRole") == "Tenant";
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddReview(int apartmentId, int rating, string comment)
        {
            if (!IsTenantLoggedIn())
                return RedirectToAction("Login", "Account");

            // ✅ Проверка рейтинга
            if (rating < 1 || rating > 5)
            {
                ModelState.AddModelError("rating", "Рейтинг должен быть от 1 до 5");
                return RedirectToAction("Details", "Apartments", new { id = apartmentId });
            }

            // ✅ Проверка комментария
            if (string.IsNullOrWhiteSpace(comment))
            {
                ModelState.AddModelError("comment", "Комментарий не может быть пустым");
                return RedirectToAction("Details", "Apartments", new { id = apartmentId });
            }

            int userId = HttpContext.Session.GetInt32("UserId").Value;

            try
            {
                // ✅ Проверка существующего отзыва
                var existingReview = _context.ReviewApartments
                    .FirstOrDefault(r => r.UserId == userId && r.ApartmentId == apartmentId);

                if (existingReview != null)
                {
                    // Обновление существующего отзыва
                    existingReview.Rating = rating;
                    existingReview.Comment = comment;
                    existingReview.CreatedAt = DateTime.Now;  // ✅ Используем CreatedAt из БД
                    _context.ReviewApartments.Update(existingReview);
                }
                else
                {
                    // Создание нового отзыва
                    var review = new ReviewApartment
                    {
                        UserId = userId,
                        ApartmentId = apartmentId,
                        Rating = rating,
                        Comment = comment,
                        CreatedAt = DateTime.Now  // ✅ Используем CreatedAt из БД
                    };
                    _context.ReviewApartments.Add(review);
                }

                _context.SaveChanges();

                return RedirectToAction("Details", "Apartments", new { id = apartmentId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Ошибка при сохранении отзыва: " + ex.Message);
                return RedirectToAction("Details", "Home", new { id = apartmentId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteReview(int reviewId)
        {
            if (!IsTenantLoggedIn())
                return RedirectToAction("Login", "Account");

            int userId = HttpContext.Session.GetInt32("UserId").Value;

            // ✅ Используем RApartmentId как в БД
            var review = _context.ReviewApartments
                .FirstOrDefault(r => r.RApartmentId == reviewId && r.UserId == userId);

            if (review == null)
                return NotFound();

            int? apartmentId = review.ApartmentId;

            try
            {
                _context.ReviewApartments.Remove(review);
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Ошибка при удалении отзыва: " + ex.Message);
            }

            return RedirectToAction("Details", "Apartments", new { id = apartmentId });
        }
    }
}