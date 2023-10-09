namespace DualSourceAdapter.Example.Contracts;

public class GetCustomerById
{
    public Guid Id { get; set; }
}

public class ChangeCustomerName 
{
    public Guid Id { get; set; }

    public string Name { get; set;}
}

public class CreateCustomerRequest {
    public string Name {get;set;}
    public string Surname {get;set;}
    public int Age {get;set;}
}