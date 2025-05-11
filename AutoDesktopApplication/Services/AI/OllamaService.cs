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
    /// Service for interacting with local Ollama models
    /// </summary>
    public class OllamaService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OllamaService> _logger;
        private AiServiceConfig? _config; // CS8618: Made _config nullable
        
        public string ProviderName => "Ollama (Local)";
        
        public bool IsConfigured => !string.IsNullOrEmpty(Config?.EndpointUrl);
        
        public AiServiceConfig Config
        {
            get => _config ??= new AiServiceConfig 
            { 
                EndpointUrl = "http://localhost:11434" // Default Ollama URL
            };
            set => _config = value;
        }
        
        public OllamaService(HttpClient httpClient, ILogger<OllamaService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }
        
        /// <summary>
        /// Checks if the Ollama service is available and properly running
        /// </summary>
        public async Task<bool> IsServiceAvailableAsync()
        {
            try
            {
                var models = await GetAvailableModelsAsync();
                return models.Count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Ollama service availability: {Message}", ex.Message);
                return false;
            }
        }
        
        /// <summary>
        /// Gets available Ollama models from the local server
        /// </summary>
        public async Task<List<string>> GetAvailableModelsAsync()
        {
            var models = new List<string>();
            
            try
            {
                string url = $"{Config.EndpointUrl}/api/tags";
                
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(content);
                
                var modelsArray = jsonResponse["models"] as JArray;
                
                if (modelsArray != null)
                {
                    foreach (var model in modelsArray)
                    {
                        string? name = model?["name"]?.ToString(); // CS8600: Made name nullable and added null-conditional for model access
                        if (!string.IsNullOrEmpty(name))
                        {
                            models.Add(name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available Ollama models: {Message}", ex.Message);
            }
            
            return models;
        }
        
        /// <summary>
        /// Process text with an Ollama model
        /// </summary>
        public async Task<string> ProcessTextAsync(string modelName, string prompt)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("Ollama service is not configured properly.");
                
            try
            {
                string url = $"{Config.EndpointUrl}/api/generate";
                
                var requestBody = new
                {
                    model = modelName,
                    prompt = prompt,
                    stream = false
                };
                
                var jsonContent = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(responseContent);
                
                // Extract the generated text from the response
                return jsonResponse["response"]?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing text with Ollama: {Message}", ex.Message);
                throw;
            }
        }
        
        /// <summary>
        /// Make a decision using image data and criteria with an Ollama model
        /// </summary>
        public async Task<(string decision, string explanation)> MakeDecisionAsync(string modelName, byte[] screenshot, string decisionCriteria)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("Ollama service is not configured properly.");
                
            try
            {
                // Convert image to base64
                string base64Image = Convert.ToBase64String(screenshot);
                
                // Create a multimodal prompt with the image and text
                // Note: This requires an Ollama model that supports multimodal inputs (like llava)
                string url = $"{Config.EndpointUrl}/api/generate";
                
                // Create the prompt with decision criteria
                string prompt = $"Analyze this image and make a decision based on the following criteria: {decisionCriteria}. " +
                                "Return your answer in the format 'DECISION: [your one-word decision] REASONING: [your explanation]'";
                
                // Check if the model name contains vision capabilities
                // Only certain models like llava support images
                if (!modelName.Contains("llava") && !modelName.Contains("vision") && !modelName.Contains("clip"))
                {
                    return ("error", $"The model {modelName} likely doesn't support image inputs. Try using a multimodal model like 'llava'.");
                }
                
                var requestBody = new
                {
                    model = modelName,
                    prompt = prompt,
                    stream = false,
                    images = new[] { base64Image }
                };
                
                var jsonContent = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(responseContent);
                
                // Extract the generated text from the response
                string generatedText = jsonResponse["response"]?.ToString() ?? string.Empty;
                
                // Parse the decision and reasoning from the response
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
                _logger.LogError(ex, "Error making decision with Ollama: {Message}", ex.Message);
                return ("error", $"Failed to process image: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Process image with Ollama AI model for visual analysis and object detection
        /// Note: This has limited success with most Ollama models as they are not specifically trained for object detection
        /// </summary>
        public async Task<List<DetectedObject>> DetectObjectsAsync(string modelName, byte[] imageData)
        {
            var results = new List<DetectedObject>();
            
            if (!IsConfigured)
                throw new InvalidOperationException("Ollama service is not configured properly.");
                
            try
            {
                // Convert image to base64
                string base64Image = Convert.ToBase64String(imageData);
                
                // Check if the model name contains vision capabilities
                if (!modelName.Contains("llava") && !modelName.Contains("vision") && !modelName.Contains("clip"))
                {
                    _logger.LogWarning("The model {ModelName} likely doesn't support image inputs.", modelName);
                    return results;
                }
                
                string url = $"{Config.EndpointUrl}/api/generate";
                
                // Ask the model to detect objects and return them in a parsable format
                string prompt = "Detect objects in this image. For each object, provide its label, position (x, y coordinates as normalized 0-1 values), width and height (as normalized 0-1 values), and confidence score. Format the response as JSON array with objects having properties: label, x, y, width, height, confidence.";
                
                var requestBody = new
                {
                    model = modelName,
                    prompt = prompt,
                    stream = false,
                    images = new[] { base64Image }
                };
                
                var jsonContent = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(responseContent);
                
                // Extract the generated text from the response
                string generatedText = jsonResponse["response"]?.ToString() ?? string.Empty;
                
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
                                        Label = obj.ContainsKey("label") && obj["label"] != null ? obj["label"].ToString() ?? "unknown" : "unknown", // CS8601: Added null check and coalescing
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
                    _logger.LogError(ex, "Error parsing object detection response from Ollama: {Message}", ex.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting objects with Ollama: {Message}", ex.Message);
            }
            
            return results;
        }
    }
}