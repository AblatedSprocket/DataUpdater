using Finance.Contracts;
using System;

namespace DataUpdater.Utilities
{
    public static class Extensions
    {
        
        public static bool IsNumeric(this string value)
        {
            if (int.TryParse(value, out int outInt))
            {
                return true;
            }
            if (double.TryParse(value, out double outDouble))
            {
                return true;
            }
            return false;
        }
        public static string ToSqliteStorage(this DateTime date)
        {
            return date.ToString("yyyy-MM-dd HH:mm:ss");
        }
        public static object ToSqliteStorage(this DateTime? date)
        {
            if (date.HasValue)
            {
                return date.Value.ToString("yyyy-MM-dd HH:mm:ss");
            }
            else
            {
                return DBNull.Value;
            }
        }
        public static int ToSqliteStorage(this decimal value)
        {
            return (int)(value * 100);
        }
        public static decimal ConvertSqliteStorageToDecimal(this object value)
        {
            int number = int.Parse(value.ToString());
            return (decimal)number / 100;
        }
        public static DateTime? ConvertSqliteStorageToNullableDateTime(this object value)
        {
            if (DateTime.TryParse(value.ToString(), out DateTime returnValue))
            {
                return returnValue;
            }
            return null;
        }
        public static void Validate(this ModelStateDictionary modelState, Transaction transaction)
        {
            if (transaction.Amount == 0)
            {
                modelState.TryAddModelError("Amount", "Must be greater than 0.");
            }
            if (transaction.TransactionDate == DateTime.MinValue)
            {
                modelState.TryAddModelError("TransactionDate", "Must not be minimum DateTime value. Set to null if not provided.");
            }
            if (transaction.PostDate == DateTime.MinValue)
            {
                modelState.TryAddModelError("PostDate", "Required.");
            }
            if (string.IsNullOrEmpty(transaction.Description))
            {
                modelState.TryAddModelError("Description", "Required.");
            }
            if (string.IsNullOrEmpty(transaction.Account))
            {
                modelState.TryAddModelError("Account", "Required.");
            }
            if (int.TryParse(transaction.Account, out int accountResult))
            {
                if (accountResult == 0)
                {
                    modelState.TryAddModelError("Account", "Must not be only zeroes.");
                }
            }
            if (int.TryParse(transaction.SerialNumber, out int serialNumberResult))
            {
                if (serialNumberResult == 0)
                {
                    modelState.TryAddModelError("SerialNumber", "Must not be only zeroes.");
                }
            }
        }
        public static void Validate(this ModelStateDictionary modelState, Vendor vendor)
        {
            if (string.IsNullOrEmpty(vendor.Name))
            {
                modelState.TryAddModelError("Name", "Required.");
            }
            if (string.IsNullOrEmpty(vendor.TransactionKey))
            {
                modelState.TryAddModelError("TransactionKey", "Required.");
            }
        }
    }
}
