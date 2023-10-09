using DualSourceAdapter.Example.Contracts;
using DualSourceAdapter.Example.Repository;
using Microsoft.Extensions.Logging;

namespace DualSourceAdapter.Example.Legacy;

public class LegacyService: ICustomerRepository
{
    private readonly CustomerRepository Rep = new CustomerRepository();
    private readonly ILogger<LegacyService> logger;

    public LegacyService(ILogger<LegacyService> logger)
    {
        this.logger = logger;
    }

    public Task<Customer?> GetCustomerByIdAsync(GetCustomerById request)
    {
        logger.LogInformation($"LEGACY Get: {request.Id}");
        return Rep.GetCustomerByIdAsync(request);
    }

    public async Task<Customer> SaveCutomerAsync(CreateCustomerRequest request)
    {
        var r = await Rep.SaveCutomerAsync(request);
        logger.LogInformation($"LEGACY Save: {r.Id}");
        return r;
    }
}