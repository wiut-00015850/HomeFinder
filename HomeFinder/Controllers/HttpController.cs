using Microsoft.AspNetCore.Mvc;

namespace HomeFinder.Controllers
{
    [ApiController]
    [Route("api/http")]
    public class HttpController : ControllerBase
    {
        private readonly HttpClient _httpClient;

        public HttpController(IHttpClientFactory factory)
        {
            _httpClient = factory.CreateClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HomeFinder/1.0 (contact: test@mail.ru)");
        }

        [HttpGet("reverse")]
        public async Task<IActionResult> Reverse([FromQuery] double lat, [FromQuery] double lon)
        {
            var url = $"https://nominatim.openstreetmap.org/reverse?format=json&lat={lat}&lon={lon}&zoom=18&addressdetails=1&accept-language=ru,en";

            return Content(await _httpClient.GetStringAsync(url), "application/json");
        }
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string str)
        {
            var url = $"https://nominatim.openstreetmap.org/search?format=json&q={Uri.EscapeDataString(str)}&limit=3&countrycodes=UZ&accept-language=ru,en";

            return Content(await _httpClient.GetStringAsync(url), "application/json");
        }
    }
}
