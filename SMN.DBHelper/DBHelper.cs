using Npgsql;
using SMG.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SMN.DBHelper
{
    public class DBHelper:IDisposable
    {
        private readonly string _connectionString;
        private NpgsqlConnection _connection;

        // Constructor để khởi tạo chuỗi kết nối
        public DBHelper()
        {
            string connectionString = "Host=localhost;Port=1521;Username=postgres;Password=Thinh1637;Database=SMN_RS;MaxPoolSize=100;Timeout=60;";

            _connectionString = connectionString;
            OpenConnectionAsync().Wait();

        }
        public async Task<NpgsqlConnection> OpenConnectionAsync()
        {
            try
            {
                LogSystem.Info("Checking connection state...");

                if (_connection == null || _connection.State != ConnectionState.Open)
                {
                    LogSystem.Info("Opening new connection...");
                    _connection = new NpgsqlConnection(_connectionString);
                    _connection.Open();
                    LogSystem.Info("Connection opened successfully.");
                }
                else
                {
                    LogSystem.Info("Connection already open.");
                }

                return _connection;
            }
            catch (Exception ex)
            {
                LogSystem.Error($"Failed to open connection: {ex.Message}");
                throw; // Không cần return null vì lỗi sẽ bị bắt ở nơi gọi hàm.
            }
        }


        // Phương thức thực hiện truy vấn bất đồng bộ
        public async Task<List<T>> ExecuteQueryAsync<T>(string query, Func<NpgsqlDataReader, T> mapFunction)
        {
            var result = new List<T>();

            try
            {

                using (var connection = await OpenConnectionAsync())
                {
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                result.Add(mapFunction((NpgsqlDataReader)reader));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogSystem.Error(ex);
            }

            return result;
        }

        // Phương thức thực hiện câu lệnh INSERT hoặc UPDATE
        public async Task<int> ExecuteNonQueryAsync(string query, Dictionary<string, object> parameters)
        {
            int affectedRows = 0;

            try
            {
                using (var connection = await OpenConnectionAsync())
                {
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        foreach (var param in parameters)
                        {
                            command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                        }
                        affectedRows = await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                LogSystem.Error(ex);
            }


            return affectedRows;
        }
        public async Task<List<T>> ExecuteQueryWithParametersAsync<T>(string query, Dictionary<string, object> parameters, Func<NpgsqlDataReader, T> mapFunction)
        {
            var result = new List<T>();

            try
            {
                NpgsqlConnection connection = null;
                if (_connection == null || _connection.State != ConnectionState.Open)
                {
                    connection = await OpenConnectionAsync();
                }
                else
                {
                    connection = _connection;
                }

                LogSystem.Debug("Run SQL: " + query);
                using (var command = new NpgsqlCommand(query, connection))
                {
                    // Kiểm tra nếu parameters != null và có phần tử
                    if (parameters != null && parameters.Count > 0)
                    {
                        foreach (var param in parameters)
                        {
                            command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                        }
                    }

                    using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            result.Add(mapFunction((NpgsqlDataReader)reader));
                        }
                    }
                }
                LogSystem.Debug("Run SQL END" );

            }
            catch (Exception ex)
            {
                LogSystem.Error(ex);
            }

            return result;
        }
        public static T MapToObject<T>(IDataRecord reader) where T : new()
        {
            var obj = new T();

            foreach (var prop in typeof(T).GetProperties())
            {
                // Kiểm tra nếu cột có tồn tại trong reader
                if (!reader.HasColumn(prop.Name)) continue;

                var value = reader[prop.Name];
                if (value != DBNull.Value)
                {
                    prop.SetValue(obj, value);
                }
            }

            return obj;
        }

        public void Dispose()
        {
            CloseConnection();
        }
        public void CloseConnection()
        {
            try
            {
                if (_connection != null)
                {
                    if (_connection.State == System.Data.ConnectionState.Open)
                    {
                        LogSystem.Info("Closing connection...");
                        _connection.Close();
                        LogSystem.Info("Connection closed successfully.");
                    }

                    // Giải phóng tài nguyên
                    _connection.Dispose();
                    _connection = null; // Đặt về null để tránh sử dụng lại kết nối đã đóng
                }
            }
            catch (Exception ex)
            {
                LogSystem.Error($"Error while closing connection: {ex.Message}");
                LogSystem.Error(ex); // Log stack trace
            }
        }

    }

}