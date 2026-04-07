
using HomeFinder.Context;
using HomeFinder.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeFinder
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddHttpClient();
            builder.Services.AddSingleton<IReviewSummaryService, ReviewSummaryService>();
            builder.Services.AddSingleton<IAiReviewSummaryService, OpenAiReviewSummaryService>();
            builder.Services.AddDbContext<HomeFinderContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);

                options.Cookie.Name = "HomeFinder.Session";
                options.Cookie.HttpOnly = true;

                options.Cookie.IsEssential = true;

                options.Cookie.SameSite = SameSiteMode.None;

                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            });
            // Add services to the container.
            builder.Services.AddControllersWithViews(options =>
            {
                // Глобально требуем анти‑подделку для всех небезопасных методов (POST/PUT/DELETE)
                options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();

            app.UseSession();

            app.UseAuthentication();
            app.UseAuthorization();
          
            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();
           

            app.Run();
        }
    }
}
