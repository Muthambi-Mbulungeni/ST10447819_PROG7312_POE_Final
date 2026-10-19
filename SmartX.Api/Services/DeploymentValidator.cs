namespace SmartX.Api.Services;

using SmartX.Api.Domain;

// Validates a DeploymentNode tree by walking it recursively depth-first.
// A node that is not configured terminates its branch. We record the full
// path to every failing node so the caller knows exactly where the gap is.
public sealed class DeploymentValidator
{
    public ValidationResult Validate(DeploymentNode root)
    {
        if (root is null)
            return new ValidationResult(false, new List<string> { "Tree root is null." });

        var failures = new List<string>();
        Walk(root, root.Name, failures);

        return new ValidationResult(failures.Count == 0, failures);
    }

    // Path is built as we descend so the caller gets the full chain
    // for example Facility-A > Zone-1 > Sub-Zone-B on failure.
    // RECURSION REQUIREMENT: This method calls itself to traverse the tree.
    private static void Walk(DeploymentNode node, string path, List<string> failures)
    {
        if (!node.IsConfigured)
        {
            failures.Add(path);
            return;
        }

        foreach (var child in node.Children)
            Walk(child, $"{path} > {child.Name}", failures);
    }
}

public sealed record ValidationResult(bool IsValid, List<string> FailedPaths);
