using System;
using System.Collections.Generic;

namespace AutoDesktopApplication.Models
{
    public class DetectedObject
    {
        public string Class { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public List<int> BoundingBox { get; set; } = new List<int>();
    }
}
