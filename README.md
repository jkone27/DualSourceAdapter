# DualSourceAdapter
adapter for a dual source to compare and migrate 

## Configuration

Here an example configuration with 3 implementations of `ICustomerRepository`,
a `NewService`, a `LegacyService` and an `AdapterService`, the usage of the library is internal to the latter and will be shown later.

`MigrationOption` is a configuration provided to configure the enabled `MigrationSource []` and the `Active` (master) configuration, from which data is stored and read first.

```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services => { 
        services.AddSingleton<LegacyService>();
        services.AddSingleton<NewService>();
        services.AddSingleton<ICustomerRepository, AdapterService>();

        var migrationOption = new MigrationOption {
            Active = MigrationSource.New,
            IsCompare = true,
            Sources = new [] {
                MigrationSource.New,
                MigrationSource.Legacy
            }
        };

        services.AddSingleton(migrationOption);
    })
    .Build();

```

## Creating an AdapterService

The creation of the adapter service goes as follows,
in this example only 2 flows need to be adapted differently, `read` and `write`,
but eventually more could be given (e.g. `read`, `write`, `auth`)

```csharp
this.readMigrator = new MigrationTaskBuilder<GetCustomerById, Customer?>(
            legacyService.GetCustomerByIdAsync,
            newService.GetCustomerByIdAsync,
            ReadCompare, // to compare data
            ReadResponseAdapter, // usually the identifier need to be adapter to match the old data
            ReadRequestAdapter // usually also new requests need to use a Map to retrieve the corresponding identifier for new data, based on old identifier
        );
```

conversely the write migrator is configured in a similar fashion

```csharp
this.writeMigrator = new MigrationTaskBuilder<CreateCustomerRequest, Customer>(
            legacyService.SaveCutomerAsync,
            newService.SaveCutomerAsync,
            StoreCustomerReference,
            WriteResponseAdapter,
            WriteRequestAdapter
        );
```

### Adapter Examples (may change)

```csharp
public Task ReadCompare(Customer? customer, Customer? customerNew)
    {
        logger.LogInformation("COMPARING CUSTOMERS...");
        return Task.CompletedTask;
    }

    public async Task StoreCustomerReference(Customer oldCustomer, Customer customerNew)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(400));
        CustomerRefStorage.Add(oldCustomer.Id, customerNew.Id);
    }

    public Customer? ReadResponseAdapter(Customer? legacyCustomer, Customer? newCustomer)
    {
        if(legacyCustomer is not null && newCustomer is not null)
        {
            newCustomer.Id = legacyCustomer.Id;
            return newCustomer;
        }

        return newCustomer;
    }

    public Customer WriteResponseAdapter(Customer legacyCustomer, Customer newCustomer)
    {
        newCustomer.Id = legacyCustomer.Id;
        return newCustomer;
    }

    public GetCustomerById ReadRequestAdapter(GetCustomerById request, Customer?    
        legacyCustomer) 
    {
        // here the reference identifier must be adapted to match the original source
        if(legacyCustomer is not null)
        {
            request.Id = legacyCustomer.Id;
        }

        return request;
    }

    public CreateCustomerRequest WriteRequestAdapter(CreateCustomerRequest request, Customer? customer) 
    {
        return request; // NOP, in our write request there is no usage of identities, so no need to adapt
    }
```