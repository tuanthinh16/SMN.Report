using Npgsql;
using SMG.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SMN.DBHelper
{
    public class DBHelper : IDisposable
    {
        private readonly string _connectionString;
        private NpgsqlConnection _connection;

        // Semaphore để giới hạn số lượng thread kết nối đồng thời
        private static SemaphoreSlim semaphore = new SemaphoreSlim(20); // Ví dụ: tối đa 20 luồng

        // Constructor để khởi tạo chuỗi kết nối
        public DBHelper()
        {
            string connectionString = "Host=localhost;Port=1521;Username=postgres;Password=Thinh1637;Database=SMN_RS";

            _connectionString = connectionString;
        }
        public void Dispose()
        {
            // Đóng kết nối khi đối tượng được giải phóng
            if (_connection != null)
            {
                _connection.Close();
                _connection.Dispose();
            }
        }
        public async Task<NpgsqlConnection> OpenConnectionAsync()
        {
            await semaphore.WaitAsync();
            try
            {
                if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
                {
                    _connection = new NpgsqlConnection(_connectionString);
                    _connection.Open();
                }
            }
            catch (Exception ex)
            {
                LogSystem.Error(ex);
                semaphore.Release();
                throw;
            }
            return _connection;
        }

        // Phương thức đóng kết nối
        public void CloseConnection()
        {
            try
            {
                if (_connection != null && _connection.State == System.Data.ConnectionState.Open)
                {
                    _connection.Close();
                    _connection.Dispose();
                }
            }
            catch (Exception ex)
            {
                LogSystem.Error(ex);
            }
            finally
            {
                semaphore.Release();  // Giải phóng semaphore sau khi kết thúc kết nối
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
            finally
            {
                CloseConnection();
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
            finally
            {
                CloseConnection();
            }

            return affectedRows;
        }
        public async Task<List<T>> ExecuteQueryWithParametersAsync<T>(string query, Dictionary<string, object> parameters, Func<NpgsqlDataReader, T> mapFunction)
        {
            var result = new List<T>();

            try
            {
                using (var connection = await OpenConnectionAsync())
                {
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
            finally
            {
                CloseConnection();
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

    }
    
}