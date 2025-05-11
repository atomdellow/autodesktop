using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AutoDesktopApplication.Services.AI
{
    /// <summary>
    /// Service for interacting with Google's Gemini AI models
    /// </summary>
    public class GoogleAiService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GoogleAiService> _logger;
        private AiServiceConfig? _config; // CS8618: Made _config nullable
        
        public string ProviderName => "Google Gemini";
        
        public bool IsConfigured => !string.IsNullOrEmpty(Config?.ApiKey);
        
        public AiServiceConfig Config
        {
            get => _config ??= new AiServiceConfig
            {
                EndpointUrl = "https://generativelanguage.googleapis.com/v1",
                AdditionalSettings = new Dictionary<string, string>()
            };
            set => _config = value;
        }
        
        public GoogleAiService(HttpClient httpClient, ILogger<GoogleAiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }
        
        public async Task<bool> IsServiceAvailableAsync()
        {
            if (!IsConfigured)
                return false;
                
            try
            {
                // Make a simple models list request to verify API key works
                var url = $"{Config.EndpointUrl}/models?key={Config.ApiKey}";
                var response = await _httpClient.GetAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Google AI service availability: {Message}", ex.Message);
                return false;
            }
        }
        
        public async Task<List<string>> GetAvailableModelsAsync()
        {
            if (!IsConfigured)
                return new List<string>();
                
            try
            {
                var url = $"{Config.EndpointUrl}/models?key={Config.ApiKey}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                dynamic? modelData = JsonConvert.DeserializeObject(content); // CS8600: Handle potential null
                
                List<string> models = new List<string>();
                if (modelData?.models != null) // CS8602: Add null check
                {
                    foreach (var model in modelData.models)
                    {
                        models.Add((string)model.name);
                    }
                }
                
                // Return models that are suitable for our use case (those that handle vision)
                return models.FindAll(m => m.Contains("gemini"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available models from Google: {Message}", ex.Message);
                return new List<string>
                {
                    // Default models if API fails to retrieve them
                    "models/gemini-pro",
                    "models/gemini-pro-vision"
                };
            }
        }
        
        public async Task<string> ProcessTextAsync(string modelName, string prompt)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("Google AI API is not configured. Please set API key.");
                
            try
            {
                // Fix model name format if needed
                string formattedModelName = modelName;
                if (!modelName.StartsWith("models/"))
                    formattedModelName = $"models/{modelName}";
                
                // Create request body
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    }
                };
                
                // Serialize to JSON
                var jsonContent = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                // Make the API request
                var url = $"{Config.EndpointUrl}/{formattedModelName}:generateContent?key={Config.ApiKey}";
                var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                dynamic? responseObject = JsonConvert.DeserializeObject(responseContent); // CS8600: Handle potential null
                
                // Extract text from response
                string? result = responseObject?.candidates?[0]?.content?.parts?[0]?.text?.ToString(); // CS8602: Add null checks
                return result ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing text with Google AI: {Message}", ex.Message);
                throw;
            }
        }
        
        public async Task<(string decision, string explanation)> MakeDecisionAsync(string modelName, byte[] screenshot, string decisionCriteria)
        {
            try
            {
                string prompt = $"Analyze this screenshot and make a decision based on the following criteria: {decisionCriteria}. " +
                               "Return your answer in the format 'DECISION: [your one-word decision] REASONING: [your explanation]'";
                
                string response = await AnalyzeImageAsync(modelName, screenshot, prompt);
                
                // Parse the decision and reasoning from the response
                string decision = "unknown";
                string reasoning = response;
                
                // Try to extract DECISION and REASONING parts if properly formatted
                int decisionIndex = response.IndexOf("DECISION:", StringComparison.OrdinalIgnoreCase);
                int reasoningIndex = response.IndexOf("REASONING:", StringComparison.OrdinalIgnoreCase);
                
                if (decisionIndex >= 0 && reasoningIndex > decisionIndex)
                {
                    decision = response
                        .Substring(decisionIndex + 9, reasoningIndex - decisionIndex - 9)
                        .Trim();
                    
                    reasoning = response
                        .Substring(reasoningIndex + 10)
                        .Trim();
                }
                
                return (decision, reasoning);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error making decision with Google AI: {Message}", ex.Message);
                throw;
            }
        }
        
        public async Task<string> AnalyzeImageAsync(string modelName, byte[] imageBytes, string prompt)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("Google AI API is not configured. Please set API key.");
                
            try
            {
                // Fix model name format if needed
                string formattedModelName = modelName;
                if (!modelName.StartsWith("models/"))
                    formattedModelName = $"models/{modelName}";
                
                // If it's not a vision model, use gemini-pro-vision
                if (!formattedModelName.Contains("vision"))
                    formattedModelName = "models/gemini-pro-vision";
                
                // Convert image bytes to base64
                string base64Image = Convert.ToBase64String(imageBytes);
                
                // Create request body with multimodal content (text + image)
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new object[]
                            {
                                new { text = prompt },
                                new 
                                { 
                                    inline_data = new
                                    {
                                        mime_type = "image/jpeg",
                                        data = base64Image
                                    }
                                }
                            }
                        }
                    }
                };
                
                // Serialize to JSON
                var jsonContent = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                // Make the API request
                var url = $"{Config.EndpointUrl}/{formattedModelName}:generateContent?key={Config.ApiKey}";
                var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                dynamic? responseObject = JsonConvert.DeserializeObject(responseContent); // CS8600: Handle potential null
                
                // Extract text from response
                string? result = responseObject?.candidates?[0]?.content?.parts?[0]?.text?.ToString(); // CS8602: Add null checks
                return result ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing image with Google AI: {Message}", ex.Message);
                throw;
            }
        }
        
        public async Task<List<DetectedObject>> DetectObjectsAsync(string modelName, byte[] imageData)
        {
            var results = new List<DetectedObject>();
            
            try
            {
                // Google Gemini doesn't have a direct object detection API, so we use vision model and prompt it
                string prompt = "Detect objects in this image. For each object, provide its name, position (x, y coordinates as normalized values from 0 to 1), width and height (also normalized from 0 to 1), and confidence score. Format as a JSON array.";
                
                string response = await AnalyzeImageAsync(modelName, imageData, prompt);
                
                // Try to extract JSON objects from the response
                try
                {
                    if (response.Contains("[") && response.Contains("]"))
                    {
                        int startIdx = response.IndexOf('[');
                        int endIdx = response.LastIndexOf(']') + 1;
                        string jsonArray = response.Substring(startIdx, endIdx - startIdx);
                        
                        // Try to parse the JSON array
                        var objects = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(jsonArray);
                        
                        if (objects != null) // CS8601: Add null check for objects
                        {
                            foreach (var obj in objects)
                            {
                                try
                                {
                                    results.Add(new DetectedObject
                                    {
                                        Label = obj.ContainsKey("name") && obj["name"] != null ? obj["name"].ToString() ?? "unknown" : "unknown",
                                        X = obj.ContainsKey("x") && obj["x"] != null ? Convert.ToSingle(obj["x"]) : 0,
                                        Y = obj.ContainsKey("y") && obj["y"] != null ? Convert.ToSingle(obj["y"]) : 0,
                                        Width = obj.ContainsKey("width") && obj["width"] != null ? Convert.ToSingle(obj["width"]) : 0,
                                        Height = obj.ContainsKey("height") && obj["height"] != null ? Convert.ToSingle(obj["height"]) : 0,
                                        Confidence = obj.ContainsKey("confidence") && obj["confidence"] != null ? Convert.ToSingle(obj["confidence"]) : 0.5f
                                    });
                                }
                                catch
                                {
                                    // Skip malformed objects
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing object detection response: {Message}", ex.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting objects: {Message}", ex.Message);
            }
            
            return results;
        }
    }
}