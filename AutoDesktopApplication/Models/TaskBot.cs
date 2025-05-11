using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace AutoDesktopApplication.Models
{
    /// <summary>
    /// Represents an individual automation task that contains recorded inputs
    /// </summary>
    public class TaskBot
    {
        public int Id { get; set; }
        public required string Name { get; set; } = string.Empty;
        public required string Description { get; set; } = string.Empty;
        public TaskType Type { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        
        // The sequence order within the workflow
        public int SequenceOrder { get; set; }
        
        // Delay before this task starts, in milliseconds
        public long DelayBefore { get; set; } // Added

        // Estimated duration of this task, in milliseconds
        public long EstimatedDuration { get; set; } // Added

        // Serialized input data
        public required string InputData { get; set; } = string.Empty;
        
        // Optional AI decision metadata
        public string AiDecisionCriteria { get; set; } = string.Empty;
        
        // Optional screenshot data
        public byte[] ScreenshotData { get; set; } = Array.Empty<byte>();
        
        // Foreign key for Workflow
        public int WorkflowId { get; set; }
        
        // Navigation property
        public required Workflow Workflow { get; set; } = null!;
        
        // Deserialize input data to appropriate type based on TaskType
        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public object? DeserializedInputData
        {
            get
            {
                return Type switch
                {
                    TaskType.MouseMovement => JsonConvert.DeserializeObject<MouseMovementData>(InputData),
                    TaskType.KeyboardInput => JsonConvert.DeserializeObject<KeyboardInputData>(InputData),
                    TaskType.AiDecision => JsonConvert.DeserializeObject<AiDecisionData>(InputData),
                    TaskType.Delay => JsonConvert.DeserializeObject<DelayData>(InputData),
                    _ => null
                };
            }
        }
    }

    // Enum to represent different task types
    public enum TaskType
    {
        MouseMovement,
        KeyboardInput,
        AiDecision,
        Delay
    }
}