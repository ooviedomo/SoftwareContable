using AccountingLab.Domain.Education;

namespace AccountingLab.Domain.Challenges.DP05Singleton;

public sealed record GlobalAccountingRegistry(string Version, int MonetaryScale, string Scope);
public interface IGlobalRegistryChallenge { GlobalAccountingRegistry Acquire(); }
public sealed class GlobalRegistryChallenge : IGlobalRegistryChallenge
{
    public GlobalAccountingRegistry Acquire()
    {
        // solucionado
        return AccountingRegistrySingleton.Instance;
    }

    private sealed class AccountingRegistrySingleton
    {
        private static readonly Lazy<GlobalAccountingRegistry> LazyInstance =
            new(static () => new GlobalAccountingRegistry("DEMO-1", 4, "process"), LazyThreadSafetyMode.ExecutionAndPublication);

        public static GlobalAccountingRegistry Instance => LazyInstance.Value;
    }
}

