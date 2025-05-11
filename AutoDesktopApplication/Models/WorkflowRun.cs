using System;

namespace AutoDesktopApplication.Models
{
    /// <summary>
    /// Represents a historical record of a workflow execution
    /// </summary>
    public class WorkflowRun
    {
        public int Id { get; set; }
        
        public int WorkflowId { get; set; }
        public Workflow Workflow { get; set; } = null!;
        
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        
        public bool Successful { get; set; }
        public string? Notes { get; set; }
    }
}