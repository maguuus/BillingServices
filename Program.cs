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
        TenureMonths = tenureMonths >= 0 ? tenureMonths : throw new ArgumentException("Tenure months must be non-negative", nameof(tenureMonths));
        Devices = devices >= 0 ?  devices : throw new ArgumentException("Devices must be non-negative", nameof(devices));
        BasePrice = basePrice >= 0 ?  basePrice : throw new ArgumentException("Price must be non-negative", nameof(basePrice));
    }
}

public class BillingService
{
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
            SubscriptionStatus.Pro => s switch
            {
                { IsLoyal: true } => s.BasePrice * 0.85,
                { IsLongTerm: true } => s.BasePrice * 0.9,
                _ => s.BasePrice
            },
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
    
    public (bool Ok, string Error) Validate(Subscriber s) =>
        s switch
        {
            null => (false, "Subscriber is null"),
            { Id: null or "" } => (false, "Id missing"),
            { Region: null or "" } => (false, "Region missing"),
            { BasePrice: < 0 } => (false, "Base price must be non-negative"),
            { TenureMonths: < 0 } => (false, "Tenure months must be non-negative"),
            { Devices: < 0 } => (false, "Devices must be non-negative"),
            _ => (true, "")
        };
}

public class Program
{
    public static void Main()
    {
        var billing = new BillingService();
        
        var subscribers = new List<Subscriber>
        {
            new Subscriber("A-1", "US", SubscriptionStatus.Trial, 0, 1, 9.99),
            new Subscriber("B-2", "US", SubscriptionStatus.Pro, 18, 4, 14.99),
            new Subscriber("C-3", "EU", SubscriptionStatus.Student, 6, 2, 12.99),
            new Subscriber("D-4", "CA", SubscriptionStatus.Basic, 3, 1, 8.99)
        };

        foreach (var subscriber in subscribers)
        {
            var (isValid, error) = billing.Validate(subscriber);
            
            if (isValid)
            {
                var total = billing.CalcTotal(subscriber);
                Console.WriteLine($"Subscriber {subscriber.Id}: ${total:F2} " +
                                  $"(Status: {subscriber.Status}, " +
                                  $"Tenure: {subscriber.TenureMonths} months, " +
                                  $"Devices: {subscriber.Devices})");
            }
            else
            {
                Console.WriteLine($"Error for {subscriber.Id}: {error}");
            }
        }
    }
}