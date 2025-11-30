namespace BillingServices;

public enum SubscriptionStatus { Trial, Basic, Pro, Student }

public class Subscriber
{

    public string Id { get; private set; }
    public string Region { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public int TenureMonths { get; private set; }
    public int Devices { get; private set; }
    public double BasePrice { get; private set; }
    
    public bool HasManyDevices => Devices > 3;
    public bool IsLongTerm => TenureMonths >= 12;
    public bool IsLoyal => TenureMonths >= 24;
    
    public Subscriber(string id, string region, SubscriptionStatus status, int tenureMonths, int devices, double basePrice)
    {
        Id = !string.IsNullOrWhiteSpace(id) 
            ? id 
            : throw new ArgumentException("Id cannot be null or empty", nameof(id));
        Region = !string.IsNullOrWhiteSpace(region) 
            ? region.Trim().ToUpperInvariant() 
            : throw new ArgumentException("Region cannot be null or empty", nameof(region));
        Status = status;
        TenureMonths = tenureMonths >= 0 ? tenureMonths : throw new ArgumentOutOfRangeException(nameof(tenureMonths), "Tenure months must be non-negative");
        Devices = devices >= 0 ?  devices : throw new ArgumentOutOfRangeException(nameof(devices), "Devices must be non-negative");
        BasePrice = basePrice >= 0 ?  basePrice : throw new ArgumentOutOfRangeException(nameof(basePrice), "Price must be non-negative");
    }
}

public class BillingService
{
    private static readonly HashSet<string> ValidRegions = ["EU", "US", "CA", "UK", "AU", "FR"];
    private static readonly HashSet<string> TaxableRegions = ["EU", "US"];

    public double CalcTotal(Subscriber s)
    {
        s = s ?? throw new ArgumentNullException(nameof(s));

        double PriceAfterStatus() => ApplyStatusDiscount(s);
        double WithDevices(double price) => price + ApplyDevicesSurcharge(s);
        double WithTax(double price) => price + ApplyRegionalTax(s, price);

        return WithTax(
            WithDevices(
                PriceAfterStatus()));
    }

    private static double ApplyStatusDiscount(Subscriber s) =>
        s.Status switch
        {
            SubscriptionStatus.Trial => 0,
            SubscriptionStatus.Student => s.BasePrice * 0.5,
            SubscriptionStatus.Pro when s.IsLoyal => s.BasePrice * 0.85,
            SubscriptionStatus.Pro when s.IsLongTerm => s.BasePrice * 0.9,
            _ => s.BasePrice
        };

    private static double ApplyDevicesSurcharge(Subscriber s) =>
        s.HasManyDevices ? 4.99 : 0;

    private static double ApplyRegionalTax(Subscriber s, double price) =>
        s.Region switch
        {
            "EU" => price * 0.21,
            "US" => price * 0.07,
            _ => 0
        };

    public (bool Ok, string Error) Validate(Subscriber? s)
    {
        if (s == null) return (false, "Subscriber is null");

        return ValidateAll(s,
            CheckRegion,
            CheckCommonRules,
            CheckStatusSpecificRules
        );
    }

    private static (bool, string) ValidateAll(Subscriber s, params Func<Subscriber, (bool, string)>[] validators)
    {
        foreach (var validator in validators)
        {
            var result = validator(s);
            if (!result.Item1) return result;
        }

        return (true, "");
    }

    private static (bool, string) CheckRegion(Subscriber s)
    {
        if (!ValidRegions.Contains(s.Region))
            return (false, $"Region '{s.Region}' is not supported");
        return (true, "");
    }

    private static (bool, string) CheckCommonRules(Subscriber s)
    {
        if (s.Devices > 10)
            return (false, "Maximum 10 devices allowed per subscription");

        if (s.Devices == 0)
            return (false, "At least one device is required");

        if (s.Status != SubscriptionStatus.Trial && s.BasePrice <= 0)
            return (false, "Non-trial subscriptions must have positive base price");

        if (TaxableRegions.Contains(s.Region) && s.BasePrice > 1000)
            return (false, "High-value subscriptions in taxable regions require special approval");

        return (true, "");
    }

    private static (bool, string) CheckStatusSpecificRules(Subscriber s)
    {
        return s.Status switch
        {
            SubscriptionStatus.Trial when s.TenureMonths > 1
                => (false, "Trial subscription cannot exceed 1 month"),
            SubscriptionStatus.Trial when s.BasePrice > 0
                => (false, "Trial subscription must have zero base price"),
            SubscriptionStatus.Student when s.TenureMonths > 48
                => (false, "Student subscription cannot exceed 48 months"),
            SubscriptionStatus.Pro when s.TenureMonths < 3
                => (false, "Pro subscription requires minimum 3 months tenure"),
            _ => (true, "")
        };
    }
}

public static class Program
{
    public static void Main()
    {
        var billing = new BillingService();
        
        var subscribers = new List<Subscriber>
        {
            new("A-1", "US", SubscriptionStatus.Trial, 0, 1, 9.99),
            new("B-2", "US", SubscriptionStatus.Pro, 18, 4, 14.99),
            new("C-3", "EU", SubscriptionStatus.Student, 6, 2, 12.99),
            new("D-4", "CA", SubscriptionStatus.Basic, 3, 1, 8.99),
            new("E-5", "XX", SubscriptionStatus.Basic, 1, 1, 5.99),
            new("F-6", "US", SubscriptionStatus.Trial, 2, 1, 0),
            new("G-7", "US", SubscriptionStatus.Pro, 1, 15, 9.99)  
        };

        foreach (var subscriber in subscribers)
        {
            var (isValid, error) = billing.Validate(subscriber);
            
            if (isValid)
            {
                var total = billing.CalcTotal(subscriber);
                Console.WriteLine($"Price for {subscriber.Id}: ${total:F2} " +
                                  $"({subscriber.Status}, " +
                                  $"{subscriber.TenureMonths} months, " +
                                  $"{subscriber.Devices} devices)");
            }
            else
            {
                Console.WriteLine($"Error for {subscriber.Id}: {error}");
            }
        }
    }
}