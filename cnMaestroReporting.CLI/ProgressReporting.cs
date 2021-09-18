using System;

namespace cnMaestroReporting.CLI
{
    public static class ProgressReporting
    {
        /// <summary>
        /// Output for thread reporting
        /// </summary>
        /// <param name="progress"></param>
        public static void ReportProgressSNMP(cnMaestroReporting.SNMP.SnmpProgress progress)
        {
            Console.WriteLine($"SNMP Update: {progress.CurrentProgressMessage} ({progress.CurrentProgressAmount}/{progress.TotalProgressAmount})");
        }

    }
}
