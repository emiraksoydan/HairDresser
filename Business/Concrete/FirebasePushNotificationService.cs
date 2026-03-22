using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using Google.Apis.Auth.OAuth2;
using System.IO;

namespace Business.Concrete
{
    /// <summary>
    /// Firebase Cloud Messaging (FCM) push notification service
    /// Handles background notifications when app is closed
    /// </summary>
    public class FirebasePushNotificationService : IPushNotificationService
    {
        private readonly IUserFcmTokenDal _fcmTokenDal;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FirebasePushNotificationService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string? _projectId;
        private GoogleCredential? _credential;
        private readonly bool _isFirebaseEnabled;

        public FirebasePushNotificationService(
            IUserFcmTokenDal fcmTokenDal,
            IConfiguration configuration,
            ILogger<FirebasePushNotificationService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _fcmTokenDal = fcmTokenDal;
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("FCM");

            // Initialize Firebase Admin SDK with service account JSON (opsiyonel)
            try
            {
                var serviceAccountPath = _configuration["Firebase:ServiceAccountPath"];
                var firebaseEnabled = _configuration.GetValue<bool>("Firebase:Enabled", true);
                
                if (string.IsNullOrEmpty(serviceAccountPath) || !firebaseEnabled)
                {
                    _isFirebaseEnabled = false;
                    _logger.LogWarning("Firebase push notifications are disabled. Firebase:ServiceAccountPath is not configured or Firebase:Enabled is false.");
                    return;
                }

                // Resolve path (relative to Api project root or absolute)
                var basePath = AppContext.BaseDirectory;
                var fullPath = Path.IsPathRooted(serviceAccountPath) 
                    ? serviceAccountPath 
                    : Path.Combine(basePath, serviceAccountPath);

                if (!File.Exists(fullPath))
                {
                    _isFirebaseEnabled = false;
                    _logger.LogWarning($"Firebase service account file not found at: {fullPath}. Push notifications will be disabled.");
                    return;
                }

                // Load service account credentials and extract project ID
                using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
                {
                    _credential = GoogleCredential.FromStream(stream)
                        .CreateScoped(new[] { "https://www.googleapis.com/auth/firebase.messaging" });
                    
                    // Read project ID from JSON
                    var json = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(fullPath));
                    _projectId = json.GetProperty("project_id").GetString() ?? throw new InvalidOperationException("project_id not found in service account JSON");
                }

                _isFirebaseEnabled = true;
                _logger.LogInformation($"Firebase Admin SDK initialized successfully for project: {_projectId}");
            }
            catch (Exception ex)
            {
                _isFirebaseEnabled = false;
                _logger.LogWarning(ex, "Failed to initialize Firebase Admin SDK. Push notifications will be disabled.");
            }
        }

        public async Task<bool> SendPushNotificationAsync(Guid userId, NotificationDto notification)
        {
            // Firebase devre dışıysa sadece log yaz ve false döndür
            if (!_isFirebaseEnabled || _credential == null || string.IsNullOrEmpty(_projectId))
            {
                _logger.LogDebug($"Firebase push notifications are disabled. Skipping notification for user {userId}");
                return false;
            }

            try
            {
                // Get all active FCM tokens for the user
                var tokens = await _fcmTokenDal.GetActiveTokensByUserIdAsync(userId);
                if (tokens == null || tokens.Count == 0)
                {
                    _logger.LogWarning($"No active FCM tokens found for user {userId}");
                    return false;
                }

                var successCount = 0;
                var failedTokens = new List<string>();

                foreach (var token in tokens)
                {
                    try
                    {
                        // Get OAuth2 access token
                        if (_credential?.UnderlyingCredential is not ServiceAccountCredential serviceAccountCredential)
                        {
                            throw new InvalidOperationException("Invalid credential type");
                        }

                        var accessToken = await serviceAccountCredential.GetAccessTokenForRequestAsync();

                        // FCM v1 API format
                        var fcmMessage = new
                        {
                            message = new
                            {
                                token = token.FcmToken,
                                notification = new
                                {
                                    title = notification.Title,
                                    body = notification.Body ?? notification.Title,
                                },
                                data = new Dictionary<string, string>
                                {
                                    { "notificationId", notification.Id.ToString() },
                                    { "type", ((int)notification.Type).ToString() },
                                    { "appointmentId", notification.AppointmentId?.ToString() ?? "" },
                                    { "payload", notification.PayloadJson ?? "{}" },
                                    { "click_action", "FLUTTER_NOTIFICATION_CLICK" }
                                },
                                android = new
                                {
                                    priority = "high",
                                    notification = new
                                    {
                                        sound = "default"
                                        // channelId belirtilmez → FCM otomatik olarak
                                        // fcm_fallback_notification_channel kullanır
                                        // (Firebase SDK tarafından her cihazda otomatik oluşturulur)
                                    }
                                },
                                apns = new
                                {
                                    headers = new Dictionary<string, string>
                                    {
                                        { "apns-priority", "10" }
                                    },
                                    payload = new
                                    {
                                        aps = new
                                        {
                                            sound = "default",
                                            badge = 1,
                                            contentAvailable = true,
                                            mutableContent = true
                                        }
                                    }
                                }
                            }
                        };

                        // Send using FCM v1 REST API
                        var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send");
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                        request.Content = JsonContent.Create(fcmMessage);

                        var response = await _httpClient.SendAsync(request);
                        var responseContent = await response.Content.ReadAsStringAsync();
                        
                        if (response.IsSuccessStatusCode)
                        {
                            successCount++;
                            // Update token's last used timestamp
                            token.UpdatedAt = DateTime.UtcNow;
                            await _fcmTokenDal.Update(token);
                        }
                        else
                        {
                            _logger.LogWarning($"FCM send failed for token {token.FcmToken}: {responseContent}");
                            
                            // If token is invalid, deactivate it
                            var shouldDeactivate = response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                                response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                                responseContent.Contains("INVALID_ARGUMENT") ||
                                responseContent.Contains("NOT_FOUND") ||
                                responseContent.Contains("UNREGISTERED");
                            
                            if (shouldDeactivate)
                            {
                                failedTokens.Add(token.FcmToken);
                                await _fcmTokenDal.DeactivateTokenAsync(token.FcmToken);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error sending FCM notification to token {token.FcmToken}");
                        failedTokens.Add(token.FcmToken);
                    }
                }

                _logger.LogInformation($"FCM notification sent to {successCount}/{tokens.Count} devices for user {userId}");
                return successCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in SendPushNotificationAsync for user {userId}");
                return false;
            }
        }

        public async Task<bool> RegisterFcmTokenAsync(Guid userId, string fcmToken, string? deviceId = null, string? platform = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fcmToken))
                {
                    _logger.LogWarning($"Empty FCM token provided for user {userId}");
                    return false;
                }

                // Check if token already exists
                var existing = await _fcmTokenDal.GetByTokenAsync(fcmToken);
                if (existing != null)
                {
                    // Update existing token
                    existing.UserId = userId;
                    existing.IsActive = true;
                    existing.UpdatedAt = DateTime.UtcNow;
                    if (!string.IsNullOrWhiteSpace(deviceId))
                        existing.DeviceId = deviceId;
                    if (!string.IsNullOrWhiteSpace(platform))
                        existing.Platform = platform;
                    await _fcmTokenDal.Update(existing);
                    _logger.LogInformation($"FCM token updated for user {userId}");
                    return true;
                }

                // Create new token
                var newToken = new UserFcmToken
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    FcmToken = fcmToken,
                    DeviceId = deviceId,
                    Platform = platform,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _fcmTokenDal.Add(newToken);
                _logger.LogInformation($"FCM token registered for user {userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error registering FCM token for user {userId}");
                return false;
            }
        }

        public async Task<bool> UnregisterFcmTokenAsync(Guid userId, string fcmToken)
        {
            try
            {
                var token = await _fcmTokenDal.GetByTokenAsync(fcmToken);
                if (token != null && token.UserId == userId)
                {
                    await _fcmTokenDal.DeactivateTokenAsync(fcmToken);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error unregistering FCM token for user {userId}");
                return false;
            }
        }
    }
}

