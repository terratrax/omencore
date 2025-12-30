using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = false)]

namespace OmenCoreApp.Tests
{
    [CollectionDefinition("Config Isolation", DisableParallelization = true)]
    public class ConfigIsolationCollectionDefinition { }
}
