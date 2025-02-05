using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SMN.Report.Processor
{
    public class CreateReport
    {
        string reportCode;
        string jsonFilter;
        string outputFile;
        string error;
        string username;
        public CreateReport(string reportCode, string jsonFilter,string username)
        {
            this.reportCode = reportCode;
            this.jsonFilter = jsonFilter;
            this.username = username;

        }
        public bool Create()
        {
            return LoadReportDll.LoadData(reportCode, jsonFilter, ref error, ref outputFile,username);
        }
        public string GetOutputFile()
        {
            return outputFile;
        }
        public string GetError()
        {
            return error;
        }

    }
}