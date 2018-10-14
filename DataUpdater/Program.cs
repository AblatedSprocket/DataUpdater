using DataUpdater.Finance.Repositories;
using Microsoft.Extensions.Configuration;

namespace DataUpdater
{
    class Program
    {
        private static IConfiguration Config { get; set; }
        private static ITransactionRepository TransactionRepository { get; set; }
        public static IVendorRepository VendorRepository { get; set; }
        static void Main(string[] args)
        {
            Init();
            DataUpdater updater = new DataUpdater(Config, TransactionRepository);
            updater.Update();
        }
        static void Init()
        {
            Config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            TransactionRepository = new TransactionRepository(Config);
            VendorRepository = new VendorRepository(Config);
        }
    }
}
