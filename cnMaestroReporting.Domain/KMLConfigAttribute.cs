using System;
using System.Collections.Generic;
using System.Text;

namespace cnMaestroReporting.Domain
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class KMLConfig : Attribute
    {
        public string Name { get; set; }
        public bool ConvertToUrl { get; set; }
        public bool Hidden { get; set; }
        public string TrimAfter { get; set; }
    }
}