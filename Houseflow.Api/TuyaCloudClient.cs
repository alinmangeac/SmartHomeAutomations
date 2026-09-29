using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Houseflow.Api;

public sealed class TuyaCloudClient(IConfiguration configuration, IHttpClientFactory clientFactory)
{
    private readonly HttpClient http = clientFactory.CreateClient("Tuya");
    private readonly string _accessId = configuration["TUYA_ACCESS_ID"] ?? "";
    private readonly string _accessSecret = configuration["TUYA_ACCESS_SECRET"] ?? "";
    private readonly string _userId = configuration["TUYA_UID"] ?? "";
    private readonly string _baseUrl = (configuration["TUYA_BASE_URL"] ?? "https://openapi.tuyaus.com").TrimEnd('/');
    private string? _token;
    private DateTimeOffset _tokenExpiresAt;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_accessId) &&
                                !string.IsNullOrWhiteSpace(_accessSecret) &&
                                !string.IsNullOrWhiteSpace(_userId);

    public async Task<IReadOnlyList<TuyaDevice>> GetDevicesAsync(CancellationToken ct)
    {
        var root = await RequestAsync(HttpMethod.Get, $"/v1.0/users/{Uri.EscapeDataString(_userId)}/devices", null, true, ct);
        var devices = new List<TuyaDevice>();
        if (root.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Array)
        {
            foreach (var device in result.EnumerateArray())
            {
                var id = Text(device, "id");
                if (id.Length == 0) continue;
                var functions = await GetFunctionsAsync(id, ct);
                var switchCode = functions.Where(f => f.Code.StartsWith("switch", StringComparison.OrdinalIgnoreCase) &&
                    f.Type.Equals("bool", StringComparison.OrdinalIgnoreCase)).Select(f => f.Code).FirstOrDefault();
                var status = device.TryGetProperty("status", out var statusItems) && statusItems.ValueKind == JsonValueKind.Array
                    ? statusItems.EnumerateArray().ToDictionary(item => Text(item, "code"), item => item.TryGetProperty("value", out var val) ? val.Clone() : default, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                var isOn = switchCode is not null && status.TryGetValue(switchCode, out var state) && state.ValueKind == JsonValueKind.True;
                devices.Add(new TuyaDevice(id, Text(device, "name", "Tuya device"), Text(device, "category"), Text(device, "room"), switchCode, isOn));
            }
        }
        return devices;
    }

    public async Task<TuyaDevice> SetSwitchAsync(string deviceId, string code, bool value, CancellationToken ct)
    {
        var devices = await GetDevicesAsync(ct);
        var device = devices.FirstOrDefault(d => d.Id == deviceId) ?? throw new TuyaApiException("That device is not linked to this Tuya account.", 404);
        if (device.SwitchCode is null || !device.SwitchCode.Equals(code, StringComparison.Ordinal))
            throw new TuyaApiException("This device does not expose that switch control.", 400);
        var body = JsonSerializer.Serialize(new { commands = new[] { new { code, value } } });
        await RequestAsync(HttpMethod.Post, $"/v1.0/iot-03/devices/{Uri.EscapeDataString(deviceId)}/commands", body, true, ct);
        return device with { IsOn = value };
    }

    private async Task<List<(string Code, string Type)>> GetFunctionsAsync(string deviceId, CancellationToken ct)
    {
        var root = await RequestAsync(HttpMethod.Get, $"/v1.0/iot-03/devices/{Uri.EscapeDataString(deviceId)}/functions", null, true, ct);
        if (!root.TryGetProperty("result", out var result) || !result.TryGetProperty("functions", out var functions) || functions.ValueKind != JsonValueKind.Array)
            return [];
        return functions.EnumerateArray().Select(item => (Text(item, "code"), Text(item, "type"))).Where(f => f.Item1.Length > 0).ToList();
    }

    private async Task<JsonElement> RequestAsync(HttpMethod method, string path, string? body, bool withToken, CancellationToken ct)
    {
        var token = withToken ? await GetTokenAsync(ct) : "";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var bodyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body ?? "")));
        var stringToSign = $"{method.Method}\n{bodyHash}\n\n{path}";
        var signInput = withToken ? _accessId + token + timestamp + stringToSign : _accessId + timestamp + stringToSign;
        var signature = Hmac(signInput);
        using var request = new HttpRequestMessage(method, _baseUrl + path);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        request.Headers.TryAddWithoutValidation("client_id", _accessId);
        request.Headers.TryAddWithoutValidation("t", timestamp);
        request.Headers.TryAddWithoutValidation("sign_method", "HMAC-SHA256");
        request.Headers.TryAddWithoutValidation("sign", signature);
        if (withToken) request.Headers.TryAddWithoutValidation("access_token", token);
        using var response = await http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { throw new TuyaApiException("Tuya Cloud returned an unreadable response. Check the API region and project settings.", 502); }
        using (doc)
        {
            var root = doc.RootElement.Clone();
            if (!response.IsSuccessStatusCode || !root.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True)
            {
                var code = Text(root, "code");
                var message = Text(root, "msg");
                throw new TuyaApiException(code == "1010" || code == "1011"
                    ? "Tuya rejected the credentials or signature. Check the Cloud project access ID, secret, API permissions, and data center."
                    : string.IsNullOrWhiteSpace(message) ? "Tuya Cloud request failed. Check the linked account and Cloud project permissions." : $"Tuya Cloud: {message}",
                    response.StatusCode == HttpStatusCode.Unauthorized ? 502 : 502);
            }
            return root;
        }
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_token is not null && _tokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1)) return _token;
        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_token is not null && _tokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1)) return _token;
            var root = await RequestAsync(HttpMethod.Get, "/v1.0/token?grant_type=1", null, false, ct);
            if (!root.TryGetProperty("result", out var result) || !result.TryGetProperty("access_token", out var token))
                throw new TuyaApiException("Tuya Cloud did not return an access token.", 502);
            _token = token.GetString();
            var expires = result.TryGetProperty("expire_time", out var expire) && expire.TryGetInt32(out var seconds) ? seconds : 3600;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expires);
            return _token ?? throw new TuyaApiException("Tuya Cloud returned an empty access token.", 502);
        }
        finally { _tokenLock.Release(); }
    }

    private string Hmac(string input) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(_accessSecret), Encoding.UTF8.GetBytes(input)));
    private static string Text(JsonElement element, string property, string fallback = "") => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;
}

public sealed record TuyaDevice(string Id, string Name, string Category, string Room, string? SwitchCode, bool IsOn);
public sealed class TuyaApiException(string message, int statusCode) : Exception(message) { public int StatusCode { get; } = statusCode; }
