// Services/VisionApiService.cs
using System;
using System.Net.Http;
using System.Net.Http.Json; // Requires System.Net.Http.Json NuGet package
using System.Threading.Tasks;
using AutoDesktopApplication.Models; // Assuming VisionServiceModels are in this namespace
using System.Diagnostics; // For Debug.WriteLine
using System.Collections.Generic; // For List<T>
using System.Text.Json; // For JsonSerializer

namespace AutoDesktopApplication.Services
{
    public class VisionApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl = "http://localhost:5001"; 

        public VisionApiService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30); 
        }

        public async Task<DetectionResponse?> DetectObjectsAsync(DetectionRequest requestData)
        {
            if (requestData == null || string.IsNullOrEmpty(requestData.Screenshot))
            {
                Debug.WriteLine("DetectObjectsAsync: requestData or requestData.Screenshot is null or empty.");
                return new DetectionResponse { Detections = new List<Detection>(), Error = "Screenshot data was empty." };
            }

            var endpoint = $"{_apiBaseUrl}/detect";
            Debug.WriteLine($"VisionApiService: Sending request to {endpoint}");

            try
            {
                HttpResponseMessage response = await _httpClient.PostAsJsonAsync(endpoint, requestData);

                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine("VisionApiService: Received successful response from API.");
                    if (response.Content != null)
                    {
                        string rawJsonResponse = await response.Content.ReadAsStringAsync(); // Log raw response
                        Debug.WriteLine($"VisionApiService: Raw JSON response: {rawJsonResponse}");

                        // Deserialize using System.Text.Json
                        var detectionResponse = JsonSerializer.Deserialize<DetectionResponse>(rawJsonResponse, 
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (detectionResponse != null)
                        {
                            // Log the deserialized object (or parts of it)
                            Debug.WriteLine($"VisionApiService: Deserialized response object: Error='{detectionResponse.Error}', Detections Count='{(detectionResponse.Detections?.Count ?? 0)}'");
                            if (detectionResponse.Detections != null && detectionResponse.Detections.Any())
                            {
                                foreach (var det in detectionResponse.Detections)
                                {
                                    Debug.WriteLine($"VisionApiService: Detection - Label: {det.Label}, Conf: {det.Confidence}, X: {det.X}, Y: {det.Y}, W: {det.Width}, H: {det.Height}");
                                }
                            }

                            if (detectionResponse.Detections == null)                            
                            {
                                Debug.WriteLine("VisionApiService: Deserialized response but Detections list is null. Initializing empty list.");
                                detectionResponse.Detections = new List<Detection>();
                            }
                            else
                            {
                                Debug.WriteLine($"VisionApiService: Successfully deserialized response. Detected {detectionResponse.Detections.Count} objects.");
                            }
                        }
                        else
                        {
                             Debug.WriteLine("VisionApiService: Deserialized response is null.");
                             return new DetectionResponse { Detections = new List<Detection>(), Error = "Failed to deserialize API response." };
                        }
                        return detectionResponse;
                    }
                    else
                    {
                        Debug.WriteLine("VisionApiService: Response content was null.");
                        return new DetectionResponse { Detections = new List<Detection>(), Error = "API response content was null." };
                    }
                }
                else
                {
                    string errorContent = response.Content != null ? await response.Content.ReadAsStringAsync() : "No error content.";
                    Debug.WriteLine($"VisionApiService: API Error: {response.StatusCode} - {errorContent}");
                    
                    if (response.Content != null)
                    {
                        try 
                        {
                            var errorResponse = await response.Content.ReadFromJsonAsync<DetectionResponse>();
                            if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.Error))
                            {
                                return errorResponse;
                            }
                        }
                        catch(Exception ex)
                        {
                             Debug.WriteLine($"VisionApiService: Could not deserialize error response: {ex.Message}");
                        }
                    }
                    return new DetectionResponse { Detections = new List<Detection>(), Error = $"API request failed with status {response.StatusCode}. Details: {errorContent}" };
                }
            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"VisionApiService: HttpRequestException during API call to {endpoint}: {httpEx.Message}");
                if (httpEx.InnerException != null)
                {
                    Debug.WriteLine($"VisionApiService: Inner HttpRequestException: {httpEx.InnerException.Message}");
                }
                if (httpEx.InnerException is System.Net.Sockets.SocketException socketEx && socketEx.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionRefused)
                {
                    Debug.WriteLine("VisionApiService: Connection refused. Ensure the Python Flask API is running at " + _apiBaseUrl);
                     return new DetectionResponse { Detections = new List<Detection>(), Error = "Connection to Vision API refused. Is the API running?" };
                }
                 return new DetectionResponse { Detections = new List<Detection>(), Error = $"HTTP request error: {httpEx.Message}" };
            }
            catch (TaskCanceledException tex)
            {
                Debug.WriteLine($"VisionApiService: API call to {endpoint} timed out: {tex.Message}");
                return new DetectionResponse { Detections = new List<Detection>(), Error = "API call timed out." };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VisionApiService: Unexpected exception during API call to {endpoint}: {ex.Message}");
                return new DetectionResponse { Detections = new List<Detection>(), Error = $"An unexpected error occurred: {ex.Message}" };
            }
        }
    }
}
