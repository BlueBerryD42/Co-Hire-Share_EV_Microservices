using Microsoft.Extensions.Configuration;

namespace CoOwnershipVehicle.Shared.Configuration;

public class EnvFileConfigurationSource : IConfigurationSource
{
    private readonly string _filePath;

    public EnvFileConfigurationSource(string filePath)
    {
        _filePath = filePath;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new EnvFileConfigurationProvider(_filePath);
    }
}