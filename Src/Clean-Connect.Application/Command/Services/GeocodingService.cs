using System.Globalization;

namespace Clean_Connect.Application.Command.Services
{
    public class GeocodingService
    {
        private readonly HttpClient httpClient;

        public GeocodingService(HttpClient _httpClient)
        {
            httpClient = _httpClient;
        }

        public async Task<string> GetAddressAsync(double latitude, double longitude)
        {
            var apiKey = "db26ad16d0ec4a7b9220db6340ed6885";

            var latStr = latitude.ToString(CultureInfo.InvariantCulture);
            var lngStr = longitude.ToString(CultureInfo.InvariantCulture);
            var url = $"https://api.opencagedata.com/geocode/v1/json?q={latStr}+{lngStr}&key={apiKey}";

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
