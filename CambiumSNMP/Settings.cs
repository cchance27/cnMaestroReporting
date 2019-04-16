using System;
using System.Collections.Generic;
using System.Text;

namespace CambiumSNMP
{
    public class Settings
    {
        public string Community { get; set; } = "public";
        public int Version { get; set; } = 2;
        public int Retries { get; set; } = 1;
        public int Timeout { get; set; } = 2000;
        public int Port { get; set; } = 161;
        public int Threads { get; set; } = 1;
    }
}
