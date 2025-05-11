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
    /// Service for interacting with OpenAI APIs including vision capabilities
    /// </summary>
    public class OpenAiService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenAiService> _logger;
        private AiServiceConfig? _config;
        
        // API endpoints
        private const string ChatCompletionEndpoint = "https://api.openai.com/v1/chat/completions";
        private const string ModelsEndpoint = "https://api.openai.com/v1/models";
        
        public string ProviderName => "OpenAI";
        
        public bool IsConfigured => !string.IsNullOrEmpty(Config?.ApiKey);
        
        public AiServiceConfig Config
        {
            get => _config ??= new AiServiceConfig();
            set => _config = value;
        }
        
        public OpenAiService(HttpClient httpClient, ILogger<OpenAiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _config = new AiServiceConfig();
        }
        
        /// <summary>
        /// Checks if the OpenAI service is available
        /// </summary>
        public async Task<bool> IsServiceAvailableAsync()
        {
            if (!IsConfigured)
                return false;
                
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, ModelsEndpoint);
                request.Headers.Add("Authorization", $"Bearer {Config.ApiKey}");
                
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to OpenAI service");
                return false;
            }
        }
        
        /// <summary>
        /// Gets available models from OpenAI
        /// </summary>
        public async Task<List<string>> GetAvailableModelsAsync()
        {
            try
            {
                if (!IsConfigured)
                    return new List<string>();
                    
                var request = new HttpRequestMessage(HttpMethod.Get, ModelsEndpoint);
                request.Headers.Add("Authorization", $"Bearer {Config.ApiKey}");
                
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                dynamic? jsonResponse = JsonConvert.DeserializeObject(content);
                
                // Extract model IDs from the response
                var models = new List<string>();
                if (jsonResponse?.data != null)
                {
                    foreach (var model in jsonResponse.data)
                    {
                        string? id = model.id?.ToString();
                        if (!string.IsNullOrEmpty(id) && 
                            (id.Contains("gpt") || id.Contains("dall-e") || id.Contains("whisper")))
                        {
                            models.Add(id);
                        }
                    }
                }
                
                // If we couldn't get models from the API, return commonly available ones
                if (models.Count == 0)
                {
                    models.AddRange(new[] {
                        "gpt-3.5-turbo",
                        "gpt-4",
                        "gpt-4-vision-preview",
                        "dall-e-3"
                    });
                }
                
                return models;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting available models from OpenAI");
                
                // Return commonly available models as fallback
                return new List<string> {
                    "gpt-3.5-turbo",
                    "gpt-4",
                    "gpt-4-vision-preview",
                    "dall-e-3"
                };
            }
        }
        
        /// <summary>
        /// Makes a vision-based decision using OpenAI's vision capabilities
        /// </summary>
        public async Task<(string decision, string explanation)> MakeDecisionAsync(
            string modelName, byte[] screenshot, string decisionCriteria)
        {
            try
            {
                if (!IsConfigured)
                    return ("error", "OpenAI service not configured");
                
                // Check if the model supports vision capabilities
                if (!modelName.Contains("vision") && !modelName.Contains("gpt-4") && !modelName.Contains("gpt-4o"))
                {
                    return ("error", "The selected model does not support vision capabilities");
                }
                
                // Use vision-preview model if not specified
                string visionModel = modelName.Contains("vision") ? modelName : "gpt-4-vision-preview";
                
                // Convert screenshot to base64
                string base64Image = Convert.ToBase64String(screenshot);
                
                // Create the request body
                var messages = new List<object>
                {
                    new
                    {
                        role = "system",
                        content = "You are an AI assistant that analyzes images and makes decisions based on the criteria provided. " +
                                  "Respond in this format: 'DECISION: [decision] REASONING: [reasoning]' where decision is a single word like 'yes', 'no', 'found', or 'unknown'."
                    },
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "text",
                                text = $"Analyze this image and make a decision based on the following criteria: {decisionCriteria}"
                            },
                            new
                            {
                                type = "image_url",
                                image_url = new
                                {
                                    url = $"data:image/jpeg;base64,{base64Image}"
                                }
                            }
                        }
                    }
                };
                
                var requestBody = new
                {
                    model = visionModel,
                    messages = messages,
                    max_tokens = 800
                };
                
                var jsonContent = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                // Create the request
                var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionEndpoint);
                request.Headers.Add("Authorization", $"Bearer {Config.ApiKey}");
                request.Content = content;
                
                // Send the request
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                dynamic? jsonResponse = JsonConvert.DeserializeObject(responseContent);
                
                // Extract the response text
                string responseText = jsonResponse?.choices[0]?.message?.content?.ToString() ?? "No response";
                
                // Parse the decision and reasoning from the response
                string decision = "unknown";
                string reasoning = responseText;
                
                // Try to extract DECISION and REASONING parts
                int decisionIndex = responseText.IndexOf("DECISION:", StringComparison.OrdinalIgnoreCase);
                int reasoningIndex = responseText.IndexOf("REASONING:", StringComparison.OrdinalIgnoreCase);
                
                if (decisionIndex >= 0 && reasoningIndex > decisionIndex)
                {
                    decision = responseText
                        .Substring(decisionIndex + 9, reasoningIndex - decisionIndex - 9)
                        .Trim();
                        
                    reasoning = responseText
                        .Substring(reasoningIndex + 10)
                        .Trim();
                }
                
                return (decision, reasoning);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error making decision with OpenAI Vision");
                return ("error", $"Error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Process text with OpenAI model
        /// </summary>
        public async Task<string> ProcessTextAsync(string modelName, string prompt)
        {
            try
            {
                if (!IsConfigured)
                    return "OpenAI service not configured";
                    
                // Default to GPT-3.5 if model not specified
                string model = string.IsNullOrEmpty(modelName) ? "gpt-3.5-turbo" : modelName;
                
                // Create the request body
                var requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                        new { role = "system", content = "You are a helpful assistant." },
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 1000
                };
                
                var jsonContent = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                // Create the request
                var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionEndpoint);
                request.Headers.Add("Authorization", $"Bearer {Config.ApiKey}");
                request.Content = content;
                
                // Send the request
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                dynamic? jsonResponse = JsonConvert.DeserializeObject(responseContent);
                
                string responseText = jsonResponse?.choices[0]?.message?.content?.ToString() ?? "No response";
                return responseText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing text with OpenAI");
                return $"Error: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Detects objects in an image using OpenAI's vision capabilities
        /// </summary>
        public async Task<List<DetectedObject>> DetectObjectsAsync(string modelName, byte[] imageData)
        {
            try
            {
                if (!IsConfigured)
                    return new List<DetectedObject>();
                
                // Check if the model supports vision capabilities
                if (!modelName.Contains("vision") && !modelName.Contains("gpt-4") && !modelName.Contains("gpt-4o"))
                {
                    return new List<DetectedObject>();
                }
                
                // Convert image to base64
                string base64Image = Convert.ToBase64String(imageData);
                
                // Use vision-preview model if not specified
                string visionModel = modelName.Contains("vision") ? modelName : "gpt-4-vision-preview";
                
                // Create the request body asking for JSON object detection
                var messages = new List<object>
                {
                    new
                    {
                        role = "system",
                        content = "You are a computer vision system. Detect objects in the image and return them in JSON format. " +
                                  "Return only a JSON array of objects with properties: label, confidence (0-1), and normalized bounding box coordinates (x, y, width, height) where x,y is the top-left corner. " +
                                  "Values must be between 0 and 1. Do not include any explanatory text outside the JSON."
                    },
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "text",
                                text = "Detect objects in this image and return them in JSON format as described."
                            },
                            new
                            {
                                type = "image_url",
                                image_url = new
                                {
                                    url = $"data:image/jpeg;base64,{base64Image}"
                                }
                            }
                        }
                    }
                };
                
                var requestBody = new
                {
                    model = visionModel,
                    messages = messages,
                    max_tokens = 800,
                    response_format = new { type = "json_object" }
                };
                
                var jsonContent = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                // Create the request
                var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionEndpoint);
                request.Headers.Add("Authorization", $"Bearer {Config.ApiKey}");
                request.Content = content;
                
                // Send the request
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                dynamic? jsonResponse = JsonConvert.DeserializeObject(responseContent);
                
                string responseText = jsonResponse?.choices[0]?.message?.content?.ToString() ?? "[]";
                
                // Parse the JSON response to extract object detections
                try
                {
                    // Clean up response to ensure it's valid JSON
                    responseText = responseText.Trim();
                    if (responseText.StartsWith("```json"))
                    {
                        responseText = responseText.Substring(7);
                        int endIndex = responseText.LastIndexOf("```");
                        if (endIndex >= 0)
                            responseText = responseText.Substring(0, endIndex);
                    }
                    
                    var detectedObjects = JsonConvert.DeserializeObject<List<DetectedObject>>(responseText);
                    return detectedObjects ?? new List<DetectedObject>();
                }
                catch
                {
                    _logger.LogWarning("Failed to parse object detection results from OpenAI");
                    return new List<DetectedObject>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting objects with OpenAI");
                return new List<DetectedObject>();
            }
        }
    }
}