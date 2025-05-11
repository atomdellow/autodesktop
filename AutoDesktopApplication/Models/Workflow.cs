using System;
using System.Collections.Generic;

namespace AutoDesktopApplication.Models
{
    /// <summary>
    /// Represents a workflow that contains a sequence of tasks
    /// </summary>
    public class Workflow
    {
        public int Id { get; set; }
        public required string Name { get; set; } = string.Empty;
        public required string Description { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public DateTime? LastRunDate { get; set; }
        
        // Foreign key for Project
        public int ProjectId { get; set; }
        
        // Navigation properties
        public Project Project { get; set; } = null!;
        public List<TaskBot> TaskBots { get; set; } = new List<TaskBot>();
        public List<WorkflowRun> WorkflowRuns { get; set; } = new List<WorkflowRun>();
    }
}