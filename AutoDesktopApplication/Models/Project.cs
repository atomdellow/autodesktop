using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AutoDesktopApplication.Models
{
    /// <summary>
    /// Represents a project that contains multiple workflows
    /// </summary>
    public class Project
    {
        public int Id { get; set; }
        
        [Required]
        public string Name { get; set; } = string.Empty;
        
        public string Description { get; set; } = string.Empty;
        
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        
        // Navigation property for related workflows
        public List<Workflow> Workflows { get; set; } = new List<Workflow>();
    }
}