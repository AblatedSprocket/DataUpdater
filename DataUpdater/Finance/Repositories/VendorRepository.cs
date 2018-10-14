using DataUpdater.Utilities;
using Finance.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DataUpdater.Finance.Repositories
{
    public class VendorRepository : IVendorRepository
    {
        private string _databaseConn;
        public VendorRepository(IConfiguration config)
        {
            string directory = config.GetSection("Controllers:DatabasePaths")["Finance"];
            SetDatabaseConnectionString(directory);
        }
        static VendorRepository()
        {
            SQLitePCL.Batteries_V2.Init();
        }

        public Dictionary<int, string> AddBulkVendors(IEnumerable<Vendor> vendors)
        {
            try
            {
                IEnumerable<Vendor> consolidatedDuplicates = vendors.Distinct().GroupBy(x => x.Name, (name, vends) =>
                    new Vendor
                    {
                        Name = name,
                        TransactionCount = vends.Select(v => v.TransactionCount).Sum(),
                        TransactionKey = string.Join(';', vends.SelectMany(v => v.TransactionKey.Split(';')).Distinct()),
                        Category = vends.Select(v => v.Category).First()
                    });
                IEnumerable<Vendor> nonDuplicates = vendors.Except(consolidatedDuplicates, new VendorComparer());
                IEnumerable<Vendor> unifiedVendors = nonDuplicates.Union(consolidatedDuplicates);
                List<Vendor> newVendors = new List<Vendor>();
                List<Vendor> updateVendors = new List<Vendor>();
                foreach (Vendor vendor in vendors)
                {
                    if (!VendorExists(vendor, out int existingId))
                    {
                        newVendors.Add(vendor);
                    }
                    else
                    {
                        updateVendors.Add(vendor);
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
                                INSERT INTO Vendors 
                                (Name, Category, TransactionKey, TransactionCount)
                                VALUES (@NAME, @CATEGORY, @TRANSACTIONKEY, @TRANSACTIONCOUNT)";
                            foreach (Vendor vendor in newVendors)
                            {
                                cmd.Parameters.Add("@NAME", SqliteType.Text).Value = vendor.Name;
                                cmd.Parameters.Add("@CATEGORY", SqliteType.Text).Value = vendor.Category;
                                cmd.Parameters.Add("@TRANSACTIONKEY", SqliteType.Text).Value = vendor.TransactionKey;
                                cmd.Parameters.Add("@TRANSACTIONCOUNT", SqliteType.Integer).Value = vendor.TransactionCount;
                                cmd.ExecuteNonQuery();
                                cmd.Parameters.Clear();
                            }
                            cmd.CommandText = @"
                                UPDATE Vendors
                                SET Name = @NAME,
                                    Category = @CATEGORY,
                                    TransactionKey = @TRANSACTIONKEY,
                                    TransactionCount = @TRANSACTIONCOUNT,
                                    DateModified = @DATEMODIFIED
                                WHERE Id = @ID";
                            foreach (Vendor vendor in updateVendors)
                            {
                                cmd.Parameters.Add("@NAME", SqliteType.Text).Value = vendor.Name;
                                cmd.Parameters.Add("@CATEGORY", SqliteType.Text).Value = vendor.Category;
                                cmd.Parameters.Add("@TRANSACTIONKEY", SqliteType.Text).Value = vendor.TransactionKey;
                                cmd.Parameters.Add("@TRANSACTIONCOUNT", SqliteType.Text).Value = vendor.TransactionCount;
                                cmd.Parameters.Add("@DATEMODIFIED", SqliteType.Text).Value = addDateTime;
                                cmd.Parameters.Add("@ID", SqliteType.Integer).Value = vendor.Id;
                                cmd.ExecuteNonQuery();
                                cmd.Parameters.Clear();
                            }
                        }
                        trans.Commit();
                    }

                }
                using (SqliteConnection conn = new SqliteConnection(_databaseConn))
                {
                    Dictionary<int, string> addedVendors = new Dictionary<int, string>();
                    conn.Open();
                    string sql = @"
                                SELECT Id, Name || ':' || TransactionKey As Description, DateEntered FROM Vendors
                                WHERE DATETIME(DateEntered) >= DATETIME(@ADDDATE)";
                    SqliteCommand cmd = new SqliteCommand(sql, conn);
                    cmd.Parameters.Add("@ADDDATE", SqliteType.Text).Value = addDateTime.ToSqliteStorage();
                    SqliteDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        addedVendors.Add(Convert.ToInt32(reader["Id"]), reader["Description"].ToString());
                    }
                    return addedVendors;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public int? AddVendor(Vendor vendor)
        {
            if (!VendorExists(vendor, out int existingId))
            {
                using (SqliteConnection conn = new SqliteConnection(_databaseConn))
                {
                    int id;
                    conn.Open();
                    DateTime addDateTime = DateTime.Now;
                    string sql = @"
                        INSERT INTO Vendors 
                        (Name, Category, TransactionKey, TransactionCount)
                        VALUES (@NAME, @CATEGORY, @TRANSACTIONKEY, @TRANSACTIONCOUNT)";
                    SqliteCommand cmd = new SqliteCommand(sql, conn);
                    cmd.Parameters.Add("@NAME", SqliteType.Text).Value = vendor.Name;
                    cmd.Parameters.Add("@CATEGORY", SqliteType.Text).Value = vendor.Category;
                    cmd.Parameters.Add("@TRANSACTIONKEY", SqliteType.Text).Value = vendor.TransactionKey;
                    cmd.Parameters.Add("@TRANSACTIONCOUNT", SqliteType.Integer).Value = vendor.TransactionCount;
                    cmd.ExecuteNonQuery();
                    sql = @"
                        SELECT Id FROM Vendors
                        WHERE Name = @NAME
                            AND DATETIME(DateEntered) >= DATETIME(@DATEENTERED);";
                    cmd = new SqliteCommand(sql, conn);
                    cmd.Parameters.Add("@NAME", SqliteType.Text).Value = vendor.Name;
                    cmd.Parameters.Add("@DATEENTERED", SqliteType.Text).Value = addDateTime;
                    id = Convert.ToInt32(cmd.ExecuteScalar());
                    return id;
                }
            }
            return null;
        }

        public bool DeleteVendor(int id)
        {
            if (VendorExists(id))
            {
                using (SqliteConnection conn = new SqliteConnection(_databaseConn))
                {
                    conn.Open();
                    string sql = @"
                        DELETE FROM Vendors 
                        WHERE Id = @ID";
                    SqliteCommand cmd = new SqliteCommand(sql, conn);
                    cmd.Parameters.Add("@ID", SqliteType.Integer).Value = id;
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            return false;
        }

        public Vendor GetVendor(int id)
        {
            using (SqliteConnection conn = new SqliteConnection(_databaseConn))
            {
                conn.Open();
                string sql = @"
                        SELECT * FROM Vendors 
                        WHERE Id = @ID";
                SqliteCommand cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.Add("@ID", SqliteType.Integer).Value = id;
                SqliteDataReader reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return new Vendor(Convert.ToInt32(reader["Id"].ToString()))
                    {
                        Name = reader["Name"].ToString(),
                        Category = (Category)Enum.Parse(typeof(Category), reader["Category"].ToString()),
                        TransactionKey = reader["TransactionKey"].ToString(),
                        TransactionCount = Convert.ToInt32(reader["TransactionCount"])
                    };
                }
            }
            return null;
        }

        public IEnumerable<Vendor> GetVendors()
        {
            List<Vendor> vendors = new List<Vendor>();
            using (SqliteConnection conn = new SqliteConnection(_databaseConn))
            {
                conn.Open();
                string sql = @"
                        SELECT * FROM Vendors";
                SqliteCommand cmd = new SqliteCommand(sql, conn);
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        vendors.Add(new Vendor(Convert.ToInt32(reader["Id"].ToString()))
                        {
                            Name = reader["Name"].ToString(),
                            Category = (Category)Enum.Parse(typeof(Category), reader["Category"].ToString()),
                            TransactionKey = reader["TransactionKey"].ToString(),
                            TransactionCount = Convert.ToInt32(reader["TransactionCount"]),
                            DateEntered = Convert.ToDateTime(reader["DateEntered"]),
                            DateModified = Convert.ToDateTime(reader["DateModified"])
                        });
                    }
                }
            }
            return vendors;
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

        public Vendor UpdateVendor(Vendor vendor)
        {
            using (SqliteConnection conn = new SqliteConnection(_databaseConn))
            {
                conn.Open();
                string sql = @"
                        UPDATE Vendors
                        SET
                            Name = @NAME,
                            Category = @CATEGORY,
                            TransactionKey = @TRANSACTIONKEY,
                            TransactionCount = @TRANSACTIONCOUNT,
                            DateModified = @DATEMODIFIED
                        WHERE Id = @ID";
                DateTime dateModified = DateTime.Now;
                SqliteCommand cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.Add("@NAME", SqliteType.Text).Value = vendor.Name;
                cmd.Parameters.Add("@CATEGORY", SqliteType.Text).Value = vendor.Category;
                cmd.Parameters.Add("@TRANSACTIONKEY", SqliteType.Text).Value = vendor.TransactionKey;
                cmd.Parameters.Add("@TRANSACTIONCOUNT", SqliteType.Text).Value = vendor.TransactionCount;
                cmd.Parameters.Add("@DATEMODIFIED", SqliteType.Text).Value = dateModified;
                cmd.Parameters.Add("@ID", SqliteType.Integer).Value = vendor.Id;
                cmd.ExecuteNonQuery();
                vendor.DateModified = dateModified;
                return vendor;
            }
        }
        public bool VendorExists(int vendorId)
        {
            using (SqliteConnection conn = new SqliteConnection(_databaseConn))
            {
                conn.Open();
                string sql = @"
                    SELECT COUNT(*)
                    FROM Vendors
                    WHERE Id = @ID";
                SqliteCommand cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.Add("@ID", SqliteType.Integer).Value = vendorId;
                int count = Convert.ToInt32(cmd.ExecuteScalar());
                return count != 0;
            }
        }
        public bool VendorExists(Vendor vendor, out int id)
        {
            using (SqliteConnection conn = new SqliteConnection(_databaseConn))
            {
                conn.Open();
                string sql = @"
                    SELECT Id
                    FROM Vendors
                    WHERE Name = @NAME";
                SqliteCommand cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.Add("@NAME", SqliteType.Text).Value = vendor.Name;
                id = Convert.ToInt32(cmd.ExecuteScalar());
                return id != 0;
            }
        }
    }
}
