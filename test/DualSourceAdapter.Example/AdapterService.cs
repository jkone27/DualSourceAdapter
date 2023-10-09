using DualSourceAdapter.Example.Contracts;
using DualSourceAdapter.Example.Legacy;
using DualSourceAdapter.Example.New;
using DualSourceAdapter.Example.Repository;
using Microsoft.Extensions.Logging;
using static DualSourceAdapter.Adapter;

namespace DualSourceAdapter.Example;

public class AdapterService : ICustomerRepository
{
    private readonly LegacyService legacyService;
    private readonly NewService newService;
    private readonly MigrationOption migrationOption;
    private readonly ILogger<AdapterService> logger;
    private readonly MigrationTaskBuilder<GetCustomerById, Customer?> readMigrator;
    private readonly MigrationTaskBuilder<CreateCustomerRequest, Customer> writeMigrator;

    private readonly Dictionary<Guid,Guid> CustomerRefStorage = new Dictionary<Guid, Guid>();

    public AdapterService(
        LegacyService legacyService, 
        NewService newService,
        MigrationOption migrationOption,
        ILogger<AdapterService> logger)
    {
        this.legacyService = legacyService;
        this.newService = newService;
        this.migrationOption = migrationOption;
        this.logger = logger;
        this.readMigrator = new MigrationTaskBuilder<GetCustomerById, Customer?>(
            legacyService.GetCustomerByIdAsync,
            newService.GetCustomerByIdAsync,
            ReadCompare,
            ReadResponseAdapter,
            ReadRequestAdapter
        );

        this.writeMigrator = new MigrationTaskBuilder<CreateCustomerRequest, Customer>(
            legacyService.SaveCutomerAsync,
            newService.SaveCutomerAsync,
            StoreCustomerReference,
            WriteResponseAdapter,
            WriteRequestAdapter
        );
    }

    public Task ReadCompare(Customer? customer, Customer? customerNew)
    {
        logger.LogInformation("COMPARING CUSTOMERS...");
        return Task.CompletedTask;
    }

    public async Task StoreCustomerReference(Customer secondary, Customer primary)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(400));
        CustomerRefStorage.Add(secondary.Id, primary.Id);
    }

    public Customer? ReadResponseAdapter(Customer? secondary, Customer? primary)
    {
        if(secondary is not null && primary is not null)
        {
            primary.Id = secondary.Id;
            return primary;
        }

        return primary;
    }

    public Customer WriteResponseAdapter(Customer secondary, Customer primary)
    {
        // keep the reference consistent for clients
        primary.Id = secondary.Id;
        return primary;
    }

    public GetCustomerById ReadRequestAdapter(GetCustomerById request, Customer? secondary) 
    {
        if(secondary is not null)
        {
            // grab the reference for the identifier in the new storage, based on old
            request.Id = CustomerRefStorage[secondary.Id];
        }

        return request;
    }

    public CreateCustomerRequest WriteRequestAdapter(CreateCustomerRequest request, Customer? customer) 
    {
        return request; // NOP
    }

    public Task<Customer?> GetCustomerByIdAsync(GetCustomerById request)
    {
        return readMigrator.MigrationTaskAsync(migrationOption, request);
    }

    public Task<Customer> SaveCutomerAsync(CreateCustomerRequest request)
    {
        return writeMigrator.MigrationTaskAsync(migrationOption, request);
    }
}