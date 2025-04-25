using FlexCel.Render;
using FlexCel.Report;
using FlexCel.XlsAdapter;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SMG.Logging;
using SMN.Report.Processor;
using SMN.ReportBase;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMN.Report.SMN00001
{
    internal class SMN00001Processor : IReport
    {
        List<SMG.Models.User> listRdo = new List<SMG.Models.User>();
        

        public bool GetData(JObject rootFilter)
        {
            bool result = true;
            try
            {
                SMN.DBHelper.DBHelper dBHelper = new SMN.DBHelper.DBHelper();
                SMN00001Filter filter = rootFilter.ToObject<SMN00001Filter>();

                
                string query = "SELECT * FROM \"SMN_USER\"";
                var rs = dBHelper.ExecuteQueryWithParametersAsync(query, null, reader => SMN.DBHelper.DBHelper.MapToObject<SMG.Models.User>(reader));

                if (rs != null)
                {
                    listRdo.AddRange(rs.Result);
                }
            }
            catch (Exception ex)
            {
                result = false;
                LogSystem.Error(ex);
            }
            return result;
        }

        public bool ProcessData()
        {
            bool result = true;
            try
            {

            }
            catch (Exception ex)
            {
                result = false;
                LogSystem.Error(ex);
            }
            return result;
        }
        public bool ExportData(SMG.Models.Report reportData, string outputFile)
        {
            bool result = true;
            try
            {
                CreateDataToExcel exporter = new CreateDataToExcel();
                result = exporter.ExportData(
                    listRdo,
                    reportData.REPORT_FILE_NAME,
                    reportData.REPORT_TYPE_CODE,
                    reportData.REPORT_CODE,
                    reportData.REPORT_NAME,
                    outputFile,
                    "Report"
                );
                

            }
            catch (Exception ex)
            {
                result = false;
                LogSystem.Error(ex);
            }
            return result;
        }
        public (bool,MemoryStream) ExportDataPDF(SMG.Models.Report reportData, string outputFile)
        {
            MemoryStream pdfStream = new MemoryStream();
            bool result = true;
            try
            {
                // thêm dữ liệu tạm thời vào memory để xuất excel
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string templatePath = Path.Combine(baseDirectory, "Report","Template", reportData.REPORT_TYPE_CODE);
                FlexCelReport report = new FlexCelReport();
                if (string.IsNullOrEmpty(templatePath))
                {
                    throw new FileNotFoundException("Template file not found.");
                }
                XlsFile xls = new XlsFile(templatePath, true);

                // Thêm dữ liệu vào báo cáo
                report.AddTable("Report", listRdo);
                report.Run(xls);

                // Xuất ra PDF trong MemoryStream
                
                using (FlexCelPdfExport pdfExport = new FlexCelPdfExport(xls))
                {
                    pdfExport.BeginExport(pdfStream);
                    pdfExport.ExportAllVisibleSheets(false, "Report");
                    pdfExport.EndExport();
                }

                pdfStream.Position = 0;
            }
            catch (Exception ex)
            {
                result = false;
                LogSystem.Error("Error when export data to PDF",ex);
            }
            return (result,pdfStream);
        }
    }
}
