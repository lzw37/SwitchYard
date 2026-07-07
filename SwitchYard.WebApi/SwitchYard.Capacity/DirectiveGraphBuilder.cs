using System;
using System.Linq;

namespace SwitchYard.Capacity
{
    public class DirectiveGraphLink
    {
        public int ID { get; set; }

        public string? Name { get; set; }

        public int FromNodeID { get; set; }

        public int ToNodeID { get; set; }
    }

    public class DirectiveGraphNode
    {
        public int ID { get; set; }

        public double X { get; set; }

        public double Y { get; set; }
    }

    public class DirectiveGraph
    {
        public DirectiveGraphDirections Direction { get; set; }

        public List<DirectiveGraphNode> Nodes { get; set; } = new();

        public List<DirectiveGraphLink> Links { get; set; } = new();
    }

    public enum DirectiveGraphDirections
    {
        LeftToRight,
        RightToLeft
    }

    public class DirectiveGraphBuilder
    {
        private const double CoordinateTolerance = 1e-9;

        public List<DirectiveGraph> BuildDirectiveGraphs(List<StationNodeRow> stationNodes, List<StationLinkRow> stationLinks)
        {
            ArgumentNullException.ThrowIfNull(stationNodes);
            ArgumentNullException.ThrowIfNull(stationLinks);

            return new List<DirectiveGraph>
            {
                BuildLeftToRightDirectiveGraph(stationNodes, stationLinks),
                BuildRightToLeftDirectiveGraph(stationNodes, stationLinks)
            };
        }

        private DirectiveGraph BuildLeftToRightDirectiveGraph(List<StationNodeRow> stationNodes, List<StationLinkRow> stationLinks)
        {
            return BuildDirectiveGraph(stationNodes, stationLinks, DirectiveGraphDirections.LeftToRight);
        }

        private DirectiveGraph BuildRightToLeftDirectiveGraph(List<StationNodeRow> stationNodes, List<StationLinkRow> stationLinks)
        {
            return BuildDirectiveGraph(stationNodes, stationLinks, DirectiveGraphDirections.RightToLeft);
        }

        private static DirectiveGraph BuildDirectiveGraph(
            IReadOnlyCollection<StationNodeRow> stationNodes,
            IReadOnlyCollection<StationLinkRow> stationLinks,
            DirectiveGraphDirections direction)
        {
            var stationNodeByID = BuildStationNodeLookup(stationNodes);
            EnsureUniqueStationLinkIDs(stationLinks);

            var graph = new DirectiveGraph
            {
                Direction = direction,
                Nodes = stationNodes
                    .Select(node => new DirectiveGraphNode
                    {
                        ID = node.ID,
                        X = node.X,
                        Y = node.Y
                    })
                    .ToList(),
                Links = stationLinks
                    .Select(link => BuildDirectiveGraphLink(link, stationNodeByID, direction))
                    .ToList()
            };

            EnsureAcyclic(graph);
            return graph;
        }

        private static Dictionary<int, StationNodeRow> BuildStationNodeLookup(IReadOnlyCollection<StationNodeRow> stationNodes)
        {
            var nodeByID = new Dictionary<int, StationNodeRow>();

            foreach (var node in stationNodes)
            {
                if (!double.IsFinite(node.X) || !double.IsFinite(node.Y))
                {
                    throw new ArgumentException($"StationNode {node.ID} has invalid coordinates.", nameof(stationNodes));
                }

                if (!nodeByID.TryAdd(node.ID, node))
                {
                    throw new ArgumentException($"Duplicate StationNode ID {node.ID}.", nameof(stationNodes));
                }
            }

            return nodeByID;
        }

        private static void EnsureUniqueStationLinkIDs(IReadOnlyCollection<StationLinkRow> stationLinks)
        {
            var linkIDs = new HashSet<int>();

            foreach (var link in stationLinks)
            {
                if (!linkIDs.Add(link.ID))
                {
                    throw new ArgumentException($"Duplicate StationLink ID {link.ID}.", nameof(stationLinks));
                }
            }
        }

        private static DirectiveGraphLink BuildDirectiveGraphLink(
            StationLinkRow stationLink,
            IReadOnlyDictionary<int, StationNodeRow> stationNodeByID,
            DirectiveGraphDirections direction)
        {
            if (!stationNodeByID.TryGetValue(stationLink.FromNodeID, out var fromNode))
            {
                throw new ArgumentException(
                    $"StationLink {stationLink.ID} references missing FromNodeID {stationLink.FromNodeID}.",
                    nameof(stationLink));
            }

            if (!stationNodeByID.TryGetValue(stationLink.ToNodeID, out var toNode))
            {
                throw new ArgumentException(
                    $"StationLink {stationLink.ID} references missing ToNodeID {stationLink.ToNodeID}.",
                    nameof(stationLink));
            }

            var (leftNodeID, rightNodeID) = GetDirectionalEndpointIDs(stationLink, fromNode, toNode);
            var fromNodeID = direction == DirectiveGraphDirections.LeftToRight ? leftNodeID : rightNodeID;
            var toNodeID = direction == DirectiveGraphDirections.LeftToRight ? rightNodeID : leftNodeID;

            return new DirectiveGraphLink
            {
                ID = stationLink.ID,
                Name = stationLink.Name,
                FromNodeID = fromNodeID,
                ToNodeID = toNodeID
            };
        }

        private static (int LeftNodeID, int RightNodeID) GetDirectionalEndpointIDs(
            StationLinkRow stationLink,
            StationNodeRow fromNode,
            StationNodeRow toNode)
        {
            var xComparison = CompareCoordinate(fromNode.X, toNode.X);

            if (xComparison < 0)
            {
                return (fromNode.ID, toNode.ID);
            }

            if (xComparison > 0)
            {
                return (toNode.ID, fromNode.ID);
            }

            return (stationLink.FromNodeID, stationLink.ToNodeID);
        }

        private static int CompareCoordinate(double first, double second)
        {
            var difference = first - second;

            if (Math.Abs(difference) <= CoordinateTolerance)
            {
                return 0;
            }

            return difference < 0 ? -1 : 1;
        }

        private static void EnsureAcyclic(DirectiveGraph graph)
        {
            var nodeIDs = graph.Nodes.Select(node => node.ID).ToHashSet();
            var outgoingLinksByNodeID = graph.Nodes.ToDictionary(node => node.ID, _ => new List<DirectiveGraphLink>());
            var incomingCountsByNodeID = graph.Nodes.ToDictionary(node => node.ID, _ => 0);

            foreach (var link in graph.Links)
            {
                if (!nodeIDs.Contains(link.FromNodeID) || !nodeIDs.Contains(link.ToNodeID))
                {
                    throw new InvalidOperationException(
                        $"Directive graph {graph.Direction} contains link {link.ID} with missing endpoint.");
                }

                if (link.FromNodeID == link.ToNodeID)
                {
                    throw new InvalidOperationException(
                        $"Directive graph {graph.Direction} contains a self-loop on node {link.FromNodeID} through link {link.ID}.");
                }

                outgoingLinksByNodeID[link.FromNodeID].Add(link);
                incomingCountsByNodeID[link.ToNodeID]++;
            }

            var readyNodeIDs = new Queue<int>(incomingCountsByNodeID
                .Where(pair => pair.Value == 0)
                .Select(pair => pair.Key));
            var visitedNodeCount = 0;

            while (readyNodeIDs.Count > 0)
            {
                var nodeID = readyNodeIDs.Dequeue();
                visitedNodeCount++;

                foreach (var link in outgoingLinksByNodeID[nodeID])
                {
                    incomingCountsByNodeID[link.ToNodeID]--;

                    if (incomingCountsByNodeID[link.ToNodeID] == 0)
                    {
                        readyNodeIDs.Enqueue(link.ToNodeID);
                    }
                }
            }

            if (visitedNodeCount != graph.Nodes.Count)
            {
                var cycleNodeIDs = incomingCountsByNodeID
                    .Where(pair => pair.Value > 0)
                    .Select(pair => pair.Key)
                    .OrderBy(nodeID => nodeID)
                    .ToArray();

                throw new InvalidOperationException(
                    $"Directive graph {graph.Direction} contains a closed cycle involving node IDs: {string.Join(", ", cycleNodeIDs)}.");
            }
        }

    }
}
