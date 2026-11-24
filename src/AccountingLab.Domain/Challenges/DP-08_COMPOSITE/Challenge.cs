using AccountingLab.Domain.Education;

namespace AccountingLab.Domain.Challenges.DP08Composite;

public sealed record AccountHierarchy(string Code, bool AllowsPosting, decimal OwnBalance, IReadOnlyList<AccountHierarchy> Children);
public interface IAccountTreeChallenge { decimal Total(AccountHierarchy account); }
public sealed class AccountTreeChallenge : IAccountTreeChallenge
{
    public decimal Total(AccountHierarchy account)
    {
        // Solucionado
        ArgumentNullException.ThrowIfNull(account);

        IAccountComponent root = BuildComponent(account);
        return root.CalculateTotal();
    }

    private static IAccountComponent BuildComponent(AccountHierarchy node)
    {
        ArgumentNullException.ThrowIfNull(node);

        bool hasChildren = node.Children is { Count: > 0 };

        if (node.AllowsPosting && hasChildren)
            throw new ArgumentException($"La cuenta '{node.Code}' permite movimiento y no puede tener descendientes.", nameof(node));

        if (!hasChildren)
            return new PostingAccountLeaf(node.OwnBalance);

        CompositeAccountNode composite = new(node.OwnBalance);

        foreach (AccountHierarchy child in node.Children!)
        {
            if (child is not null)
                composite.Add(BuildComponent(child));
        }

        return composite;
    }

    // Component
    private interface IAccountComponent
    {
        decimal CalculateTotal();
    }

    // Leaf
    private sealed class PostingAccountLeaf(decimal ownBalance) : IAccountComponent
    {
        public decimal CalculateTotal() => ownBalance;
    }

    // Composite
    private sealed class CompositeAccountNode(decimal ownBalance) : IAccountComponent
    {
        private readonly List<IAccountComponent> _children = [];

        public void Add(IAccountComponent component) => _children.Add(component);

        public decimal CalculateTotal()
        {
            decimal total = ownBalance;

            foreach (IAccountComponent child in _children)
                total += child.CalculateTotal();

            return total;
        }
    }
}