using System.Net.Http.Json;
using System.Text.Json;

namespace Game.Features.Recording;

public sealed class RecordingUploadClient
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public RecordingUploadClient(HttpClient? httpClient = null)
    {
        if (httpClient is null)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(Highscores.RecordingApiConfig.ApiBaseUrl.TrimEnd('/') + "/")
            };
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = httpClient;
        }
    }

    public async Task SendAsync(string recordingName, string localFilePath, CancellationToken cancellationToken = default)
    {
        if (!RecordingNameSanitizer.TrySanitize(recordingName, out var sanitizedName, out var sanitizeError))
            throw new InvalidOperationException(sanitizeError);

        var recording = RecFileSerializer.Read(localFilePath);
        var payload = new RecordingUploadRequest
        {
            Name = sanitizedName,
            Recording = recording
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "api/recordings",
            payload,
            RecFileSerializer.JsonOptions,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(ExtractErrorMessage(body, response));
        }
    }

    private static string ExtractErrorMessage(string body, HttpResponseMessage response)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var errorProp))
                {
                    var message = errorProp.GetString();
                    if (!string.IsNullOrWhiteSpace(message))
                        return message;
                }
            }
            catch (JsonException)
            {
                return body;
            }

            return body;
        }

        return $"Upload failed ({(int)response.StatusCode} {response.ReasonPhrase}).";
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }
}
