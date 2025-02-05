using FlexCel.Report;
using FlexCel.XlsAdapter;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SMG.Logging;
using SMG.Models;
using SMN.ReportBase;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using System.Web.Razor.Tokenizer;

namespace SMN.Report.Processor
{
    public class LoadReportDll
    {
        public static bool LoadData(string reportTypeCode, string jsonFilter, ref string error, ref string outputFileName,string username)
        {
            // Load the report dll
            bool isSuccessful = true;
            try
            {
                string pluginLink = "SMN.Report." + reportTypeCode;
                LogSystem.Debug("Create Report: "+JObject.Parse(jsonFilter).ToString());

                //Tạo mã ngẫu nhiên để tạo tên file
                string generateCode = Guid.NewGuid().ToString().Substring(0, 10);
                //đọc chuỗi json để lấy dữ liệu filter
                var filterData = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonFilter);
                JObject filtered = JsonConvert.DeserializeObject<dynamic>(filterData["JSON_FILTER"].ToString());
                //xử lý điều kiện lọc

                var filter = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonFilter);
                SMG.Models.Report reportDataCreate = new SMG.Models.Report();
                if (filter != null)
                {
                    if (filter.ContainsKey("REPORT_TYPE_CODE"))
                    {
                        reportDataCreate.REPORT_TYPE_CODE = filter["REPORT_TYPE_CODE"].ToString();

                    }
                    if (filter.ContainsKey("REPORT_CODE"))
                    {
                        reportDataCreate.REPORT_CODE = filter["REPORT_CODE"].ToString();

                    }
                    if (filter.ContainsKey("REPORT_NAME"))
                    {
                        reportDataCreate.REPORT_NAME = filter["REPORT_NAME"].ToString();

                    }
                }
                SMG.Models.Report reportData = LoadReport(reportTypeCode, reportDataCreate.REPORT_CODE).Result;
                outputFileName = reportData.REPORT_CODE + "_" + reportData.REPORT_NAME + "_" + generateCode + ".xlsx";

                if (!string.IsNullOrEmpty(reportTypeCode) && reportTypeCode.StartsWith("TKB"))
                {
                    var rs = ProcessTKB(reportTypeCode, jsonFilter,reportDataCreate,reportData,outputFileName);
                    if (rs != null)
                    {
                        isSuccessful = rs.Result.Item1;
                        error = rs.Result.Item2;
                    }
                }
                else
                {
                    AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
                    string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    string pluginPath = Path.Combine(baseDirectory, "bin","ReportDll", pluginLink + ".dll");
                    // Check if the file exists
                    // Kiểm tra plugin có tồn tại không
                    if (File.Exists(pluginPath))
                    {
                        // Load plugin assembly (DLL)
                        Assembly pluginAssembly = Assembly.LoadFrom(pluginPath);

                        // Tìm các lớp triển khai IPlugin trong assembly
                        var pluginTypes = pluginAssembly.GetTypes()
                            .Where(t => typeof(IReport).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
                        
                        foreach (var pluginType in pluginTypes)
                        {
                            // Tạo instance của lớp plugin
                            var pluginInstance = Activator.CreateInstance(pluginType);

                            // Gọi phương thức Execute của plugin
                            if (pluginInstance is IReport report)
                            {
                                bool isGetData = report.GetData(filtered);
                                bool isProcessData = isGetData ? report.ProcessData() : false;
                                bool isExportData = isGetData && isProcessData ? report.ExportData(reportData, outputFileName) : false;
                                isSuccessful = isSuccessful && isExportData && isGetData && isProcessData;

                            }
                        }
                        


                    }
                    else
                    {
                        error = "Plugin không tồn tại tại: "+pluginPath;

                        isSuccessful = false;
                    }
                    if (!isSuccessful)
                    {
                        error += " \r\n Có lỗi xảy ra trong quá trình xử lý báo cáo";
                    }
                }
                if (!SaveReportData(reportData, jsonFilter, generateCode, username))
                {
                    error = "Có lỗi xảy ra trong quá trình lưu báo cáo";
                    isSuccessful = false;
                }


            }
            catch (Exception ex)
            {

                LogSystem.Error(ex);
                isSuccessful = false;
            }
            return isSuccessful;
        }

        private static async Task<(bool, string)> ProcessTKB(string reportTypeCode, string jsonFilter, SMG.Models.Report reportDataCreate, SMG.Models.Report report, string outputFileName)
        {
            string error = string.Empty;
            try
            {

                if (report == null)
                {
                    return (false, "Report data is null.");
                }

                // Tạo đối tượng FlexCelReport và quy trình xử lý Excel
                using (FlexCelReport rp = new FlexCelReport())
                {
                    var (xls, outPath) = CreateDataToExcel.ProcessOutput(reportDataCreate.REPORT_FILE_NAME, reportTypeCode, outputFileName);

                    if (xls == null)
                    {
                        return (false, "Template file not found.");
                    }

                    xls.ActiveSheet = 1;//lấy sheet đầu tiên

                    int colCount = xls.ColCount;
                    SMN.DBHelper.DBHelper dbHelper = new SMN.DBHelper.DBHelper();

                    // Đọc dữ liệu từ từng cột trong file Excel
                    for (int col = 1; col <= colCount; col++)
                    {
                        string sqlQuery = xls.GetCellValue(1, col)?.ToString();// Lấy dữ liệu ở ô A1,B1,C1,...
                        LogSystem.Debug("SQL Query: " + sqlQuery);
                        if (!string.IsNullOrWhiteSpace(sqlQuery))
                        {
                            // Lấy dữ liệu từ cơ sở dữ liệu bằng câu truy vấn SQL
                            var data = await dbHelper.ExecuteQueryAsync(sqlQuery, reader =>
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                }
                                return row;
                            });

                            // Kiểm tra nếu dữ liệu trả về không rỗng
                            if (data == null || data.Count == 0)
                            {
                                return (false, "No data found for query:" + sqlQuery);
                            }

                            // Chuyển đổi dữ liệu thành DataTable và thêm vào báo cáo
                            DataTable dataTable = ConvertToDataTable(data);
                            rp.AddTable("Report"+col, dataTable);
                        }
                    }

                    // Chạy báo cáo và lưu kết quả
                    rp.Run(xls);
                    xls.Save(outPath);

                    return (true, "Report generated successfully.");
                }
            }
            catch (Exception ex)
            {
                error = "Error occurred: "+ ex.Message;
                LogSystem.Error(ex);
                return (false, error);
            }
        }


        private static DataTable ConvertToDataTable(List<Dictionary<string, object>> data)
        {
            DataTable table = new DataTable();

            if (data.Count > 0)
            {
                // Thêm cột vào DataTable
                foreach (var column in data[0].Keys)
                {
                    table.Columns.Add(column);
                }

                // Thêm dữ liệu vào DataTable
                foreach (var row in data)
                {
                    var newRow = table.NewRow();
                    foreach (var column in row.Keys)
                    {
                        newRow[column] = row[column] ?? DBNull.Value;
                    }
                    table.Rows.Add(newRow);
                }
            }
            return table;
        }

        private static bool SaveReportData(SMG.Models.Report reportData, string Filter, string generatecode,string username)
        {
            bool result = true;
            try
            {
                SMN.DBHelper.DBHelper dBHelper = new SMN.DBHelper.DBHelper();
                SMG.Models.ReportDetail report = new SMG.Models.ReportDetail();
                report.REPORT_CODE = reportData.REPORT_CODE;
                report.REPORT_TYPE_CODE = reportData.REPORT_TYPE_CODE;
                report.REPORT_NAME = reportData.REPORT_NAME;
                report.REPORT_JSON_FILTER = Filter;
                report.REPORT_DETAIL_CODE = generatecode;
                report.OUTPUT_FILE_NAME = reportData.REPORT_CODE + "_" + reportData.REPORT_NAME + "_" + report.REPORT_DETAIL_CODE + ".xlsx";
                report.IS_ACTIVE = true;
                string query = "INSERT INTO SMN_REPORT_DETAIL (REPORT_TYPE_CODE, REPORT_CODE, REPORT_JSON_FILTER, REPORT_NAME, REPORT_DETAIL_CODE, CREATE_TIME, CREATOR, IS_ACTIVE, OUTPUT_FILE_NAME) " +
                               "VALUES (@REPORT_TYPE_CODE, @REPORT_CODE, @REPORT_JSON_FILTER, @REPORT_NAME, @REPORT_DETAIL_CODE, @CREATE_TIME, @CREATOR, @IS_ACTIVE, @OUTPUT_FILE_NAME)";
                var parameters = new Dictionary<string, object>
                {
                    { "@REPORT_TYPE_CODE", report.REPORT_TYPE_CODE },
                    { "@REPORT_CODE", report.REPORT_CODE },
                    { "@REPORT_JSON_FILTER", report.REPORT_JSON_FILTER },
                    { "@REPORT_NAME", report.REPORT_NAME },
                    { "@REPORT_DETAIL_CODE", report.REPORT_DETAIL_CODE },
                    { "@CREATE_TIME", DateTime.Now },
                    { "@CREATOR", username },
                    { "@IS_ACTIVE", report.IS_ACTIVE },
                    { "@OUTPUT_FILE_NAME", report.OUTPUT_FILE_NAME }
                };
                var rs = dBHelper.ExecuteNonQueryAsync(query, parameters);
                if (rs != null)
                {
                    result = rs.Result > 0;
                }
            }
            catch (Exception ex)
            {
                LogSystem.Error(ex);
                result = false;
            }
            return result;
        }

        private static async Task<SMG.Models.Report> LoadReport(string reportTypeCode, string reportCode)
        {
            SMG.Models.Report result = null;
            try
            {
                SMN.DBHelper.DBHelper dBHelper = new SMN.DBHelper.DBHelper();
                // Cập nhật câu truy vấn để lọc cả REPORT_TYPE_CODE và REPORT_CODE
                string query = "SELECT * FROM \"SMN_REPORT\" WHERE \"REPORT_TYPE_CODE\" = @ReportTypeCode AND \"REPORT_CODE\" = @ReportCode\r\n";
                var parameters = new Dictionary<string, object>
                    {
                        { "@ReportTypeCode", reportTypeCode },
                        { "@ReportCode", reportCode }
                    };

                // Chạy câu lệnh với các tham số truyền vào
                var reports = await dBHelper.ExecuteQueryWithParametersAsync(query, parameters, reader => new SMG.Models.Report
                {
                    REPORT_CODE = reader["REPORT_CODE"].ToString(),
                    REPORT_NAME = reader["REPORT_NAME"].ToString(),
                    REPORT_GROUP_ID = reader["REPORT_GROUP_ID"]?.ToString(),
                    REPORT_FILE_NAME = reader["REPORT_FILE_NAME"]?.ToString(),
                    REPORT_TYPE_CODE = reader["REPORT_TYPE_CODE"]?.ToString(),
                    CREATE_TIME = Convert.ToInt64(reader["CREATE_TIME"]),
                    CREATOR = reader["CREATOR"].ToString(),
                    MODIFIER = reader["MODIFIER"]?.ToString(),
                    MODIFY_TIME = reader["MODIFY_TIME"] != DBNull.Value ? Convert.ToInt64(reader["MODIFY_TIME"]) : (long?)null,
                    IS_ACTIVE = Convert.ToInt32(reader["IS_ACTIVE"]) == 1
                });

                // Kiểm tra nếu có dữ liệu, lấy phần tử đầu tiên
                if (reports != null && reports.Count > 0)
                {
                    result = reports.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                LogSystem.Error(ex);
            }
            return result;
        }



        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            // Get the directory where the executable is running
            string baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // Specify the Libs directory
            string libsDirectory = Path.Combine(baseDirectory, "Libs");

            // Extract the assembly name
            string assemblyName = new AssemblyName(args.Name).Name;

            // Ensure the file extension is included for loading the assembly
            string assemblyPath = Path.Combine(libsDirectory, assemblyName + ".dll");

            // Check if the DLL exists in the Libs directory and load it if available
            if (File.Exists(assemblyPath))
            {
                return Assembly.LoadFrom(assemblyPath);
            }

            // Return null if the assembly is not found
            return null;
        }
    }
}