using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMN.TokenHelper
{
    public class TokenHelper
    {
        private static string SecretKey = "thinhvipnghean"; // Đây phải là key mà bạn dùng trong Flask (nên để trong cấu hình)

        // Phương thức để giải mã JWT token và lấy thông tin từ đó
        public static (string username, DateTime? expiration, string role) GetUsernameRoleAndExpirationFromJwt(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;

                // Truy xuất thông tin từ token
                var username = jsonToken?.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                var role = jsonToken?.Claims.FirstOrDefault(c => c.Type == "role")?.Value; // Lấy thông tin role
                var expiration = jsonToken?.ValidTo;

                return (username, expiration, role);
            }
            catch (Exception ex)
            {
                // Xử lý lỗi nếu có
                Console.WriteLine("Error decoding JWT: " + ex.Message);
                return (null, null, null);
            }
        }

        // Phương thức kiểm tra xem token đã hết hạn chưa
        public static bool IsTokenExpired(string token)
        {
            var (_, expiration, _) = GetUsernameRoleAndExpirationFromJwt(token);
            if (expiration.HasValue)
            {
                return DateTime.UtcNow >= expiration.Value;
            }
            return true; // Nếu không có thời gian hết hạn, cho là token đã hết hạn
        }

        public static bool ValidToken(string token)
        {
            return !IsTokenExpired(token);
        }

        // Phương thức lấy role từ token
        public static string GetRoleFromJwt(string token)
        {
            var (_, _, role) = GetUsernameRoleAndExpirationFromJwt(token);
            return role;
        }
    }
}
