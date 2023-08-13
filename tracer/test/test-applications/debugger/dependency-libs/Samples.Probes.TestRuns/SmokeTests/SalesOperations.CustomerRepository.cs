using System.Collections.Generic;

namespace Billing;

public partial class SalesOperations
{
    public static class CustomerRepository
    {
        public static List<SalesOperations.Customer> GetCustomersToProcess()
        {
            Coupon goldenDeal = new Coupon { FancyName = "Golden Deal", DiscountRatio = 0.1 };
            Coupon silverSteal = new Coupon { FancyName = "Silver Steal", DiscountRatio = 0.2 };
            Coupon mysticZero = new Coupon { FancyName = "Mystic Zero", DiscountRatio = 1 };
            Coupon bronzeBash = new Coupon { FancyName = "Bronze Bash", DiscountRatio = 0.05 };

            return new List<Customer>
            {
                new Customer { Id = 1, Name = "John", BranchName = "Central Plaza", Coupon = goldenDeal },
                new Customer { Id = 2, Name = "Doe", BranchName = "North Point", Coupon = silverSteal },
                new Customer { Id = 3, Name = "Jane", BranchName = "South Gate", Coupon = mysticZero },
                new Customer { Id = 4, Name = "Smith", BranchName = "West Avenue", Coupon = bronzeBash },
                new Customer { Id = 5, Name = "Marry", BranchName = "East Station", Coupon = goldenDeal },
                new Customer { Id = 6, Name = "Peter", BranchName = "Central Plaza", Coupon = silverSteal },
                new Customer { Id = 7, Name = "Lucy", BranchName = "North Point", Coupon = mysticZero },
                new Customer { Id = 8, Name = "Paul", BranchName = "West Avenue", Coupon = bronzeBash }
            };
        }
    }
}
