namespace SmartX.Api.Domain;

// One node in the physical deployment tree.
// Structure: Facility > Zone > Sub-Zone > Device.
// The recursive validator walks this tree depth-first.
public sealed class DeploymentNode
{
    public string             Name         { get; init; }
    public string             NodeId       { get; init; }
    public bool               IsConfigured { get; set;  }
    public List<DeploymentNode> Children   { get; init; }

    public DeploymentNode(string name, string nodeId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Node name cannot be empty.", nameof(name));

        Name         = name;
        NodeId       = nodeId;
        IsConfigured = false;
        Children     = new List<DeploymentNode>();
    }

    public void AddChild(DeploymentNode child)
    {
        if (child is null) return;
        Children.Add(child);
    }
}
