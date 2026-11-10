using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Clean_Connect.Application.Command.Services
{
    public class GeocodingService
    {
        private readonly HttpClient httpClient;
        private readonly string apiKey;

        public GeocodingService(HttpClient _httpClient, IConfiguration configuration)
        {
            httpClient = _httpClient;
            var configuredApiKey = configuration["Geocoding:ApiKey"];
            if (string.IsNullOrWhiteSpace(configuredApiKey))
                throw new InvalidOperationException("Geocoding is not configured.");
            apiKey = configuredApiKey;
        }

        public async Task<string> GetAddressAsync(double latitude, double longitude)
        {
            var latStr = latitude.ToString(CultureInfo.InvariantCulture);
            var lngStr = longitude.ToString(CultureInfo.InvariantCulture);
            var query = Uri.EscapeDataString($"{latStr}+{lngStr}");
            var url = $"https://api.opencagedata.com/geocode/v1/json?q={query}&key={Uri.EscapeDataString(apiKey)}";

            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to retrieve address from geocoding API.");
            }

            var content = await response.Content.ReadAsStringAsync();

            dynamic? result = Newtonsoft.Json.JsonConvert.DeserializeObject(content);

            if (result?.results == null || result.results.Count == 0)
            {
                return "Lagos, Nigeria";
            }

            return (string)result.results[0].formatted;
        }
    }
}
