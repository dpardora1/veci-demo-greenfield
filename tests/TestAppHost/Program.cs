using greenfield_checkout.Shared;

namespace greenfield_checkout.TestAppHost;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = DistributedApplication.CreateBuilder(args);

        builder.AddPostgres(Services.DatabaseServer)
            .AddDatabase(Services.Database);

        builder.Build().Run();
    }
}