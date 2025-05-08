using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Text.Json;






var builder = WebApplication.CreateBuilder(args);
// Access the API key from appsettings.json
//string apiKey = builder.Configuration["GoogleGeocoding:ApiKey"];
builder.Configuration.AddEnvironmentVariables();


// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Access the API key from appsettings.Development.json
string apiKey = builder.Configuration["GoogleGeocoding:ApiKey"];

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

app.MapGet("/weatherforecast", async (string? location) => // Mark the lambda as async
//app.MapGet("/weatherforecast", async (Microsoft.AspNetCore.Http.HttpContext? location) => // Mark the lambda as async
{
    if (string.IsNullOrWhiteSpace(location))
    {
        return Results.BadRequest("Location (city name, zip code, or address) is required.");
    } else if(location.Length < 5 || location.Length > 5)
    {
        return Results.BadRequest("Improperly formatted zip code");
    }

        //if (location == null)
        //{
        //    return Results.BadRequest("HttpContext is required but was not provided.");
        //}

        //if (location is not HttpContext)
        //{
        //    return Results.BadRequest("Invalid type for HttpContext.");
        //}

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

    try
    {
        var jsonDocument = JsonDocument.Parse(weatherApiResponseContent);
        var data = jsonDocument.RootElement;

        var properties = data.GetProperty("properties");
        var forecast = properties.GetProperty("forecast");
        var forecastHourly = properties.GetProperty("forecastHourly");


        //return Results.Ok(new { Forecast = forecast, ForecastHourly = forecastHourly });
        //return getAndFormatWeatherData(forecast, forecastHourly);
        return await getAndFormatWeatherData(forecast.GetString(), forecastHourly.GetString());
    }
    catch (JsonException ex)
    {
        Console.WriteLine($"Error parsing JSON: {ex.Message}");
        return Results.Problem("Failed to parse weather API response as JSON.");
    }
})
.WithName("GetWeatherForecast") // Ensure this is part of the chain
.WithOpenApi();





async Task<object> getAndFormatWeatherData(string forecast, string forecastHourly)
{
    using var httpClient = new HttpClient();
    // gotta have them headers!
    httpClient.DefaultRequestHeaders.Add("User-Agent", "WeatherApp/1.0");

    // Fetch data from the forecast endpoint
    var forecastResponse = await httpClient.GetAsync(forecast);
    if (!forecastResponse.IsSuccessStatusCode)
    {
        return new { Error = "Failed to fetch forecast data." };
    }
    var forecastContent = await forecastResponse.Content.ReadAsStringAsync();

    // Fetch data from the forecastHourly endpoint
    var forecastHourlyResponse = await httpClient.GetAsync(forecastHourly);
    if (!forecastHourlyResponse.IsSuccessStatusCode)
    {
        return new { Error = "Failed to fetch hourly forecast data." };
    }
    var forecastHourlyContent = await forecastHourlyResponse.Content.ReadAsStringAsync();

    // Parse the JSON responses
    try
    {
        var forecastJson = JsonDocument.Parse(forecastContent).RootElement;
        var forecastHourlyJson = JsonDocument.Parse(forecastHourlyContent).RootElement;

        // Combine and return the data
        return new
        {
            Forecast = forecastJson,
            ForecastHourly = forecastHourlyJson
        };
    }
    catch (JsonException ex)
    {
        Console.WriteLine($"Error parsing JSON: {ex.Message}");
        return new { Error = "Failed to parse forecast data." };
    }
}






app.MapGet("/test-geocoding", async () =>
{
    var geocodingService = new GeocodingService();
    string testLocation = "1600 Amphitheatre Parkway, Mountain View, CA"; // Example address
    string testApiKey = apiKey;

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




//private 
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
            string requestUri = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(location)}&key={api}";


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