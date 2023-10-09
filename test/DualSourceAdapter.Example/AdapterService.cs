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

    private readonly Dictionary<Guid,Guid> CustomerRefStorage = new();

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

    public Task ReadCompare(PrimaryResponse<Customer?> customer, SecondaryResponse<Customer?> customerNew)
    {
        logger.LogInformation("COMPARING CUSTOMERS...");
        return Task.CompletedTask;
    }

    public async Task StoreCustomerReference(PrimaryResponse<Customer> primary, SecondaryResponse<Customer> secondary)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(400));

        var p = GetPrimary(primary);
        var s = GetSecondary(secondary);
    
        CustomerRefStorage.Add(p.Id, s.Id);
    }

    public PrimaryResponse<Customer?> ReadResponseAdapter(PrimaryResponse<Customer?> primary, SecondaryResponse<Customer?> secondary)
    {
        var p = GetPrimary(primary);
        var s = GetSecondary(secondary);

        if(s is not null && p is not null)
        {
            p.Id = s.Id;
            return Primary<Customer?>(p);
        }

        return primary;
    }

    public PrimaryResponse<Customer> WriteResponseAdapter(PrimaryResponse<Customer> primary, SecondaryResponse<Customer> secondary)
    {
        // keep the reference consistent for clients
        secondary.Item.Id = primary.Item.Id;
        return Primary(secondary.Item);
    }

    public GetCustomerById ReadRequestAdapter(GetCustomerById request, PrimaryResponse<Customer?> primary) 
    {
        Customer? p = primary.Item;
        
        if(p is not null)
        {
            // grab the reference for the identifier in the new storage, based on old
            request.Id = CustomerRefStorage[p.Id];
        }

        return request;
    }

    public CreateCustomerRequest WriteRequestAdapter(CreateCustomerRequest request, PrimaryResponse<Customer> _) 
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