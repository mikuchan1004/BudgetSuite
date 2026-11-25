using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MySqlConnector;

namespace BudgetSuite
{
    public static class DatabaseService
    {
        private sealed class DbConfig
        {
            public string? Host { get; set; }
            public int Port { get; set; } = 3306;
            public string? User { get; set; }
            public string? Password { get; set; }
            public string? Database { get; set; }
            public string? SslMode { get; set; }
        }

        private static readonly string? ConnectionString = LoadConnectionString();

        public static bool IsConfigured => !string.IsNullOrEmpty(ConnectionString);

        public static DateTime LastDbUpdatedTime { get; set; } = DateTime.MinValue;

        private static string? LoadConnectionString()
        {
            try
            {
                string[] candidates =
                [
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db_config.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "db_config.json")
                ];

                string? configPath = null;
                foreach (var path in candidates)
                {
                    if (File.Exists(path))
                    {
                        configPath = path;
                        break;
                    }
                }

                if (configPath == null) return null;

                string json = File.ReadAllText(configPath, Encoding.UTF8);
                var cfg = JsonSerializer.Deserialize<DbConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (cfg == null || string.IsNullOrWhiteSpace(cfg.Host) || string.IsNullOrWhiteSpace(cfg.User))
                {
                    return null;
                }

                var sslMode = MySqlSslMode.Preferred;
                if (!string.IsNullOrWhiteSpace(cfg.SslMode) &&
                    Enum.TryParse<MySqlSslMode>(cfg.SslMode, ignoreCase: true, out var parsedSsl))
                {
                    sslMode = parsedSsl;
                }

                var builder = new MySqlConnectionStringBuilder
                {
                    Server = cfg.Host.Trim(),
                    Port = cfg.Port > 0 ? (uint)cfg.Port : 3306,
                    Database = string.IsNullOrWhiteSpace(cfg.Database) ? "budgetsuite" : cfg.Database.Trim(),
                    UserID = cfg.User.Trim(),
                    Password = cfg.Password ?? string.Empty,
                    CharacterSet = "utf8mb4",
                    SslMode = sslMode,
                    Pooling = true,
                    MinimumPoolSize = 1,
                    MaximumPoolSize = 5,
                    ConnectionTimeout = 5
                };

                return builder.ConnectionString;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Config load error: {ex.Message}");
                return null;
            }
        }

        public static bool TestConnection()
        {
            if (!IsConfigured) return false;
            try
            {
                using var conn = new MySqlConnection(ConnectionString);
                conn.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static AppData? LoadFromDatabase()
        {
            if (!IsConfigured) return null;
            try
            {
                using var conn = new MySqlConnection(ConnectionString);
                conn.Open();

                var data = new AppData
                {
                    LastSavedDate = DateTime.Today.ToString("yyyy-MM-dd"),
                    LastActivateMonth = DateTime.Today.ToString("yyyy-MM")
                };

                // Multi-result query: combines 4 round-trips into 1 single round-trip
                const string sql = @"
                    SELECT current_bank, target_balance, naver_points, kakao_pay_money, hold_amount, updated_at FROM wallets WHERE id = 1 LIMIT 1;
                    SELECT tx_date, tx_time, item_type, description, amount, payment_method FROM transactions ORDER BY tx_date DESC, tx_time DESC;
                    SELECT name, amount, is_paid, active_month FROM fixed_expenses;
                    SELECT plan_type, name, amount FROM plans;
                ";

                using var cmd = new MySqlCommand(sql, conn);
                using var reader = cmd.ExecuteReader();

                // 1. Wallets
                if (reader.Read())
                {
                    data.CurrentBank = reader.GetDecimal(0);
                    data.TargetBalance = reader.GetDecimal(1);
                    data.NaverPoints = reader.GetDecimal(2);
                    data.KakaoPayMoney = reader.GetDecimal(3);
                    data.HoldAmount = reader.GetDecimal(4);
                    if (!reader.IsDBNull(5))
                    {
                        LastDbUpdatedTime = reader.GetDateTime(5);
                    }
                }

                // 2. Transactions
                reader.NextResult();
                data.AllExpenseHistory = new List<ExpenseItem>();
                while (reader.Read())
                {
                    var dt = reader.GetDateTime(0);
                    data.AllExpenseHistory.Add(new ExpenseItem
                    {
                        Date = dt.ToString("yyyy-MM-dd"),
                        Time = reader.GetString(1),
                        ItemType = reader.GetString(2),
                        Description = reader.GetString(3),
                        Amount = reader.GetDecimal(4),
                        PaymentMethod = reader.GetString(5)
                    });
                }

                // 3. Fixed Expenses (Deduplicated)
                reader.NextResult();
                data.FixedExpenses = new List<FixedExpenseItem>();
                var seenFeRead = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string? loadedActiveMonth = null;
                while (reader.Read())
                {
                    string name = reader.GetString(0).Trim();
                    if (!string.IsNullOrEmpty(name) && seenFeRead.Add(name))
                    {
                        data.FixedExpenses.Add(new FixedExpenseItem
                        {
                            Name = name,
                            Amount = reader.GetDecimal(1),
                            IsPaid = reader.GetBoolean(2)
                        });
                        if (loadedActiveMonth == null && !reader.IsDBNull(3))
                        {
                            loadedActiveMonth = reader.GetString(3);
                        }
                    }
                }
                if (!string.IsNullOrEmpty(loadedActiveMonth))
                {
                    data.LastActivateMonth = loadedActiveMonth;
                }

                // 4. Plans
                reader.NextResult();
                data.IncomePlans = new List<PlanItem>();
                data.InvestmentPlans = new List<PlanItem>();
                while (reader.Read())
                {
                    string type = reader.GetString(0);
                    var plan = new PlanItem
                    {
                        Name = reader.GetString(1),
                        Amount = reader.GetDecimal(2)
                    };
                    if (type == "INCOME")
                        data.IncomePlans.Add(plan);
                    else
                        data.InvestmentPlans.Add(plan);
                }

                return data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Load error: {ex.Message}");
                return null;
            }
        }

        public static void SaveToDatabaseAsync(AppData data)
        {
            if (!IsConfigured) return;
            Task.Run(() => SaveToDatabase(data));
        }

        public static void SaveToDatabase(AppData data)
        {
            if (!IsConfigured) return;
            try
            {
                using var conn = new MySqlConnection(ConnectionString);
                conn.Open();
                using var trans = conn.BeginTransaction();

                // 1. Wallets
                using (var cmd = new MySqlCommand(@"
                    INSERT INTO wallets (id, current_bank, target_balance, naver_points, kakao_pay_money, hold_amount)
                    VALUES (1, @bank, @target, @naver, @kakao, @hold)
                    ON DUPLICATE KEY UPDATE 
                        current_bank = @bank, 
                        target_balance = @target, 
                        naver_points = @naver, 
                        kakao_pay_money = @kakao, 
                        hold_amount = @hold,
                        updated_at = CURRENT_TIMESTAMP;", conn, trans))
                {
                    cmd.Parameters.AddWithValue("@bank", data.CurrentBank);
                    cmd.Parameters.AddWithValue("@target", data.TargetBalance);
                    cmd.Parameters.AddWithValue("@naver", data.NaverPoints);
                    cmd.Parameters.AddWithValue("@kakao", data.KakaoPayMoney);
                    cmd.Parameters.AddWithValue("@hold", data.HoldAmount);
                    cmd.ExecuteNonQuery();
                }

                // 2. Fixed Expenses (Deduplicated Batch insert)
                using (var cmd = new MySqlCommand("DELETE FROM fixed_expenses;", conn, trans))
                {
                    cmd.ExecuteNonQuery();
                }

                var fixedList = data.FixedExpenses ?? [];
                var seenFeSave = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var uniqueFixed = new List<FixedExpenseItem>();
                foreach (var fe in fixedList)
                {
                    if (fe != null && !string.IsNullOrWhiteSpace(fe.Name) && seenFeSave.Add(fe.Name.Trim()))
                    {
                        uniqueFixed.Add(fe);
                    }
                }

                if (uniqueFixed.Count > 0)
                {
                    var sb = new StringBuilder("INSERT INTO fixed_expenses (name, amount, is_paid, active_month) VALUES ");
                    using var cmd = new MySqlCommand("", conn, trans);
                    for (int i = 0; i < uniqueFixed.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append($"(@n{i}, @a{i}, @p{i}, @m{i})");
                        cmd.Parameters.AddWithValue($"@n{i}", uniqueFixed[i].Name.Trim());
                        cmd.Parameters.AddWithValue($"@a{i}", uniqueFixed[i].Amount);
                        cmd.Parameters.AddWithValue($"@p{i}", uniqueFixed[i].IsPaid ? 1 : 0);
                        cmd.Parameters.AddWithValue($"@m{i}", DateTime.Today.ToString("yyyy-MM"));
                    }
                    cmd.CommandText = sb.ToString();
                    cmd.ExecuteNonQuery();
                }

                // 3. Plans (Batch insert)
                using (var cmd = new MySqlCommand("DELETE FROM plans;", conn, trans))
                {
                    cmd.ExecuteNonQuery();
                }

                var allPlans = new List<(string type, PlanItem plan)>();
                foreach (var ip in data.IncomePlans ?? []) allPlans.Add(("INCOME", ip));
                foreach (var iv in data.InvestmentPlans ?? []) allPlans.Add(("INVEST", iv));

                if (allPlans.Count > 0)
                {
                    var sb = new StringBuilder("INSERT INTO plans (plan_type, name, amount) VALUES ");
                    using var cmd = new MySqlCommand("", conn, trans);
                    for (int i = 0; i < allPlans.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append($"(@t{i}, @pn{i}, @pa{i})");
                        cmd.Parameters.AddWithValue($"@t{i}", allPlans[i].type);
                        cmd.Parameters.AddWithValue($"@pn{i}", allPlans[i].plan.Name);
                        cmd.Parameters.AddWithValue($"@pa{i}", allPlans[i].plan.Amount);
                    }
                    cmd.CommandText = sb.ToString();
                    cmd.ExecuteNonQuery();
                }

                // 4. Transactions (Batch insert)
                using (var cmd = new MySqlCommand("DELETE FROM transactions;", conn, trans))
                {
                    cmd.ExecuteNonQuery();
                }

                var txList = data.AllExpenseHistory ?? [];
                if (txList.Count > 0)
                {
                    var sb = new StringBuilder("INSERT INTO transactions (tx_date, tx_time, item_type, description, amount, payment_method) VALUES ");
                    using var cmd = new MySqlCommand("", conn, trans);
                    for (int i = 0; i < txList.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append($"(@d{i}, @tm{i}, @it{i}, @dc{i}, @am{i}, @pm{i})");
                        cmd.Parameters.AddWithValue($"@d{i}", txList[i].Date);
                        cmd.Parameters.AddWithValue($"@tm{i}", txList[i].Time);
                        cmd.Parameters.AddWithValue($"@it{i}", txList[i].ItemType);
                        cmd.Parameters.AddWithValue($"@dc{i}", txList[i].Description);
                        cmd.Parameters.AddWithValue($"@am{i}", txList[i].Amount);
                        cmd.Parameters.AddWithValue($"@pm{i}", txList[i].PaymentMethod);
                    }
                    cmd.CommandText = sb.ToString();
                    cmd.ExecuteNonQuery();
                }

                trans.Commit();

                try
                {
                    using var tsCmd = new MySqlCommand("SELECT updated_at FROM wallets WHERE id = 1 LIMIT 1;", conn);
                    var ts = tsCmd.ExecuteScalar();
                    if (ts != null && ts != DBNull.Value)
                    {
                        LastDbUpdatedTime = Convert.ToDateTime(ts);
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Save error: {ex.Message}");
            }
        }

        public static DateTime? GetLatestDbTimestamp()
        {
            if (!IsConfigured) return null;
            try
            {
                using var conn = new MySqlConnection(ConnectionString);
                conn.Open();
                using var cmd = new MySqlCommand("SELECT updated_at FROM wallets WHERE id = 1 LIMIT 1;", conn);
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    return Convert.ToDateTime(result);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Timestamp check error: {ex.Message}");
            }
            return null;
        }
    }
}
