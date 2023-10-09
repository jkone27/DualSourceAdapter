namespace DualSourceAdapter.Example.New;

using DualSourceAdapter.Example.Contracts;
using DualSourceAdapter.Example.Repository;
using Microsoft.Extensions.Logging;

public class NewService: ICustomerRepository
{
    private readonly CustomerRepository Rep = new CustomerRepository();
    private readonly ILogger<NewService> logger;

    public NewService(ILogger<NewService> logger)
    {
        this.logger = logger;
    }

    public Task<Customer?> GetCustomerByIdAsync(GetCustomerById request)
    {
        logger.LogInformation($"NEW Get: {request.Id}");
        return Rep.GetCustomerByIdAsync(request);
    }

    public async Task<Customer> SaveCutomerAsync(CreateCustomerRequest request)
    {
        var r = await Rep.SaveCutomerAsync(request);
        logger.LogInformation($"NEW Save: {r.Id}");
        return r;
    }
}