// Models/VisionServiceModels.cs
using System.Collections.Generic;
using System.Text.Json.Serialization; // Required for JsonPropertyName

namespace AutoDesktopApplication.Models
{
    // Request to the /detect endpoint
    public class DetectionRequest
    {
        [JsonPropertyName("screenshot")]
        public string? Screenshot { get; set; }
    }

    // Overall response from the /detect endpoint
    public class DetectionResponse
    {
        [JsonPropertyName("detections")]
        public List<Detection>? Detections { get; set; }

        [JsonPropertyName("error")] // Added to capture potential error messages from the API
        public string? Error { get; set; }
    }

    // Represents a single detected object
    public class Detection
    {
        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; } // Value types like double are non-nullable by default and initialized to 0.

        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }
    }
}
