using Houseflow.Api;

var builder = WebApplication.CreateBuilder(args);
var renderPort = Environment.GetEnvironmentVariable("PORT");
if (int.TryParse(renderPort, out var port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
var allowedOrigins = builder.Configuration["FRONTEND_ORIGINS"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0)
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
}));
builder.Services.AddHttpClient("Tuya", client => client.Timeout = TimeSpan.FromSeconds(25));
builder.Services.AddSingleton<TuyaCloudClient>();

var app = builder.Build();
app.UseCors();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/devices"))
    {
        var expected = builder.Configuration["HOUSEFLOW_API_KEY"];
        var supplied = context.Request.Headers.Authorization.ToString();
        var candidate = supplied.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? supplied[7..] : "";
        var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expected ?? "");
        var candidateBytes = System.Text.Encoding.UTF8.GetBytes(candidate);
        if (expectedBytes.Length < 32 || candidateBytes.Length != expectedBytes.Length ||
            !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expectedBytes, candidateBytes))
        {
            context.Response.StatusCode = expectedBytes.Length < 32 ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { detail = expectedBytes.Length < 32 ? "Houseflow API access is not configured." : "Enter a valid Houseflow API access key." });
            return;
        }
    }
    await next();
});

app.MapGet("/api/status", (TuyaCloudClient tuya) => Results.Ok(new ApiStatus(tuya.IsConfigured)));
app.MapGet("/api/devices", async (TuyaCloudClient tuya, CancellationToken ct) =>
{
    if (!tuya.IsConfigured) return Results.Problem("Tuya Cloud is not configured on the API host.", statusCode: 503);
    try { return Results.Ok(await tuya.GetDevicesAsync(ct)); }
    catch (TuyaApiException ex) { return Results.Problem(ex.Message, statusCode: ex.StatusCode); }
});
app.MapPost("/api/devices/{deviceId}/commands", async (string deviceId, DeviceCommand command, TuyaCloudClient tuya, CancellationToken ct) =>
{
    if (!tuya.IsConfigured) return Results.Problem("Tuya Cloud is not configured on the API host.", statusCode: 503);
    if (string.IsNullOrWhiteSpace(command.Code) || command.Value.ValueKind is not (System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False))
        return Results.BadRequest(new { message = "A switch code and boolean value are required." });
    try
    {
        var updated = await tuya.SetSwitchAsync(deviceId, command.Code, command.Value.GetBoolean(), ct);
        return Results.Ok(updated);
    }
    catch (TuyaApiException ex) { return Results.Problem(ex.Message, statusCode: ex.StatusCode); }
});

app.Run();

public sealed record ApiStatus(bool Configured);
public sealed record DeviceCommand(string Code, System.Text.Json.JsonElement Value);
