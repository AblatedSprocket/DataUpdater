using DataUpdater.Utilities;
using Finance.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DataUpdater.Finance.Repositories
{
    public class TransactionRepository : ITransactionRepository
    {
        private string _databaseConn;
        public TransactionRepository(IConfiguration config)
        {
            string directory = config.GetSection("Controllers:DatabasePaths")["Finance"];
            SetDatabaseConnectionString(directory);
        }

        static TransactionRepository()
        {
            SQLitePCL.Batteries_V2.Init();
        }

        public Dictionary<int, string> AddBulkTransactions(IEnumerable<Transaction> transactions)
        {
            try
            {
                List<Transaction> newTransactions = new List<Transaction>();
                foreach (Transaction transaction in transactions)
                {
                    if (!TransactionExists(transaction))
                    {
                        newTransactions.Add(transaction);
                    }
                }
                DateTime addDateTime = DateTime.Now;
                using (SqliteConnection conn = new SqliteConnection(_databaseConn))
                {
                    conn.Open();
                    using (SqliteTransaction trans = conn.BeginTransaction())
                    {
                        using (SqliteCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = @"
                        INSERT INTO Transactions
                        (Vendor, Amount, Type, Category, TransactionDate, PostDate, Description, Account, SerialNumber)
                        VALUES (@VENDOR, @AMOUNT, @TYPE, @CATEGORY, @TRANSACTIONDATE, @POSTDATE, @DESCRIPTION, @ACCOUNT, @SERIALNUMBER)";
                            foreach (Transaction transaction in newTransactions)
                            {
                                cmd.Parameters.Add("@VENDOR", SqliteType.Text).Value = transaction.Vendor;
                                cmd.Parameters.Add("@AMOUNT", SqliteType.Text).Value = transaction.Amount.ToSqliteStorage();
                                cmd.Parameters.Add("@TYPE", SqliteType.Text).Value = transaction.Type.ToString();
                                cmd.Parameters.Add("@CATEGORY", SqliteType.Text).Value = transaction.Category.ToString();
                                cmd.Parameters.Add("@TRANSACTIONDATE", SqliteType.Text).Value = transaction.TransactionDate.ToSqliteStorage();
                                cmd.Parameters.Add("@POSTDATE", SqliteType.Text).Value = transaction.PostDate.ToSqliteStorage();
                                cmd.Parameters.Add("@DESCRIPTION", SqliteType.Text).Value = transaction.Description;
                                cmd.Parameters.Add("@ACCOUNT", SqliteType.Text).Value = transaction.Account;
                                cmd.Parameters.Add("@SERIALNUMBER", SqliteType.Text).Value = transaction.SerialNumber;
                                cmd.ExecuteNonQuery();
                                cmd.Parameters.Clear();
                            }

                        }
                        trans.Commit();
                    }
                }
                using (SqliteConnection conn = new SqliteConnection(_databaseConn))
                {
                    Dictionary<int, string> addedTransactions = new Dictionary<int, string>();
                    conn.Open();
                    string sql = @"
                                SELECT Id, PostDate || ' ' || Description AS Details, DateEntered FROM Transactions
                                WHERE DATETIME(DateEntered) >= DATETIME(@ADDDATE)";
                    SqliteCommand cmd = new SqliteCommand(sql, conn);
                    cmd.Parameters.Add("@ADDDATE", SqliteType.Text).Value = addDateTime.ToSqliteStorage();
                    SqliteDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        addedTransactions.Add(Convert.ToInt32(reader["Id"]), reader["Details"].ToString());
                    }
                    return addedTransactions;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public int? AddTransaction(Transaction transaction)
        {
            if (!TransactionExists(transaction))
            {
                int id = 0;
                using (SqliteConnection conn = new SqliteConnection(_databaseConn))
                {
                    conn.Open();
                    DateTime addDateTime = DateTime.Now;
                    string sql = @"
                        INSERT INTO Transactions
                        (Vendor, Amount, Type, Category, TransactionDate, PostDate, Description, Account, SerialNumber)
                        VALUES (@VENDOR, @AMOUNT, @TYPE, @CATEGORY, @TRANSACTIONDATE, @POSTDATE, @DESCRIPTION, @ACCOUNT, @SERIALNUMBER)";
                    SqliteCommand cmd = new SqliteCommand(sql, conn);
                    cmd.Parameters.Add("@VENDOR", SqliteType.Text).Value = transaction.Vendor;
                    cmd.Parameters.Add("@AMOUNT", SqliteType.Text).Value = transaction.Amount.ToSqliteStorage();
                    cmd.Parameters.Add("@TYPE", SqliteType.Text).Value = transaction.Type.ToString();
                    cmd.Parameters.Add("@CATEGORY", SqliteType.Text).Value = transaction.Category.ToString();
                    cmd.Parameters.Add("@TRANSACTIONDATE", SqliteType.Text).Value = transaction.TransactionDate.ToSqliteStorage();
                    cmd.Parameters.Add("@POSTDATE", SqliteType.Text).Value = transaction.PostDate.ToSqliteStorage();
                    cmd.Parameters.Add("@DESCRIPTION", SqliteType.Text).Value = transaction.Description;
                    cmd.Parameters.Add("@ACCOUNT", SqliteType.Text).Value = transaction.Account;
                    cmd.Parameters.Add("@SERIALNUMBER", SqliteType.Text).Value = transaction.SerialNumber;
                    cmd.ExecuteNonQuery();
                    sql = @"
                        SELECT Id FROM Transactions
                        WHERE Vendor = @VENDOR
                            AND Amount = @AMOUNT
                            AND Type = @TYPE
                            AND PostDate = @POSTDATE
                            AND DATETIME(DateEntered) > DATETIME(@DATEENTERED);";
                    cmd = new SqliteCommand(sql, conn);
                    cmd.Parameters.Add("@VENDOR", SqliteType.Text).Value = transaction.Vendor;
                    cmd.Parameters.Add("@AMOUNT", SqliteType.Text).Value = transaction.Amount.ToSqliteStorage();
                    cmd.Parameters.Add("@TYPE", SqliteType.Text).Value = transaction.Type;
                    cmd.Parameters.Add("@POSTDATE", SqliteType.Text).Value = transaction.PostDate.ToSqliteStorage();
                    cmd.Parameters.Add("@DATEENTERED", SqliteType.Text).Value = addDateTime;
                    id = Convert.ToInt32(cmd.ExecuteScalar());
                }
                return id;
            }
            else
            {
                return null;
            }
        }

        public bool DeleteTransaction(int transactionId)
        {
            if (TransactionExists(transactionId))
            {
                using (SqliteConnection conn = new SqliteConnection(_databaseConn))
                {
                    conn.Open();
                    string sql = @"
                        DELETE FROM Transactions 
                        WHERE Id = @ID";
                    SqliteCommand cmd = new SqliteCommand(sql, conn);
                    cmd.Parameters.Add("@ID", SqliteType.Integer).Value = transactionId;
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            return false;
        }

        public CostBreakdown GetLifetimeCostBreakdown()
        {
            using (SqliteConnection conn = new SqliteConnection(_databaseConn))
            {
                conn.Open();
                SqliteCommand cmd = new SqliteCommand();
                cmd.Connection = conn;
                StringBuilder sql = new StringBuilder("SELECT ");
                int index = 0;
                foreach (Category category in Enum.GetValues(typeof(Category)))
                {
                    if (index != 0)
                    {
                        sql.Append(", ");
                    }
                    sql.Append($"SUM(CASE WHEN Category = @category{index} AND Type = 'Credit' THEN -Amount WHEN Category = @category{index} AND Type = 'Debit' THEN Amount ELSE 0 END) AS {category.ToString()}");
                    cmd.Parameters.Add($"category{index}", SqliteType.Text).Value = category.ToString();
                    index++;
                }
                sql.Append(" FROM Transactions");
                cmd.CommandText = sql.ToString();
                SqliteDataReader reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    CostBreakdown costBreakdown = new CostBreakdown();
                    costBreakdown.Auto = reader["Auto"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Dining = reader["Dining"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Grocery = reader["Grocery"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Home = reader["Home"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Interest = -reader["Interest"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Loans = reader["Loans"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Luxury = reader["Luxury"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Misc = reader["Misc"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Mortgage = reader["Mortgage"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Paycheck = -reader["Paycheck"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Travel = reader["Travel"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Utilities = reader["Utilities"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Work = reader["Work"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Payments = reader["Payment"].ConvertSqliteStorageToDecimal();
                    return costBreakdown;
                }
            }
            return null;
        }

        public IEnumerable<CostBreakdown> GetMonthCostBreakdowns()
        {
            List<CostBreakdown> costBreakdowns = new List<CostBreakdown>();
            using (SqliteConnection conn = new SqliteConnection(_databaseConn))
            {
                conn.Open();
                SqliteCommand cmd = new SqliteCommand();
                cmd.Connection = conn;
                StringBuilder sql = new StringBuilder("SELECT ");
                int index = 0;
                foreach (Category category in Enum.GetValues(typeof(Category)))
                {
                    sql.Append($"SUM(CASE WHEN Category = @category{index} AND Type = 'Credit' THEN -Amount WHEN Category = @category{index} AND Type = 'Debit' THEN Amount ELSE 0 END) AS {category.ToString()}, ");
                    cmd.Parameters.Add($"category{index}", SqliteType.Text).Value = category.ToString();
                    index++;
                }
                sql.Append("strftime('%Y-%m', PostDate) as yearmonth FROM Transactions GROUP BY yearmonth ORDER BY yearmonth ASC");
                cmd.CommandText = sql.ToString();
                SqliteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    CostBreakdown costBreakdown = new CostBreakdown();
                    costBreakdown.Auto = reader["Auto"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Dining = reader["Dining"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Grocery = reader["Grocery"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Home = reader["Home"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Interest = -reader["Interest"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Loans = reader["Loans"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Luxury = reader["Luxury"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Misc = reader["Misc"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Mortgage = reader["Mortgage"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Paycheck = -reader["Paycheck"].ConvertSqliteStorageToDecimal();
                    costBreakdown.TimePeriod = Convert.ToDateTime(reader["yearmonth"]);
                    costBreakdown.Travel = reader["Travel"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Utilities = reader["Utilities"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Work = reader["Work"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Payments = reader["Payment"].ConvertSqliteStorageToDecimal();
                    costBreakdowns.Add(costBreakdown);
                }
                return costBreakdowns;
            }
        }

        public IEnumerable<CostBreakdown> GetYearCostBreakdowns()
        {
            List<CostBreakdown> costBreakdowns = new List<CostBreakdown>();
            using (SqliteConnection conn = new SqliteConnection(_databaseConn))
            {
                conn.Open();
                SqliteCommand cmd = new SqliteCommand();
                cmd.Connection = conn;
                StringBuilder sql = new StringBuilder("SELECT ");
                int index = 0;
                foreach (Category category in Enum.GetValues(typeof(Category)))
                {
                    sql.Append($"SUM(CASE WHEN Category = @category{index} AND Type = 'Credit' THEN -Amount WHEN Category = @category{index} AND Type = 'Debit' THEN Amount ELSE 0 END) AS {category.ToString()}, ");
                    cmd.Parameters.Add($"category{index}", SqliteType.Text).Value = category.ToString();
                    index++;
                }
                sql.Append("strftime('%Y', PostDate) as year FROM Transactions GROUP BY year");
                cmd.CommandText = sql.ToString();
                SqliteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    CostBreakdown costBreakdown = new CostBreakdown();
                    costBreakdown.Auto = reader["Auto"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Dining = reader["Dining"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Grocery = reader["Grocery"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Home = reader["Home"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Interest = -reader["Interest"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Loans = reader["Loans"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Luxury = reader["Luxury"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Misc = reader["Misc"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Mortgage = reader["Mortgage"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Paycheck = -reader["Paycheck"].ConvertSqliteStorageToDecimal();
                    costBreakdown.TimePeriod = new DateTime(Convert.ToInt32(reader["year"]), 1, 1);
                    costBreakdown.Travel = reader["Travel"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Utilities = reader["Utilities"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Work = reader["Work"].ConvertSqliteStorageToDecimal();
                    costBreakdown.Payments = reader["Payment"].ConvertSqliteStorageToDecimal();
                    costBreakdowns.Add(costBreakdown);
                }
                return costBreakdowns;
            }
        }

        public Transaction GetTransaction(int transactionId)
        {
            if (TransactionExists(transactionId))
            {
                Transaction transaction = new Transaction();
                using (SqliteConnection conn = new SqliteConnection(_databaseConn))
                {
                    conn.Open();
                    string sql = @"
                        SELECT * FROM Transactions 
                        WHERE Id = @ID";
                    using (SqliteCommand cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.Add("@ID", SqliteType.Integer).Value = transactionId;
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                transaction = new Transaction(Convert.ToInt32(reader["Id"].ToString()))
                                {
                                    Account = reader["Account"].ToString(),
                                    Amount = reader["Amount"].ConvertSqliteStorageToDecimal(),
                                    Category = (Category)Enum.Parse(typeof(Category), reader["Category"].ToString()),
                                    Vendor = reader["Vendor"].ToString(),
                                    Description = reader["Description"].ToString(),
                                    PostDate = Convert.ToDateTime(reader["PostDate"]),
                                    TransactionDate = reader["TransactionDate"].ConvertSqliteStorageToNullableDateTime(),
                                    Type = (TransactionType)Enum.Parse(typeof(TransactionType), reader["Type"].ToString()),
                                    SerialNumber = reader["SerialNumber"].ToString(),
                                    DateEntered = Convert.ToDateTime(reader["DateEntered"]),
                                    DateModified = Convert.ToDateTime(reader["DateModified"])
                                };
                            }
                        }
                    }
                }
                return transaction;
            }
            else
            {
                return null;
            }
        }

        public IEnumerable<Transaction> GetTransactions()
        {
            List<Transaction> transactions = new List<Transaction>();
            using (SqliteConnection conn = new SqliteConnection(_databaseConn))
            {
                conn.Open();
                string sql = @"
                        SELECT * FROM Transactions";
                SqliteCommand cmd = new SqliteCommand(sql, conn);
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        transactions.Add(new Transaction(Convert.ToInt32(reader["Id"]))
                        {
                            Account = reader["Account"].ToString(),
                            Amount = reader["Amount"].ConvertSqliteStorageToDecimal(),
                            Category = (Category)Enum.Parse(typeof(Category), reader["Category"].ToString()),
                            Vendor = reader["Vendor"].ToString(),
                            Description = reader["Description"].ToString(),
                            PostDate = Convert.ToDateTime(reader["PostDate"]),
                            TransactionDate = reader["TransactionDate"].ConvertSqliteStorageToNullableDateTime(),
                            Type = (TransactionType)Enum.Parse(typeof(TransactionType), reader["Type"].ToString()),
                            SerialNumber = reader["SerialNumber"].ToString(),
                            DateEntered = Convert.ToDateTime(reader["DateEntered"]),
                            DateModified = Convert.ToDateTime(reader["DateModified"])
                        });
                    }
                }
            }
            return transactions;
        }

        public IEnumerable<Transaction> GetTransactionsByMonth(DateTime month)
        {
            List<Transaction> transactions = new List<Transaction>();
            using (SqliteConnection conn = new SqliteConnection(_databaseConn))
            {
                conn.Open();
                string sql = @"
                        SELECT * FROM Transactions
                        WHERE DATETIME(PostDate) > DATETIME(@STARTDATE)
                        AND DATETIME(PostDate) < DATETIME(@ENDDATE)";
                SqliteCommand cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.Add("@STARTDATE", SqliteType.Text).Value = month.ToSqliteStorage();
                cmd.Parameters.Add("@ENDDATE", SqliteType.Text).Value = GetEndDate(month).ToSqliteStorage();
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        transactions.Add(new Transaction(Convert.ToInt32(reader["Id"].ToString()))
                        {
                            Account = reader["Account"].ToString(),
                            Amount = reader["Amount"].ConvertSqliteStorageToDecimal(),
                            Category = (Category)Enum.Parse(typeof(Category), reader["Category"].ToString()),
                            Vendor = reader["Vendor"].ToString(),
                            Description = reader["Description"].ToString(),
                            PostDate = Convert.ToDateTime(reader["PostDate"]),
                            TransactionDate = reader["TransactionDate"].ConvertSqliteStorageToNullableDateTime(),
                            Type = (TransactionType)Enum.Parse(typeof(TransactionType), reader["Type"].ToString()),
                            SerialNumber = reader["SerialNumber"].ToString(),
                            DateEntered = Convert.ToDateTime(reader["DateEntered"]),
                            DateModified = Convert.ToDateTime(reader["DateModified"])
                        });
                    }
                }
            }
            return transactions;
        }

        public IEnumerable<Transaction> GetTransactionsByYear(int year)
        {
            List<Transaction> transactions = new List<Transaction>();
            using (SqliteConnection conn = new SqliteConnection(_databaseConn))
            {
                conn.Open();
                string sql = @"
                        SELECT * FROM Transactions
                        WHERE DATETIME(PostDate) > DATETIME(@STARTDATE)
                        AND DATETIME(PostDate) < DATETIME(@ENDDATE)";
                SqliteCommand cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.Add("@STARTDATE", SqliteType.Text).Value = new DateTime(year, 1, 1);
                cmd.Parameters.Add("@ENDDATE", SqliteType.Text).Value = new DateTime(year + 1, 1, 1);
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        transactions.Add(new Transaction(Convert.ToInt32(reader["Id"].ToString()))
                        {
                            Account = reader["Account"].ToString(),
                            Amount = reader["Amount"].ConvertSqliteStorageToDecimal(),
                            Category = (Category)Enum.Parse(typeof(Category), reader["Category"].ToString()),
                            Vendor = reader["Vendor"].ToString(),
                            Description = reader["Description"].ToString(),
                            PostDate = Convert.ToDateTime(reader["PostDate"]),
                            TransactionDate = reader["TransactionDate"].ConvertSqliteStorageToNullableDateTime(),
                            Type = (TransactionType)Enum.Parse(typeof(TransactionType), reader["Type"].ToString()),
                            SerialNumber = reader["SerialNumber"].ToString(),
                            DateEntered = Convert.ToDateTime(reader["DateEntered"]),
                            DateModified = Convert.ToDateTime(reader["DateModified"])
                        });
                    }
                }
            }
            return transactions;
        }

        public Transaction UpdateTransaction(Transaction transaction)
        {
            using (SqliteConnection conn = new SqliteConnection(_databaseConn))
            {
                conn.Open();
                string sql = @"
                        UPDATE Transactions
                        SET
                            Vendor = @VENDOR,
                            Amount = @AMOUNT,
                            Type = @TYPE,
                            Category = @CATEGORY,
                            TransactionDate = @TRANSACTIONDATE,
                            PostDate = @POSTDATE,
                            Description = @DESCRIPTION,
                            Account = @ACCOUNT,
                            SerialNumber = @SERIALNUMBER,
                            DateModified = @DATEMODIFIED
                        WHERE Id = @ID";
                DateTime dateModified = DateTime.Now;
                using (SqliteCommand cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.Add("@VENDOR", SqliteType.Text).Value = transaction.Vendor;
                    cmd.Parameters.Add("@AMOUNT", SqliteType.Text).Value = transaction.Amount.ToSqliteStorage();
                    cmd.Parameters.Add("@TYPE", SqliteType.Text).Value = transaction.Type.ToString();
                    cmd.Parameters.Add("@CATEGORY", SqliteType.Text).Value = transaction.Category.ToString();
                    cmd.Parameters.Add("@TRANSACTIONDATE", SqliteType.Text).Value = transaction.TransactionDate.ToSqliteStorage();
                    cmd.Parameters.Add("@POSTDATE", SqliteType.Text).Value = transaction.PostDate.ToSqliteStorage();
                    cmd.Parameters.Add("@DESCRIPTION", SqliteType.Text).Value = transaction.Description;
                    cmd.Parameters.Add("@ACCOUNT", SqliteType.Text).Value = transaction.Account;
                    cmd.Parameters.Add("@SERIALNUMBER", SqliteType.Text).Value = transaction.SerialNumber;
                    cmd.Parameters.Add("@DATEMODIFIED", SqliteType.Text).Value = dateModified;
                    cmd.Parameters.Add("@ID", SqliteType.Integer).Value = transaction.Id;
                    cmd.ExecuteNonQuery();
                }
                transaction.DateModified = dateModified;
            }
            return transaction;
        }

        public void UpdateTransactionVendors(Vendor vendor)
        {
            List<int> transactionIds = new List<int>();
            using (SqliteConnection conn = new SqliteConnection(_databaseConn))
            {
                conn.Open();
                string sql = @"
                        SELECT Id FROM Transactions
                        WHERE Description LIKE @DESCRIPTION";
                using (SqliteCommand cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.Add("@DESCRIPTION", SqliteType.Text).Value = $"%{vendor.TransactionKey}%";
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            transactionIds.Add(int.Parse(reader["Id"].ToString()));
                        }
                    }
                }
                foreach (int id in transactionIds)
                {
                    sql = @"
                        UPDATE Transactions
                        SET Vendor = @VENDOR,
                            Category = @CATEGORY
                        WHERE Id = @ID";
                    using (SqliteCommand cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.Add("@VENDOR", SqliteType.Text).Value = vendor.Name;
                        cmd.Parameters.Add("@CATEGORY", SqliteType.Text).Value = vendor.Category;
                        cmd.Parameters.Add("@ID", SqliteType.Text).Value = id;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public void UpdateTransactionVendors(IEnumerable<Vendor> vendors)
        {
            foreach (Vendor vendor in vendors)
            {
                UpdateTransactionVendors(vendor);
            }
        }

        private DateTime GetEndDate(DateTime date)
        {
            if (date.Month == 12)
            {
                return new DateTime(date.Year + 1, 1, 1);
            }
            else
            {
                return new DateTime(date.Year, date.Month + 1, 1);
            }
        }

        private bool SetDatabaseConnectionString(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path) && Path.GetExtension(path) == ".db")
            {
                SqliteConnectionStringBuilder builder = new SqliteConnectionStringBuilder
                {
                    DataSource = path,
                    Mode = SqliteOpenMode.ReadWriteCreate
                };
                _databaseConn = builder.ConnectionString;
                return true;
            }
            return false;
        }

        public bool TransactionExists(int transactionId)
        {
            using (SqliteConnection conn = new SqliteConnection(_databaseConn))
            {
                conn.Open();
                string sql = @"
                    SELECT COUNT(*)
                    FROM Transactions
                    WHERE Id = @ID";
                SqliteCommand cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.Add("@ID", SqliteType.Integer).Value = transactionId;
                int count = Convert.ToInt32(cmd.ExecuteScalar());
                return count != 0;
            }
        }

        public bool TransactionExists(Transaction transaction)
        {
            Transaction tran = GetTransaction(1);
            using (SqliteConnection conn = new SqliteConnection(_databaseConn))
            {
                conn.Open();
                string sql = @"
                        SELECT Id
                        FROM Transactions
                        WHERE Amount = @AMOUNT
                        AND Type = @TYPE
                        AND PostDate = @POSTDATE
                        AND Description = @DESCRIPTION";
                SqliteCommand cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.Add("@AMOUNT", SqliteType.Text).Value = transaction.Amount.ToSqliteStorage();
                cmd.Parameters.Add("@TYPE", SqliteType.Text).Value = transaction.Type.ToString();
                cmd.Parameters.Add("@POSTDATE", SqliteType.Text).Value = transaction.PostDate.ToSqliteStorage();
                cmd.Parameters.Add("@DESCRIPTION", SqliteType.Text).Value = transaction.Description;
                int count = Convert.ToInt32(cmd.ExecuteScalar());
                return count != 0;
            }
        }
    }
}
