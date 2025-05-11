using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Linq;

namespace AutoDesktopApplication.Services.AI
{
    /// <summary>
    /// Service for interacting with Azure Cognitive Services (Language, OpenAI, etc.)
    /// </summary>
    public class AzureAiService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AzureAiService> _logger;
        private AiServiceConfig? _config;
        
        // Azure Cognitive Service Types
        private const string AzureOpenAiType = "openai";
        private const string AzureLanguageType = "language";
        
        // Service type selection
        private string _serviceType = AzureOpenAiType;
        
        public string ProviderName => "Azure AI";
        
        public bool IsConfigured => !string.IsNullOrEmpty(Config?.ApiKey) && !string.IsNullOrEmpty(Config?.EndpointUrl);
        
        public AiServiceConfig Config
        {
            get => _config ??= new AiServiceConfig();
            set => _config = value;
        }
        
        public AzureAiService(HttpClient httpClient, ILogger<AzureAiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _config = new AiServiceConfig();
        }
        
        /// <summary>
        /// Sets the specific Azure service type to use
        /// </summary>
        /// <param name="serviceType">Either "openai" or "language"</param>
        public void SetServiceType(string serviceType)
        {
            if (serviceType?.ToLower() == AzureLanguageType)
                _serviceType = AzureLanguageType;
            else
                _serviceType = AzureOpenAiType;
        }
        
        /// <summary>
        /// Checks if the Azure Cognitive service is available
        /// </summary>
        public async Task<bool> IsServiceAvailableAsync()
        {
            if (!IsConfigured)
                return false;
                
            try
            {
                // Perform a simple request to check service availability
                var request = new HttpRequestMessage(HttpMethod.Get, Config.EndpointUrl);
                request.Headers.Add("api-key", Config.ApiKey);
                
                var response = await _httpClient.SendAsync(request);
                
                // Even if we get a 401 or 404, the service is online
                return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Unauthorized;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to Azure AI service");
                return false;
            }
        }
        
        /// <summary>
        /// Gets available models from Azure Cognitive Services
        /// </summary>
        public Task<List<string>> GetAvailableModelsAsync() // Removed async
        {
            // Return predefined models based on the service type
            if (_serviceType == AzureOpenAiType)
            {
                return Task.FromResult(new List<string> // Added Task.FromResult
                {
                    "gpt-35-turbo",
                    "gpt-4",
                    "gpt-4-vision"
                });
            }
            else
            {
                return Task.FromResult(new List<string> // Added Task.FromResult
                {
                    "text-analytics-4",
                    "language-understanding"
                });
            }
        }
        
        /// <summary>
        /// Makes a vision-based decision using Azure services
        /// </summary>
        public async Task<(string decision, string explanation)> MakeDecisionAsync(
            string modelName, byte[] screenshot, string decisionCriteria)
        {
            try
            {
                if (!IsConfigured)
                    return ("error", "Azure AI service not configured");
                
                if (_serviceType == AzureOpenAiType && modelName.Contains("gpt-4"))
                {
                    // Use Azure OpenAI with vision capabilities
                    return await MakeGpt4VisionDecisionAsync(screenshot, decisionCriteria);
                }
                else
                {
                    // Fall back to text-only reasoning
                    return ("unknown", "This Azure AI model doesn't support vision capabilities");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error making decision with Azure AI");
                return ("error", $"Error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Process text with Azure AI model
        /// </summary>
        public async Task<string> ProcessTextAsync(string modelName, string prompt)
        {
            try
            {
                if (!IsConfigured)
                    return "Azure AI service not configured";
                
                // Different handling based on service type
                if (_serviceType == AzureOpenAiType)
                {
                    return await ProcessWithAzureOpenAiAsync(modelName, prompt);
                }
                else
                {
                    return await ProcessWithAzureLanguageAsync(modelName, prompt);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing text with Azure AI");
                return $"Error: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Detects objects in an image - delegates to other Azure vision services
        /// </summary>
        public Task<List<DetectedObject>> DetectObjectsAsync(string modelName, byte[] imageData) // Removed async
        {
            // Azure Cognitive Services doesn't directly support object detection
            // This would need to be implemented via Azure Computer Vision specifically
            return Task.FromResult(new List<DetectedObject>()); // Added Task.FromResult
        }
        
        /// <summary>
        /// Makes a decision using GPT-4 Vision capabilities (through Azure OpenAI)
        /// </summary>
        private async Task<(string decision, string explanation)> MakeGpt4VisionDecisionAsync(
            byte[] imageData, string decisionCriteria)
        {
            try
            {
                // Convert image to base64
                string base64Image = Convert.ToBase64String(imageData);
                
                // Create Azure OpenAI GPT-4 Vision request
                var requestBody = new
                {
                    messages = new object[]
                    {
                        new
                        {
                            role = "system",
                            content = "You are an AI assistant that analyzes images and makes decisions based on specified criteria. " +
                                      "Your response must follow this format: 'DECISION: [decision] REASONING: [reasoning]' " +
                                      "where decision is a single word like 'yes', 'no', 'found', or 'unknown'."
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
                    },
                    max_tokens = 800
                };
                
                var jsonContent = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                // Determine the Azure OpenAI deployment name from model name or use default
                string deploymentId = "gpt-4-vision";
                
                // Create the request with proper headers
                var request = new HttpRequestMessage(HttpMethod.Post, $"{Config.EndpointUrl}/openai/deployments/{deploymentId}/chat/completions?api-version=2023-07-01-preview");
                request.Headers.Add("api-key", Config.ApiKey);
                request.Content = content;
                
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                dynamic? jsonResponse = JsonConvert.DeserializeObject(responseContent);
                
                // Extract the response text
                string responseText = jsonResponse?.choices[0]?.message?.content?.ToString() ?? "No response";
                
                // Parse the decision and reasoning from the response
                string decision = "unknown";
                string reasoning = responseText;
                
                // Try to extract DECISION and REASONING parts if properly formatted
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
                _logger.LogError(ex, "Error using Azure OpenAI Vision");
                throw;
            }
        }
        
        /// <summary>
        /// Process text using Azure OpenAI service
        /// </summary>
        private async Task<string> ProcessWithAzureOpenAiAsync(string modelName, string prompt)
        {
            // Format for Azure OpenAI API
            var requestBody = new
            {
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful AI assistant." },
                    new { role = "user", content = prompt }
                },
                max_tokens = 800
            };
            
            var jsonContent = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            // Determine the deployment name based on the model name or use default
            string deploymentId = modelName.Contains("gpt-4") ? "gpt-4" : "gpt-35-turbo";
            
            // Create the request with proper headers
            var request = new HttpRequestMessage(HttpMethod.Post, $"{Config.EndpointUrl}/openai/deployments/{deploymentId}/chat/completions?api-version=2023-07-01-preview");
            request.Headers.Add("api-key", Config.ApiKey);
            request.Content = content;
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            dynamic? jsonResponse = JsonConvert.DeserializeObject(responseContent);
            
            return jsonResponse?.choices?[0]?.message?.content?.ToString() ?? "No response";
        }
        
        /// <summary>
        /// Process text using Azure Language service
        /// </summary>
        private async Task<string> ProcessWithAzureLanguageAsync(string modelName, string prompt)
        {
            // This is a simplified implementation, in practice you'd use Azure Language SDK
            var requestBody = new
            {
                kind = "Conversation",
                analysisInput = new
                {
                    conversationItem = new
                    {
                        text = prompt,
                        id = "1",
                        participantId = "user"
                    }
                },
                parameters = new
                {
                    projectName = modelName,
                    deploymentName = "production",
                    verbose = true
                }
            };
            
            var jsonContent = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            // Create the request with proper headers
            var request = new HttpRequestMessage(HttpMethod.Post, $"{Config.EndpointUrl}/language/:analyze-conversations?api-version=2023-04-01");
            request.Headers.Add("api-key", Config.ApiKey);
            request.Content = content;
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent; // In a real app, you'd parse this response
        }
    }
}