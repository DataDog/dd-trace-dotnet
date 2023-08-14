using System;

namespace Billing;

public class PaymentProcessor
{
    public static bool BillCustomer(Customer customer, string couponName, double billAmount)
    {
        Console.WriteLine($"Bill for {customer.Name} (Coupon: {couponName}): ${billAmount}");
        return billAmount > 0;
    }
}
