using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AutoDesktopApplication.Services.AI
{
    /// <summary>
    /// Service for interacting with Google's Gemini AI models
    /// </summary>
    public class GeminiService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeminiService> _logger;
        private AiServiceConfig? _config; // CS8618: Made _config nullable
        private readonly string _baseUrl = "https://generativelanguage.googleapis.com/v1beta";
        
        public string ProviderName => "Google AI Gemini";
        
        public bool IsConfigured => !string.IsNullOrEmpty(Config?.ApiKey);
        
        public AiServiceConfig Config
        {
            get => _config ??= new AiServiceConfig();
            set => _config = value;
        }
        
        public GeminiService(HttpClient httpClient, ILogger<GeminiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }
        
        /// <summary>
        /// Checks if the Gemini service is available and properly configured
        /// </summary>
        public async Task<bool> IsServiceAvailableAsync()
        {
            if (!IsConfigured)
                return false;
                
            try
            {
                var models = await GetAvailableModelsAsync();
                return models.Count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Gemini service availability: {Message}", ex.Message);
                return false;
            }
        }
        
        /// <summary>
        /// Gets a list of available Gemini models
        /// </summary>
        public async Task<List<string>> GetAvailableModelsAsync()
        {
            var models = new List<string>
            {
                "gemini-pro",
                "gemini-pro-vision"
            };
            
            if (!IsConfigured)
                return models;
                
            try
            {
                string url = $"{_baseUrl}/models?key={Config.ApiKey}";
                
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(content);
                
                var modelsArray = jsonResponse["models"] as JArray;
                
                if (modelsArray != null)
                {
                    models.Clear();
                    foreach (var model in modelsArray)
                    {
                        string? name = model?["name"]?.ToString(); // CS8600: Handle potential null from JToken
                        if (!string.IsNullOrEmpty(name))
                        {
                            // Extract just the model ID from the full path
                            string modelId = name.Substring(name.LastIndexOf('/') + 1);
                            models.Add(modelId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available Gemini models: {Message}", ex.Message);
            }
            
            return models;
        }
        
        /// <summary>
        /// Process text with a Gemini model
        /// </summary>
        public async Task<string> ProcessTextAsync(string modelName, string prompt)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("Gemini service is not configured properly.");
                
            try
            {
                // Ensure we have the correct model format
                if (!modelName.Contains("/"))
                {
                    modelName = $"models/{modelName}";
                }
                
                string url = $"{_baseUrl}/{modelName}:generateContent?key={Config.ApiKey}";
                
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        topK = 40,
                        topP = 0.95,
                        maxOutputTokens = 2048,
                    }
                };
                
                var jsonContent = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(responseContent);
                
                // Extract the generated text from the response
                var candidates = jsonResponse?["candidates"] as JArray; // CS8602: Handle potential null
                if (candidates != null && candidates.Count > 0)
                {
                    var content0 = candidates[0]?["content"]; // CS8602: Handle potential null
                    var parts = content0?["parts"] as JArray; // CS8602: Handle potential null
                    if (parts != null && parts.Count > 0)
                    {
                        return parts[0]?["text"]?.ToString() ?? string.Empty; // CS8602: Handle potential null
                    }
                }
                
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing text with Gemini: {Message}", ex.Message);
                throw;
            }
        }
        
        /// <summary>
        /// Make a decision using image data and decision criteria with Gemini Pro Vision
        /// </summary>
        public async Task<(string decision, string explanation)> MakeDecisionAsync(string modelName, byte[] screenshot, string decisionCriteria)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("Gemini service is not configured properly.");
                
            try
            {
                // For Gemini, we need to use the vision-capable model
                if (!modelName.Contains("vision"))
                {
                    modelName = "gemini-pro-vision";
                }
                
                // Ensure we have the correct model format
                if (!modelName.Contains("/"))
                {
                    modelName = $"models/{modelName}";
                }
                
                string url = $"{_baseUrl}/{modelName}:generateContent?key={Config.ApiKey}";
                
                // Convert image to base64
                string base64Image = Convert.ToBase64String(screenshot);
                
                // Create a multimodal prompt with the image and text
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = $"Analyze this image and make a decision based on the following criteria: {decisionCriteria}. Return your answer in the format 'DECISION: [your one-word decision] REASONING: [your explanation]'" },
                                new
                                {
                                    inlineData = new
                                    {
                                        mimeType = "image/jpeg",
                                        data = base64Image
                                    }
                                }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.4,
                        topK = 32,
                        topP = 1,
                        maxOutputTokens = 1024,
                    }
                };
                
                var jsonContent = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(responseContent);
                
                // Extract the generated text from the response
                string generatedText = string.Empty;
                var candidates = jsonResponse?["candidates"] as JArray; // CS8602: Handle potential null
                if (candidates != null && candidates.Count > 0)
                {
                    var content0 = candidates[0]?["content"]; // CS8602: Handle potential null
                    var parts = content0?["parts"] as JArray; // CS8602: Handle potential null
                    if (parts != null && parts.Count > 0)
                    {
                        generatedText = parts[0]?["text"]?.ToString() ?? string.Empty; // CS8602: Handle potential null
                    }
                }
                
                // Parse the decision and reasoning
                string decision = "unknown";
                string reasoning = generatedText;
                
                // Try to extract DECISION and REASONING parts if properly formatted
                int decisionIndex = generatedText.IndexOf("DECISION:", StringComparison.OrdinalIgnoreCase);
                int reasoningIndex = generatedText.IndexOf("REASONING:", StringComparison.OrdinalIgnoreCase);
                
                if (decisionIndex >= 0 && reasoningIndex > decisionIndex)
                {
                    decision = generatedText
                        .Substring(decisionIndex + 9, reasoningIndex - decisionIndex - 9)
                        .Trim();
                    
                    reasoning = generatedText
                        .Substring(reasoningIndex + 10)
                        .Trim();
                }
                
                return (decision, reasoning);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error making decision with Gemini: {Message}", ex.Message);
                return ("error", $"Failed to process image: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Process image with Gemini AI model for visual analysis and object detection
        /// </summary>
        public async Task<List<DetectedObject>> DetectObjectsAsync(string modelName, byte[] imageData)
        {
            var results = new List<DetectedObject>();
            
            if (!IsConfigured)
                throw new InvalidOperationException("Gemini service is not configured properly.");
                
            try
            {
                // For Gemini, we need to use the vision-capable model
                if (!modelName.Contains("vision"))
                {
                    modelName = "gemini-pro-vision";
                }
                
                // Ensure we have the correct model format
                if (!modelName.Contains("/"))
                {
                    modelName = $"models/{modelName}";
                }
                
                string url = $"{_baseUrl}/{modelName}:generateContent?key={Config.ApiKey}";
                
                // Convert image to base64
                string base64Image = Convert.ToBase64String(imageData);
                
                // Create object detection prompt
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = "Detect objects in this image. For each object, provide its label, position (x, y coordinates as normalized 0-1 values), width and height (as normalized 0-1 values), and confidence score. Format the response as a JSON array with objects having properties: label, x, y, width, height, confidence." },
                                new
                                {
                                    inlineData = new
                                    {
                                        mimeType = "image/jpeg",
                                        data = base64Image
                                    }
                                }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.2,
                        topK = 40,
                        topP = 1,
                        maxOutputTokens = 2048,
                    }
                };
                
                var jsonContent = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(responseContent);
                
                // Extract the generated text from the response
                string generatedText = string.Empty;
                var candidates = jsonResponse?["candidates"] as JArray; // CS8602: Handle potential null
                if (candidates != null && candidates.Count > 0)
                {
                    var content0 = candidates[0]?["content"]; // CS8602: Handle potential null
                    var parts = content0?["parts"] as JArray; // CS8602: Handle potential null
                    if (parts != null && parts.Count > 0)
                    {
                        generatedText = parts[0]?["text"]?.ToString() ?? string.Empty; // CS8602: Handle potential null
                    }
                }
                
                // Try to extract JSON objects from the response
                try
                {
                    if (generatedText.Contains("[") && generatedText.Contains("]"))
                    {
                        int startIdx = generatedText.IndexOf('[');
                        int endIdx = generatedText.LastIndexOf(']') + 1;
                        string jsonArray = generatedText.Substring(startIdx, endIdx - startIdx);
                        
                        // Try to parse the JSON array
                        var objects = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(jsonArray);
                        
                        if (objects != null)
                        {
                            foreach (var obj in objects)
                            {
                                try
                                {
                                    results.Add(new DetectedObject
                                    {
                                        Label = obj.ContainsKey("label") ? obj["label"]?.ToString() ?? "unknown" : "unknown", // CS8601: Handle potential null
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
                    _logger.LogError(ex, "Error parsing object detection response from Gemini: {Message}", ex.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting objects with Gemini: {Message}", ex.Message);
            }
            
            return results;
        }
    }
}