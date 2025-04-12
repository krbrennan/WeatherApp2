using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;




var builder = WebApplication.CreateBuilder(args);
// Access the API key from appsettings.json
string apiKey = builder.Configuration["GoogleGeocoding:ApiKey"];


// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Weather API v1"));
    //app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

//var summaries = new[]
//{
//    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
//};

app.MapGet("/weatherforecast", async (string? location) => // Mark the lambda as async
{
    if (string.IsNullOrWhiteSpace(location))
    {
        return Results.BadRequest("Location (city name, zip code, or address) is required.");
    }

    string locationType;
    if (System.Text.RegularExpressions.Regex.IsMatch(location, @"^\d{5}$"))
    {
        locationType = "Zip Code";
    }
    else if (System.Text.RegularExpressions.Regex.IsMatch(location, @"^[a-zA-Z\s]+$"))
    {
        locationType = "City";
    }
    else
    {
        locationType = "Address";
    }

    // google geocode api to get lat and long
    var geocodingService = new GeocodingService();
    var coordinates = await geocodingService.GetLatLongFromGoogleApiAsync(location, apiKey);

    if (coordinates == null)
    {
        Console.WriteLine("Failed to retrieve coordinates.");
    }
    else
    {
        Console.WriteLine($"Coordinates: {coordinates}");
    }


    // Call the weather API using the latitude and longitude
    string weatherApiUrl = $"https://api.weather.gov/points/{coordinates.Value.Latitude},{coordinates.Value.Longitude}";
    using var httpClient = new HttpClient();
    // gotta have them headers!
    httpClient.DefaultRequestHeaders.Add("User-Agent", "WeatherApp/1.0"); // Add User-Agent header
    var weatherApiResponse = await httpClient.GetAsync(weatherApiUrl);

    if (!weatherApiResponse.IsSuccessStatusCode)
    {
        Console.WriteLine($"Failed to fetch weather data for coordinates: Latitude = {coordinates.Value.Latitude}, Longitude = {coordinates.Value.Longitude}");
        return Results.BadRequest("Invalid information provided.");
    }

    var weatherApiResponseContent = await weatherApiResponse.Content.ReadAsStringAsync();
    Console.WriteLine($"Weather API Response: {weatherApiResponseContent}");

  

    return Results.Ok(weatherApiResponseContent); // 'forecast' is now defined
})
.WithName("GetWeatherForecast") // Ensure this is part of the chain
.WithOpenApi();


app.MapGet("/test-geocoding", async () =>
{
    var geocodingService = new GeocodingService();
    string testLocation = "1600 Amphitheatre Parkway, Mountain View, CA"; // Example address
    string testApiKey = "AIzaSyCaODk6-70vzIOmaWZ4nTZE6maPGq1nmjU"; // Replace with your actual API key

    try
    {
        var coordinates = await geocodingService.GetLatLongFromGoogleApiAsync(testLocation, testApiKey);
        if (coordinates != null)
        {
            Console.WriteLine($"Latitude: {coordinates.Value.Latitude}, Longitude: {coordinates.Value.Longitude}");
            return Results.Ok($"Latitude: {coordinates.Value.Latitude}, Longitude: {coordinates.Value.Longitude}");
        }
        else
        {
            Console.WriteLine("Failed to retrieve coordinates.");
            return Results.BadRequest("Failed to retrieve coordinates.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        return Results.Problem($"Error: {ex.Message}");
    }
});

app.MapGet("/test-weatherAPI", async (HttpContext context) =>
{
    // Call the weather API using the latitude and longitude
    string weatherApiUrl = "https://api.weather.gov/points/39.7456,-97.0892";
    using var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Add("User-Agent", "WeatherApp/1.0"); // Add User-Agent header
    var weatherApiResponse = await httpClient.GetAsync(weatherApiUrl);

    //if (!weatherApiResponse.IsSuccessStatusCode)
    //{
    //    return Results.BadRequest("Invalid information provided.");
    //}

    var weatherApiResponseContent = await weatherApiResponse.Content.ReadAsStringAsync();
    Console.WriteLine($"Weather API Response: {weatherApiResponseContent}");
    return Results.Ok(weatherApiResponseContent);
});




app.Run();
internal class GeocodingService
{
    //public async Task<(double Latitude, double Longitude)?> GetLatLongFromGoogleApiAsync(string location, string locationType, string api)
    public async Task<(double Latitude, double Longitude)?> GetLatLongFromGoogleApiAsync(string location, string api)
    {
        {
            using var httpClient = new HttpClient();
            // gotta have them headers!
            httpClient.DefaultRequestHeaders.Add("User-Agent", "WeatherApp/1.0"); // Add User-Agent header
            //string googleApiKey = apiKey; // Replace with your actual Google API key
            //string requestUri = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(location)}&key={api}";
            string requestUri = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(location)}&key=AIzaSyCaODk6-70vzIOmaWZ4nTZE6maPGq1nmjU";


            var response = await httpClient.GetAsync(requestUri);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var geocodeResult = System.Text.Json.JsonDocument.Parse(jsonResponse);

            Console.WriteLine(geocodeResult);

            var locationElement = geocodeResult.RootElement
                .GetProperty("results")
                .EnumerateArray()
                .FirstOrDefault()
                .GetProperty("geometry")
                .GetProperty("location");

            double latitude = locationElement.GetProperty("lat").GetDouble();
            double longitude = locationElement.GetProperty("lng").GetDouble();

            return (latitude, longitude);
        }
    }
}



//internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
//{
//    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
//}


