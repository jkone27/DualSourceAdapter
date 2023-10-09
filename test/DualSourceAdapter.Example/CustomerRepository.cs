namespace DualSourceAdapter.Example.Repository;

using DualSourceAdapter.Example.Contracts;

public interface ICustomerRepository 
{
    Task<Customer> SaveCutomerAsync(CreateCustomerRequest request);
    Task<Customer?> GetCustomerByIdAsync(GetCustomerById request);
}

public class CustomerRepository : ICustomerRepository
{
    private readonly Dictionary<Guid, Customer> DataStore = 
        new Dictionary<Guid, Customer>();

    private static readonly Random Rnd = new Random();

    // simulate network
    private readonly TimeSpan RndSecs = TimeSpan.FromMilliseconds(Rnd.Next(500, 3000));

    public async Task<Customer> SaveCutomerAsync(CreateCustomerRequest request)
    {
        await Task.Delay(RndSecs);

        var id = Guid.NewGuid();

        var customer = new Customer 
        {
            Id = id,
            Age = request.Age,
            Name = request.Name,
            Surname = request.Surname
        };

        DataStore.Add(id,customer);

        return customer;
    }

    public async Task<Customer?> GetCustomerByIdAsync(GetCustomerById request)
    {
        await Task.Delay(RndSecs);

        return DataStore.TryGetValue(request.Id, out var value) ?
            value : null;
    }
}