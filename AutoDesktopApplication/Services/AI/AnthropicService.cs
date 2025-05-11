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
    /// Service for interacting with Anthropic's Claude AI models
    /// </summary>
    public class AnthropicService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AnthropicService> _logger;
        private AiServiceConfig? _config;
        
        public string ProviderName => "Anthropic Claude";
        
        public bool IsConfigured => !string.IsNullOrEmpty(Config?.ApiKey);
        
        public AiServiceConfig Config
        {
            get => _config ??= new AiServiceConfig
            {
                ApiKey = string.Empty,
                EndpointUrl = "https://api.anthropic.com/v1",
                AdditionalSettings = new Dictionary<string, string>
                {
                    { "AnthropicVersion", "2023-06-01" }
                }
            };
            set => _config = value;
        }
        
        public AnthropicService(HttpClient httpClient, ILogger<AnthropicService> logger)
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
                // Anthropic doesn't have a dedicated endpoint to check API status
                // We'll attempt a minimal message to verify the API key works
                var testMessage = new
                {
                    model = "claude-3-sonnet-20240229",
                    max_tokens = 10,
                    messages = new[]
                    {
                        new { role = "user", content = "Say hello" }
                    }
                };
                
                var content = new StringContent(
                    JsonConvert.SerializeObject(testMessage),
                    Encoding.UTF8,
                    "application/json");
                
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{Config.EndpointUrl}/messages");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Config.ApiKey);
                
                // Add Anthropic API version header
                if (Config.AdditionalSettings.TryGetValue("AnthropicVersion", out string? anthropicVersion)) // CS8600: Changed to out string?
                {
                    request.Headers.Add("anthropic-version", anthropicVersion);
                }
                
                request.Content = content;
                
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Anthropic service availability: {Message}", ex.Message);
                return false;
            }
        }
        
        public Task<List<string>> GetAvailableModelsAsync()
        {
            // Anthropic doesn't have an endpoint to list available models
            // Return the known models
            return Task.FromResult(new List<string>
            {
                "claude-3-opus-20240229",
                "claude-3-sonnet-20240229",
                "claude-3-haiku-20240307",
                "claude-2.1",
                "claude-2.0",
                "claude-instant-1.2"
            });
        }
        
        public async Task<string> ProcessTextAsync(string modelName, string prompt)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("Anthropic API is not configured. Please set API key.");
                
            try
            {
                var requestBody = new
                {
                    model = modelName,
                    max_tokens = 1024,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    }
                };
                
                var content = new StringContent(
                    JsonConvert.SerializeObject(requestBody),
                    Encoding.UTF8,
                    "application/json");
                
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{Config.EndpointUrl}/messages");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Config.ApiKey);
                
                // Add Anthropic API version header
                if (Config.AdditionalSettings.TryGetValue("AnthropicVersion", out string? anthropicVersion)) // CS8600: Changed to out string?
                {
                    request.Headers.Add("anthropic-version", anthropicVersion);
                }
                
                request.Content = content;
                
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                dynamic? responseObject = JsonConvert.DeserializeObject(responseContent);
                
                var textContent = responseObject?.content?[0]?.text; // Safely access
                if (textContent == null)
                {
                    throw new InvalidOperationException("Failed to parse response from Anthropic API or content is missing.");
                }
                return textContent.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing text with Anthropic: {Message}", ex.Message);
                throw;
            }
        }
        
        public async Task<(string decision, string explanation)> MakeDecisionAsync(string modelName, byte[] screenshot, string decisionCriteria)
        {
            try
            {
                string response = await AnalyzeImageAsync(modelName, screenshot, 
                    $"Analyze this screenshot and make a decision based on the following criteria: {decisionCriteria}. " +
                    "Return your answer in the format 'DECISION: [your one-word decision] REASONING: [your explanation]'");
                
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
                _logger.LogError(ex, "Error making decision with Anthropic: {Message}", ex.Message);
                throw;
            }
        }
        
        public async Task<string> AnalyzeImageAsync(string modelName, byte[] imageBytes, string prompt)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("Anthropic API is not configured. Please set API key.");
                
            try
            {
                // Convert image bytes to base64
                string base64Image = Convert.ToBase64String(imageBytes);
                
                // Prepare the messages with multimodal content
                // Claude API requires a specific format for messages with images
                var messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = prompt },
                            new
                            {
                                type = "image",
                                source = new
                                {
                                    type = "base64",
                                    media_type = "image/jpeg",
                                    data = base64Image
                                }
                            }
                        }
                    }
                };
                
                var requestBody = new
                {
                    model = modelName,  // Use claude-3-opus or other vision capable model
                    max_tokens = 1024,
                    messages
                };
                
                var content = new StringContent(
                    JsonConvert.SerializeObject(requestBody),
                    Encoding.UTF8,
                    "application/json");
                
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{Config.EndpointUrl}/messages");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Config.ApiKey);
                
                // Add Anthropic API version header
                if (Config.AdditionalSettings.TryGetValue("AnthropicVersion", out string? anthropicVersion)) // CS8600: Changed to out string?
                {
                    request.Headers.Add("anthropic-version", anthropicVersion);
                }
                
                request.Content = content;
                
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                dynamic? responseObject = JsonConvert.DeserializeObject(responseContent);
                
                var textContent = responseObject?.content?[0]?.text; // Safely access
                if (textContent == null)
                {
                    throw new InvalidOperationException("Failed to parse response from Anthropic API or content is missing.");
                }
                return textContent.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing image with Anthropic: {Message}", ex.Message);
                throw;
            }
        }
        
        public async Task<List<DetectedObject>> DetectObjectsAsync(string modelName, byte[] imageData)
        {
            var results = new List<DetectedObject>();
            
            try
            {
                // Claude doesn't have a dedicated object detection API
                // We'll prompt it to analyze the image and return objects in a structured format
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
                        
                        if (objects != null)
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
                    _logger.LogError(ex, "Error parsing object detection response from Anthropic: {Message}", ex.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting objects with Anthropic: {Message}", ex.Message);
            }
            
            return results;
        }
    }
}