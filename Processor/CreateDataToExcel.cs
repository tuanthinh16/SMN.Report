using FlexCel.Report;
using FlexCel.XlsAdapter;
using SMG.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Web;

namespace SMN.Report.Processor
{
    public class CreateDataToExcel
    {
        public bool ExportData<T>(List<T> data, string reportFileName, string reportTypeCode, string reportCode, string reportName, string outputFile, string key)
        {
            bool result = true;
            try
            {
                FlexCelReport report = new FlexCelReport();
                (XlsFile xls, string outPath) = ProcessOutput(reportFileName, reportTypeCode, outputFile);
                if(xls == null)
                {
                    return false;
                }
                report.AddTable(key, data);
                report.Run(xls);
                xls.Save(outPath);
            }
            catch (Exception ex)
            {
                result = false;
                LogSystem.Error(ex);
            }
            return result;
        }
        public static (XlsFile, string) ProcessOutput(string reportFileName, string reportTypeCode, string outputFile)
        {
            try
            {
                //Chưa xác định được nơi lưu trữ file
                //Có thể lưu ở nơi đặt server hoặc  lưu ở nơi được chỉ định tùy theo dự án
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string templatePath = Path.Combine(baseDirectory, "Report", "Tmp", reportFileName);
                string outputPath = Path.Combine(baseDirectory, "Report", "Data", reportTypeCode, outputFile);
                string outputDirectory = Path.GetDirectoryName(outputPath);
                if (!Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }
                XlsFile xls = new XlsFile(templatePath, true);
                return (xls, outputPath);
            }
            catch (Exception ex)
            {
                LogSystem.Error(ex);
                return (null, null);
            }
        }

    }
}