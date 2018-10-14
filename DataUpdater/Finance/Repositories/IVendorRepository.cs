using Finance.Contracts;
using System.Collections.Generic;

namespace DataUpdater.Finance.Repositories
{
    public interface IVendorRepository
    {
        Dictionary<int, string> AddBulkVendors(IEnumerable<Vendor> vendors);
        int? AddVendor(Vendor vendor);
        bool DeleteVendor(int vendorId);
        Vendor GetVendor(int tvendorId);
        IEnumerable<Vendor> GetVendors();
        Vendor UpdateVendor(Vendor vendor);
        bool VendorExists(int id);
        bool VendorExists(Vendor vendor, out int id);
    }
}
