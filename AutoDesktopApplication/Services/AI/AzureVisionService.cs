using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace AutoDesktopApplication.Services.AI
{
    /// <summary>
    /// Service for interacting with Azure Computer Vision services
    /// </summary>
    public class AzureVisionService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AzureVisionService> _logger;
        private AiServiceConfig? _config;
        
        public string ProviderName => "Azure Vision";
        
        public bool IsConfigured => !string.IsNullOrEmpty(Config?.ApiKey) && !string.IsNullOrEmpty(Config?.EndpointUrl);
        
        public AiServiceConfig Config
        {
            get => _config ??= new AiServiceConfig();
            set => _config = value;
        }
        
        public AzureVisionService(HttpClient httpClient, ILogger<AzureVisionService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _config = new AiServiceConfig();
        }
        
        /// <summary>
        /// Checks if the Azure Vision service is available
        /// </summary>
        public async Task<bool> IsServiceAvailableAsync()
        {
            if (!IsConfigured)
                return false;
                
            try
            {
                // Just check if we can connect to the endpoint
                var request = new HttpRequestMessage(HttpMethod.Head, Config.EndpointUrl);
                request.Headers.Add("Ocp-Apim-Subscription-Key", Config.ApiKey);
                
                var response = await _httpClient.SendAsync(request);
                
                // Even if we get a 401 or 404, the service is online
                return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Unauthorized;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to Azure Vision service");
                return false;
            }
        }
        
        /// <summary>
        /// Gets available models from Azure Vision
        /// </summary>
        public Task<List<string>> GetAvailableModelsAsync() // Removed async
        {
            // Azure Vision API doesn't have a model list endpoint like OpenAI
            // Return the available capabilities instead
            return Task.FromResult(new List<string> // Added Task.FromResult
            {
                "image-analysis",
                "ocr",
                "object-detection",
                "face-detection",
                "spatial-analysis"
            });
        }
        
        /// <summary>
        /// Makes a vision-based decision using Azure Computer Vision
        /// </summary>
        public async Task<(string decision, string explanation)> MakeDecisionAsync(
            string modelName, byte[] screenshot, string decisionCriteria)
        {
            try
            {
                if (!IsConfigured)
                    return ("error", "Azure Vision service not configured");
                
                // Use general image analysis first to understand the content
                var imageAnalysis = await AnalyzeImageAsync(screenshot);
                
                // Then detect objects
                var detectedObjects = await DetectObjectsAsync("object-detection", screenshot);
                
                // If we're looking for text, also run OCR
                string extractedText = "";
                if (decisionCriteria.Contains("text") || decisionCriteria.Contains("read"))
                {
                    extractedText = await ExtractTextAsync(screenshot);
                }
                
                // Combine all analysis results to form a decision
                string combinedAnalysis = $"Image analysis: {imageAnalysis}";
                
                if (!string.IsNullOrEmpty(extractedText))
                {
                    combinedAnalysis += $"\nExtracted text: {extractedText}";
                }
                
                if (detectedObjects.Count > 0)
                {
                    combinedAnalysis += $"\nDetected objects: {string.Join(", ", detectedObjects.Select(o => o.Label))}";
                }
                
                // Make a basic decision based on the criteria and analysis
                string decision = "unknown";
                
                // Simple keyword matching for decision making
                string criteriaLower = decisionCriteria.ToLower();
                string analysisLower = combinedAnalysis.ToLower();
                
                if (criteriaLower.Contains("found") || criteriaLower.Contains("detect") || criteriaLower.Contains("identify"))
                {
                    string[] keyItems = criteriaLower
                        .Replace("found", "")
                        .Replace("detect", "")
                        .Replace("identify", "")
                        .Replace("if", "")
                        .Replace("is", "")
                        .Replace("there", "")
                        .Replace("are", "")
                        .Replace("any", "")
                        .Split(new[] { ' ', ',', '.', '?' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    foreach (var item in keyItems)
                    {
                        if (analysisLower.Contains(item) && item.Length > 3)
                        {
                            decision = "found";
                            break;
                        }
                    }
                    
                    if (decision == "unknown")
                    {
                        decision = "not_found";
                    }
                }
                
                return (decision, combinedAnalysis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error making decision with Azure Vision");
                return ("error", $"Error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Process text with Azure Cognitive Services
        /// </summary>
        public Task<string> ProcessTextAsync(string modelName, string prompt) // Removed async
        {
            // Azure Vision is primarily for image processing, not text processing
            return Task.FromResult("Azure Vision service is designed for image analysis, not text processing."); // Added Task.FromResult
        }
        
        /// <summary>
        /// Detects objects in an image using Azure Computer Vision
        /// </summary>
        public async Task<List<DetectedObject>> DetectObjectsAsync(string modelName, byte[] imageData)
        {
            try
            {
                if (!IsConfigured || imageData == null || imageData.Length == 0)
                    return new List<DetectedObject>();
                
                // Endpoint for object detection
                string objectDetectionEndpoint = $"{Config.EndpointUrl}/vision/v3.2/detect";
                
                // Create the request
                var content = new ByteArrayContent(imageData);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                
                var request = new HttpRequestMessage(HttpMethod.Post, objectDetectionEndpoint);
                request.Headers.Add("Ocp-Apim-Subscription-Key", Config.ApiKey);
                request.Content = content;
                
                // Send the request
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                dynamic? jsonResponse = JsonConvert.DeserializeObject(responseContent);
                
                // Parse the response to extract object detections
                var detectedObjects = new List<DetectedObject>();
                
                if (jsonResponse?.objects != null)
                {
                    foreach (var obj in jsonResponse.objects)
                    {
                        try
                        {
                            // Get the bounding box values
                            // Fix: Use proper syntax for dynamic property access to avoid CS0826 error
                            double x = (double)(obj?.rectangle?.x ?? 0) / (double)(jsonResponse?.metadata?.width ?? 1); // Added null checks and default values
                            double y = (double)(obj?.rectangle?.y ?? 0) / (double)(jsonResponse?.metadata?.height ?? 1); // Added null checks and default values
                            double width = (double)(obj?.rectangle?.w ?? 0) / (double)(jsonResponse?.metadata?.width ?? 1); // Added null checks and default values
                            double height = (double)(obj?.rectangle?.h ?? 0) / (double)(jsonResponse?.metadata?.height ?? 1); // Added null checks and default values
                            
                            // Fix: Use proper syntax for accessing nullable dynamic properties
                            string label = obj?["@object"]?.ToString() ?? "unknown"; // Changed to obj?["@object"] and added null coalescing
                            double confidence = (double)(obj?.confidence ?? 0.0); // Added null coalescing
                            
                            detectedObjects.Add(new DetectedObject
                            {
                                Label = label,
                                Confidence = (float)confidence,
                                X = (float)x,
                                Y = (float)y,
                                Width = (float)width,
                                Height = (float)height
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse object detection result");
                        }
                    }
                }
                
                return detectedObjects;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting objects with Azure Vision");
                return new List<DetectedObject>();
            }
        }
        
        /// <summary>
        /// Analyzes an image using Azure Computer Vision to get descriptions and tags
        /// </summary>
        private async Task<string> AnalyzeImageAsync(byte[] imageData)
        {
            try
            {
                string analyzeEndpoint = $"{Config.EndpointUrl}/vision/v3.2/analyze?visualFeatures=Categories,Description,Tags,Objects,Color";
                
                var content = new ByteArrayContent(imageData);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                
                var request = new HttpRequestMessage(HttpMethod.Post, analyzeEndpoint);
                request.Headers.Add("Ocp-Apim-Subscription-Key", Config.ApiKey);
                request.Content = content;
                
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                dynamic? jsonResponse = JsonConvert.DeserializeObject(responseContent);
                
                // Extract descriptions from the response
                var descriptions = new List<string>();
                if (jsonResponse?.description?.captions != null)
                {
                    foreach (var caption in jsonResponse.description.captions)
                    {
                        descriptions.Add(caption.text.ToString());
                    }
                }
                
                // Extract tags from the response
                var tags = new List<string>();
                if (jsonResponse?.tags != null)
                {
                    foreach (var tag in jsonResponse.tags)
                    {
                        if ((double)tag.confidence > 0.5)
                            tags.Add(tag.name.ToString());
                    }
                }
                
                // Combine the results
                string result = string.Join(". ", descriptions);
                if (tags.Count > 0)
                {
                    result += $" Tags: {string.Join(", ", tags)}";
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing image with Azure Vision");
                return "Failed to analyze image";
            }
        }
        
        /// <summary>
        /// Extracts text from an image using Azure Computer Vision OCR
        /// </summary>
        private async Task<string> ExtractTextAsync(byte[] imageData)
        {
            try
            {
                string ocrEndpoint = $"{Config.EndpointUrl}/vision/v3.2/read/analyze";
                
                var content = new ByteArrayContent(imageData);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                
                var request = new HttpRequestMessage(HttpMethod.Post, ocrEndpoint);
                request.Headers.Add("Ocp-Apim-Subscription-Key", Config.ApiKey);
                request.Content = content;
                
                // Submit the OCR request
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                // Get the operation-location URL to check status
                string? operationLocation = response.Headers.GetValues("Operation-Location").FirstOrDefault(); // CS8600: Made operationLocation nullable
                if (string.IsNullOrEmpty(operationLocation))
                {
                    return "Failed to get OCR operation URL";
                }
                
                // Poll until the OCR operation is complete
                bool isOperationComplete = false;
                dynamic? ocrResult = null;
                int maxRetries = 10;
                int retryCount = 0;
                
                while (!isOperationComplete && retryCount < maxRetries)
                {
                    // Wait before checking status
                    await Task.Delay(1000);
                    
                    // Check operation status
                    var statusRequest = new HttpRequestMessage(HttpMethod.Get, operationLocation);
                    statusRequest.Headers.Add("Ocp-Apim-Subscription-Key", Config.ApiKey);
                    
                    var statusResponse = await _httpClient.SendAsync(statusRequest);
                    statusResponse.EnsureSuccessStatusCode();
                    
                    var statusContent = await statusResponse.Content.ReadAsStringAsync();
                    dynamic? statusResult = JsonConvert.DeserializeObject(statusContent);
                    
                    string? status = statusResult?.status?.ToString(); // CS8600: Made status nullable
                    
                    if (status?.ToLower() == "succeeded") // Added null conditional for ToLower()
                    {
                        isOperationComplete = true;
                        ocrResult = statusResult;
                    }
                    else if (status?.ToLower() == "failed") // Added null conditional for ToLower()
                    {
                        return "OCR operation failed";
                    }
                    
                    retryCount++;
                }
                
                if (!isOperationComplete || ocrResult == null)
                {
                    return "OCR operation timed out";
                }
                
                // Extract the text from the result
                var textLines = new List<string>();
                
                if (ocrResult?.analyzeResult?.readResults != null) // CS8602: Added null conditional for ocrResult
                {
                    foreach (var readResult in ocrResult.analyzeResult.readResults)
                    {
                        if (readResult.lines != null)
                        {
                            foreach (var line in readResult.lines)
                            {
                                textLines.Add(line.text.ToString());
                            }
                        }
                    }
                }
                
                return string.Join(" ", textLines);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text with Azure Vision OCR");
                return "Failed to extract text";
            }
        }
    }
}