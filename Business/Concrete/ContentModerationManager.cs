using Business.Abstract;
using Core.Utilities.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Business.Concrete
{
    public class ContentModerationManager : IContentModerationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ContentModerationManager> _logger;
        private readonly HttpClient _httpClient;
        private const string MODERATION_ENDPOINT = "https://api.openai.com/v1/moderations";

        public ContentModerationManager(
            IConfiguration configuration,
            ILogger<ContentModerationManager> logger,
            IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("OpenAI");
        }

        public async Task<IResult> CheckContentAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new SuccessResult();

            var apiKey = _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("OpenAI API key is not configured. Skipping content moderation.");
                return new SuccessResult();
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, MODERATION_ENDPOINT);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var payload = new { input = text };
                request.Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("OpenAI Moderation API error: {StatusCode} - {Content}",
                        response.StatusCode, responseContent);
                    // API hatası durumunda içeriği geçir (false positive önlemek için)
                    return new SuccessResult();
                }

                var moderationResponse = JsonSerializer.Deserialize<ModerationResponse>(responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (moderationResponse?.Results != null && moderationResponse.Results.Length > 0)
                {
                    var result = moderationResponse.Results[0];
                    if (result.Flagged)
                    {
                        var flaggedCategories = GetFlaggedCategories(result.Categories);
                        _logger.LogWarning("Content flagged by OpenAI Moderation. Categories: {Categories}",
                            string.Join(", ", flaggedCategories));

                        return new ErrorResult("Mesajınız uygunsuz içerik barındırmaktadır. Lütfen küfür, hakaret veya uygunsuz ifadeler kullanmayınız.");
                    }
                }

                return new SuccessResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during content moderation check");
                // Hata durumunda içeriği geçir
                return new SuccessResult();
            }
        }

        public async Task<IResult> CheckContentsAsync(params string[] texts)
        {
            foreach (var text in texts)
            {
                var result = await CheckContentAsync(text);
                if (!result.Success)
                    return result;
            }
            return new SuccessResult();
        }

        public async Task<IResult> CheckImageContentAsync(Microsoft.AspNetCore.Http.IFormFile file)
        {
            if (file == null || file.Length == 0)
                return new SuccessResult();

            var apiKey = _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("OpenAI API key is not configured. Skipping image moderation.");
                return new SuccessResult();
            }

            try
            {
                // Convert file to base64
                using var memoryStream = new System.IO.MemoryStream();
                await file.CopyToAsync(memoryStream);
                var base64Image = Convert.ToBase64String(memoryStream.ToArray());

                var mediaType = file.ContentType ?? "image/jpeg";

                var request = new HttpRequestMessage(HttpMethod.Post, MODERATION_ENDPOINT);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                // omni-moderation-latest model supports image input
                var payload = new
                {
                    model = "omni-moderation-latest",
                    input = new object[]
                    {
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = $"data:{mediaType};base64,{base64Image}"
                            }
                        }
                    }
                };

                request.Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("OpenAI Image Moderation API error: {StatusCode} - {Content}",
                        response.StatusCode, responseContent);
                    return new SuccessResult(); // Fail-open: API hatası durumunda yüklemeye izin ver
                }

                var moderationResponse = JsonSerializer.Deserialize<ModerationResponse>(responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (moderationResponse?.Results != null && moderationResponse.Results.Length > 0)
                {
                    var result = moderationResponse.Results[0];
                    if (result.Flagged)
                    {
                        var flaggedCategories = GetFlaggedCategories(result.Categories);
                        _logger.LogWarning(
                            "Image flagged by OpenAI Moderation. Categories: {Categories}, FileName: {FileName}",
                            string.Join(", ", flaggedCategories), file.FileName);

                        return new ErrorResult(
                            "Yüklediğiniz görsel uygunsuz içerik barındırmaktadır. Lütfen uygun bir görsel yükleyiniz.");
                    }
                }

                return new SuccessResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during image content moderation check for file: {FileName}", file.FileName);
                return new SuccessResult(); // Fail-open: hata durumunda yüklemeye izin ver
            }
        }

        public async Task<IResult> CheckImageContentAsync(byte[] imageBytes, string contentType, string fileName = "image")
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return new SuccessResult();

            var apiKey = _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("OpenAI API key is not configured. Skipping image moderation.");
                return new SuccessResult();
            }

            try
            {
                var base64Image = Convert.ToBase64String(imageBytes);
                var mediaType = contentType ?? "image/jpeg";

                var request = new HttpRequestMessage(HttpMethod.Post, MODERATION_ENDPOINT);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var payload = new
                {
                    model = "omni-moderation-latest",
                    input = new object[]
                    {
                        new
                        {
                            type = "image_url",
                            image_url = new { url = $"data:{mediaType};base64,{base64Image}" }
                        }
                    }
                };

                request.Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("OpenAI Image Moderation API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return new SuccessResult();
                }

                var moderationResponse = JsonSerializer.Deserialize<ModerationResponse>(responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (moderationResponse?.Results != null && moderationResponse.Results.Length > 0)
                {
                    var result = moderationResponse.Results[0];
                    if (result.Flagged)
                    {
                        var flaggedCategories = GetFlaggedCategories(result.Categories);
                        _logger.LogWarning(
                            "Image flagged by OpenAI Moderation. Categories: {Categories}, FileName: {FileName}",
                            string.Join(", ", flaggedCategories), fileName);

                        return new ErrorResult(
                            "Yüklediğiniz görsel uygunsuz içerik barındırmaktadır. Lütfen uygun bir görsel yükleyiniz.");
                    }
                }

                return new SuccessResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during image content moderation check for file: {FileName}", fileName);
                return new SuccessResult();
            }
        }

        private string[] GetFlaggedCategories(ModerationCategories categories)
        {
            var flagged = new System.Collections.Generic.List<string>();

            if (categories.Hate) flagged.Add("hate");
            if (categories.HateThreatening) flagged.Add("hate/threatening");
            if (categories.Harassment) flagged.Add("harassment");
            if (categories.HarassmentThreatening) flagged.Add("harassment/threatening");
            if (categories.SelfHarm) flagged.Add("self-harm");
            if (categories.SelfHarmIntent) flagged.Add("self-harm/intent");
            if (categories.SelfHarmInstructions) flagged.Add("self-harm/instructions");
            if (categories.Sexual) flagged.Add("sexual");
            if (categories.SexualMinors) flagged.Add("sexual/minors");
            if (categories.Violence) flagged.Add("violence");
            if (categories.ViolenceGraphic) flagged.Add("violence/graphic");

            return flagged.ToArray();
        }

        #region OpenAI Response Models
        private class ModerationResponse
        {
            public string Id { get; set; }
            public string Model { get; set; }
            public ModerationResult[] Results { get; set; }
        }

        private class ModerationResult
        {
            public bool Flagged { get; set; }
            public ModerationCategories Categories { get; set; }
            public ModerationCategoryScores CategoryScores { get; set; }
        }

        private class ModerationCategories
        {
            public bool Hate { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("hate/threatening")]
            public bool HateThreatening { get; set; }

            public bool Harassment { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("harassment/threatening")]
            public bool HarassmentThreatening { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("self-harm")]
            public bool SelfHarm { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("self-harm/intent")]
            public bool SelfHarmIntent { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("self-harm/instructions")]
            public bool SelfHarmInstructions { get; set; }

            public bool Sexual { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("sexual/minors")]
            public bool SexualMinors { get; set; }

            public bool Violence { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("violence/graphic")]
            public bool ViolenceGraphic { get; set; }
        }

        private class ModerationCategoryScores
        {
            public double Hate { get; set; }
            public double Harassment { get; set; }
            public double Sexual { get; set; }
            public double Violence { get; set; }
        }
        #endregion
    }
}
