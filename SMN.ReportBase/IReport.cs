using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SMN.ReportBase
{
    public interface IReport
    {
        
        bool GetData(JObject jsonFilter);
        bool ProcessData();
        bool ExportData(SMG.Models.Report report, string outputFile);
        (bool, MemoryStream) ExportDataPDF(SMG.Models.Report report, string outputFile);
    }
}
