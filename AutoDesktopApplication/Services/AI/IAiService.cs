using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AutoDesktopApplication.Services.AI
{
    /// <summary>
    /// Interface for AI services that can be used by the application
    /// </summary>
    public interface IAiService
    {
        /// <summary>
        /// Gets the name of the AI provider
        /// </summary>
        string ProviderName { get; }
        
        /// <summary>
        /// Gets or sets the configuration for the AI service
        /// </summary>
        AiServiceConfig Config { get; set; }
        
        /// <summary>
        /// Gets whether the service is configured with valid credentials
        /// </summary>
        bool IsConfigured { get; }
        
        /// <summary>
        /// Checks if the AI service is available and properly configured
        /// </summary>
        Task<bool> IsServiceAvailableAsync();
        
        /// <summary>
        /// Gets a list of available models from the AI provider
        /// </summary>
        Task<List<string>> GetAvailableModelsAsync();
        
        /// <summary>
        /// Process text with an AI model
        /// </summary>
        /// <param name="modelName">Name/ID of the model to use</param>
        /// <param name="prompt">Text prompt to send to the model</param>
        /// <returns>Generated response text</returns>
        Task<string> ProcessTextAsync(string modelName, string prompt);
        
        /// <summary>
        /// Make a decision using image data and decision criteria
        /// </summary>
        /// <param name="modelName">Name/ID of the model to use</param>
        /// <param name="screenshot">Image data as byte array</param>
        /// <param name="decisionCriteria">Criteria for making the decision</param>
        /// <returns>Decision (typically yes/no) and explanation</returns>
        Task<(string decision, string explanation)> MakeDecisionAsync(string modelName, byte[] screenshot, string decisionCriteria);
        
        /// <summary>
        /// Process image with AI model for visual analysis and object detection
        /// </summary>
        /// <param name="modelName">Name/ID of the model to use</param>
        /// <param name="imageData">Image data as byte array</param>
        /// <returns>List of detected objects with their properties</returns>
        Task<List<DetectedObject>> DetectObjectsAsync(string modelName, byte[] imageData);
    }
    
    /// <summary>
    /// Configuration settings for AI services
    /// </summary>
    public class AiServiceConfig
    {
        public string ApiKey { get; set; } = string.Empty;
        public string EndpointUrl { get; set; } = string.Empty;
        public Dictionary<string, string> AdditionalSettings { get; set; } = new Dictionary<string, string>();
    }
    
    /// <summary>
    /// Represents an object detected in an image
    /// </summary>
    public class DetectedObject
    {
        /// <summary>
        /// Label/class of the detected object
        /// </summary>
        public required string Label { get; set; }
        
        /// <summary>
        /// Confidence score (0-1) of the detection
        /// </summary>
        public float Confidence { get; set; }
        
        /// <summary>
        /// Bounding box coordinates (normalized 0-1 values)
        /// </summary>
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }
}