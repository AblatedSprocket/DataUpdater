using Finance.Contracts;
using System;
using System.Collections.Generic;

namespace DataUpdater.Finance.Repositories
{
    public interface ITransactionRepository
    {
        Dictionary<int, string> AddBulkTransactions(IEnumerable<Transaction> transactions);
        int? AddTransaction(Transaction transaction);
        bool DeleteTransaction(int transactionId);
        Transaction GetTransaction(int transactionId);
        IEnumerable<CostBreakdown> GetMonthCostBreakdowns();
        IEnumerable<CostBreakdown> GetYearCostBreakdowns();
        CostBreakdown GetLifetimeCostBreakdown();
        IEnumerable<Transaction> GetTransactions();
        IEnumerable<Transaction> GetTransactionsByMonth(DateTime month);
        IEnumerable<Transaction> GetTransactionsByYear(int year);
        bool TransactionExists(int id);
        bool TransactionExists(Transaction transaction);
        Transaction UpdateTransaction(Transaction transaction);
        void UpdateTransactionVendors(Vendor vendor);
        void UpdateTransactionVendors(IEnumerable<Vendor> vendors);
    }
}
