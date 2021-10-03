using System.Data;
using System.Collections.Generic;
using System.Linq;

namespace cnMaestroReporting.Reporting.PPTX.Entities
{
    public record TableInfo(string title, string[] headers, double[] columnPercents, IEnumerable<string[]> columnData)
    {
        double[] columnPercents { get; set; } = columnPercents;
        public double[] columnWidths(float tableWidth) => columnPercents.Select(x => x * tableWidth).ToArray();
    }
}

