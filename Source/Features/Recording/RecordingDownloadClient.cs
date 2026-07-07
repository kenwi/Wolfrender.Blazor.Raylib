using Game.Features.Highscores;
using Game.Features.Highscores.Shared;

namespace Game.Features.Recording;

public sealed class RecordingDownloadClient
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public RecordingDownloadClient(HttpClient? httpClient = null)
    {
        if (httpClient is null)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(RecordingApiConfig.ApiBaseUrl.TrimEnd('/') + "/")
            };
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = httpClient;
        }
    }

    public async Task DownloadAsync(
        string levelId,
        int rank,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var sanitizedLevelId = ScoreSanitizer.SanitizeLevelId(levelId);
        if (string.IsNullOrEmpty(sanitizedLevelId))
            throw new InvalidOperationException("Level id is required.");

        if (rank < 1)
            throw new InvalidOperationException("Rank must be at least 1.");

        using var response = await _httpClient.GetAsync(
            $"api/scores/{Uri.EscapeDataString(sanitizedLevelId)}/recordings/{rank}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(ExtractErrorMessage(body, response));
        }

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var file = File.Create(destinationPath);
        await stream.CopyToAsync(file, cancellationToken);
    }

    private static string ExtractErrorMessage(string body, HttpResponseMessage response)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var errorProp))
                {
                    var message = errorProp.GetString();
                    if (!string.IsNullOrWhiteSpace(message))
                        return message;
                }
            }
            catch (System.Text.Json.JsonException)
            {
                return body;
            }

            return body;
        }

        return $"Download failed ({(int)response.StatusCode} {response.ReasonPhrase}).";
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }
}
