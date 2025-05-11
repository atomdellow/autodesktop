using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net.Http.Json;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using AutoDesktopApplication.Services.AI;

namespace AutoDesktopApplication.Services
{
    /// <summary>
    /// Service responsible for interacting with a locally-hosted Ollama AI model
    /// </summary>
    public class OllamaService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OllamaService> _logger;
        private const string BaseUrl = "http://localhost:11434/api";
        private AiServiceConfig? _config;

        public string ProviderName => "Ollama";
        
        public bool IsConfigured => true; // Ollama is locally hosted, so no API key is required

        public AiServiceConfig Config 
        { 
            get => _config ??= new AiServiceConfig 
            { 
                EndpointUrl = BaseUrl 
            }; 
            set => _config = value; 
        }

        public OllamaService(HttpClient httpClient, ILogger<OllamaService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _config = new AiServiceConfig { EndpointUrl = BaseUrl };
        }

        /// <summary>
        /// Sends a prompt to the Ollama API and gets the response
        /// </summary>
        /// <param name="model">The name of the model to use</param>
        /// <param name="prompt">The text prompt to send</param>
        /// <param name="options">Optional model parameters</param>
        /// <returns>The AI-generated response</returns>
        public async Task<string> GenerateTextAsync(string model, string prompt, Dictionary<string, object>? options = null)
        {
            try
            {
                var requestBody = new
                {
                    model = model,
                    prompt = prompt,
                    options = options ?? new Dictionary<string, object>()
                };

                var jsonContent = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{Config.EndpointUrl}/generate", content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                
                // Parse JSON response to extract the generated text
                dynamic? responseObject = JsonConvert.DeserializeObject(responseContent);
                if (responseObject == null)
                    return string.Empty;
                    
                return responseObject.response?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating text with Ollama: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Sends a screenshot to the Ollama model for visual analysis
        /// </summary>
        /// <param name="model">The name of the model to use (must support vision)</param>
        /// <param name="imageBytes">The image bytes for the screenshot</param>
        /// <param name="prompt">Text prompt explaining what to look for in the image</param>
        /// <returns>The AI-generated analysis of the image</returns>
        public async Task<string> AnalyzeImageAsync(string model, byte[] imageBytes, string prompt)
        {
            try
            {
                // Convert image bytes to base64
                string base64Image = Convert.ToBase64String(imageBytes);
                
                // Create a multimodal prompt with both text and an image
                var message = new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt },
                        new
                        {
                            type = "image",
                            image = base64Image
                        }
                    }
                };

                var requestBody = new
                {
                    model = model,
                    messages = new[] { message }
                };

                var jsonContent = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{Config.EndpointUrl}/chat/completions", content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                
                // Parse JSON response to extract the generated analysis
                dynamic? responseObject = JsonConvert.DeserializeObject(responseContent);
                if (responseObject == null)
                    return string.Empty;
                    
                return responseObject?.choices?[0]?.message?.content?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing image with Ollama: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Makes a decision based on visual input and a set of criteria
        /// </summary>
        /// <param name="modelName">The name of the model to use</param>
        /// <param name="screenshot">The screenshot to analyze</param>
        /// <param name="decisionCriteria">Criteria for making the decision</param>
        /// <returns>The AI's decision and reasoning</returns>
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
                _logger.LogError(ex, "Error making decision with Ollama: {Message}", ex.Message);
                throw;
            }
        }
        
        /// <summary>
        /// Checks if Ollama service is running
        /// </summary>
        /// <returns>True if service is available</returns>
        public async Task<bool> IsServiceAvailableAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{Config.EndpointUrl}/version");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Gets available models from Ollama
        /// </summary>
        public async Task<List<string>> GetAvailableModelsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{Config.EndpointUrl}/models");
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                dynamic? modelData = JsonConvert.DeserializeObject(content);
                
                List<string> models = new List<string>();
                if (modelData?.models != null)
                {
                    foreach (var model in modelData.models)
                    {
                        models.Add((string?)model.name ?? "unknown");
                    }
                }
                
                return models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available models: {Message}", ex.Message);
                return new List<string>();
            }
        }
        
        /// <summary>
        /// Process text with AI model (for text-only AI requests)
        /// Implementation of IAiService interface
        /// </summary>
        public async Task<string> ProcessTextAsync(string modelName, string prompt)
        {
            return await GenerateTextAsync(modelName, prompt);
        }

        /// <summary>
        /// Process image with AI model for visual analysis and object detection
        /// Implementation of IAiService interface
        /// </summary>
        public async Task<List<DetectedObject>> DetectObjectsAsync(string modelName, byte[] imageData)
        {
            var results = new List<DetectedObject>();
            
            try
            {
                // Ollama doesn't have built-in object detection, so we'll use the LLM to describe objects
                string prompt = "Detect objects in this image. For each object, provide its name, position (x, y coordinates), width, height, and confidence score. Format each object detection as JSON.";
                
                string response = await AnalyzeImageAsync(modelName, imageData, prompt);
                
                // Try to extract JSON objects from the response
                // This is imperfect as it relies on the LLM to format properly
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
                                        Label = obj.ContainsKey("name") ? obj["name"]?.ToString() ?? "unknown" : "unknown",
                                        X = obj.ContainsKey("x") ? Convert.ToSingle(obj["x"]) : 0,
                                        Y = obj.ContainsKey("y") ? Convert.ToSingle(obj["y"]) : 0,
                                        Width = obj.ContainsKey("width") ? Convert.ToSingle(obj["width"]) : 0,
                                        Height = obj.ContainsKey("height") ? Convert.ToSingle(obj["height"]) : 0,
                                        Confidence = obj.ContainsKey("confidence") ? Convert.ToSingle(obj["confidence"]) : 0.5f
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