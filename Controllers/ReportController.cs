using Newtonsoft.Json.Linq;
using SMG.Logging;
using SMN.Report.Processor;
using SMN.TokenHelper;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http.Headers;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;

namespace SMN.Report.Controllers
{
    public class ReportController : ApiController
    {
        // GET: Report
        public IEnumerable<string> Get()
        {
            LogSystem.Debug("Get Report");
            return new string[] { "value1", "value2" };
        }
        [System.Web.Mvc.Authorize]
        public IHttpActionResult Post([FromBody] JObject jsonData)
        {
            if (jsonData == null)
            {
                return BadRequest("Invalid JSON data.");
            }
            var token = HttpContext.Current.Request.Headers["Authorization"]?.Replace("Bearer ", "");

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized();
            }
            if(!TokenHelper.TokenHelper.ValidToken(token))
            {
                var challenge = new AuthenticationHeaderValue("Bearer", "Token");
                return Unauthorized(challenge);
            }
            var username = TokenHelper.TokenHelper.GetUsernameRoleAndExpirationFromJwt(token).username;

            if (username == null)
            {
                return Unauthorized();
            }
            var role = TokenHelper.TokenHelper.GetUsernameRoleAndExpirationFromJwt(token).role.ToLower();
            if (role != "admin" && role != "staff")// Tạm thời để nhân viên và admin chỉ có thể tạo
            {
                return BadRequest("You do not have permission to create report. Contact to STAFF or ADMINISTRATOR");
            }
            var reportTypeCode = jsonData["REPORT_TYPE_CODE"]?.ToString();
            var reportCode = jsonData["REPORT_CODE"]?.ToString();
            var jsonFilter = jsonData.ToString();
            string outputFile = "";
            if (!string.IsNullOrEmpty(reportTypeCode) || !string.IsNullOrEmpty(jsonFilter))
            {
                CreateReport createReport = new CreateReport(reportTypeCode, jsonFilter,username);
                bool result = createReport.Create();
                if(!result)
                {
                    return BadRequest(createReport.GetError());
                }
                outputFile = createReport.GetOutputFile();
                
            }
            return Ok(new
            {
                message = "Create successfully",
                REPORT_TYPE_CODE = reportTypeCode,
                OUTPUT_FILE = outputFile,
                REPORT_CODE= reportCode
            });
        }
    }
}