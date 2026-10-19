using Core;
using Core.Abstractions;
using Core.Storage;

namespace Cli.Composition;

// Альтернативна частина composition root: конкретні класи залишаються в Cli.
public static class StoreFactory
{
    public static ICatalogStore Create(string[] args)
    {
        StoreOptions options = StoreOptions.Parse(args);
        ICatalogStore store = options.UseFile
            ? new FileCatalogStore(options.DataPath)
            : new InMemoryCatalogStore(SampleData.Goods());
        return options.UseCache ? new CachingCatalogStore(store) : store;
    }
}
