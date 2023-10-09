using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DualSourceAdapter.Example.Legacy;
using DualSourceAdapter.Example.New;
using static DualSourceAdapter.Adapter;
using DualSourceAdapter.Example.Repository;
using DualSourceAdapter.Example;
using DualSourceAdapter.Example.Contracts;

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

var repo = host.Services.GetRequiredService<ICustomerRepository>();

var customerReq = new CreateCustomerRequest {
     Age = 41,
     Name = "Gianni",
     Surname = "Pinotto"
};

var c = await repo.SaveCutomerAsync(customerReq);

var cresult = await repo.GetCustomerByIdAsync(new GetCustomerById {
    Id = c.Id
});

Console.WriteLine("END");
