using DataUpdater.Finance.Repositories;
using Finance.Contracts;
using FinanceManagement.StatementProcessing;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace DataUpdater
{
    public class DataUpdater
    {
        private IConfiguration Config { get; }
        private ITransactionRepository TransactionRepository { get; }
        private StatementProcessor Processor { get; }
        public DataUpdater(IConfiguration config, ITransactionRepository transactionRepository)
        {
            Config = config;
            TransactionRepository = transactionRepository;
            Processor = new StatementProcessor(Config["FileDirectory"]);

        }
        public void Update()
        {
            IEnumerable<Transaction> transactions = Processor.ProcessStatements();
        }
    }
}
