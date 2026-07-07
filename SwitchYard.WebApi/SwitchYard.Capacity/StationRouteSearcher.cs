using System;
using System.Linq;

namespace SwitchYard.Capacity
{
    public class StationRoute
    {
        public DirectiveGraphDirections Direction { get; set; }

        public StationNodeRow StartNode { get; set; } = null!;

        public StationNodeRow EndNode { get; set; } = null!;

        public List<StationLinkRow> Links { get; set; } = new();

        public List<StationNodeRow> Nodes { get; set; } = new();
    }

    public class StationRouteSearcher
    {
        private readonly Dictionary<int, StationNodeRow> _stationNodeByID;

        private readonly Dictionary<int, StationLinkRow> _stationLinkByID;

        public List<DirectiveGraph> directiveGraphs { get; set; } = new();

        public StationRouteSearcher(List<StationNodeRow> stationNodes, List<StationLinkRow> stationLinks) 
        {
            ArgumentNullException.ThrowIfNull(stationNodes);
            ArgumentNullException.ThrowIfNull(stationLinks);

            _stationNodeByID = BuildStationNodeLookup(stationNodes);
            _stationLinkByID = BuildStationLinkLookup(stationLinks);

            var builder = new DirectiveGraphBuilder();
            directiveGraphs = builder.BuildDirectiveGraphs(stationNodes, stationLinks);
        }

        public List<StationRoute> Search(StationNodeRow startNode, StationNodeRow endNode)
        {
            ArgumentNullException.ThrowIfNull(startNode);
            ArgumentNullException.ThrowIfNull(endNode);

            if (!_stationNodeByID.ContainsKey(startNode.ID))
            {
                throw new ArgumentException($"Start node {startNode.ID} does not exist in the station network.", nameof(startNode));
            }

            if (!_stationNodeByID.ContainsKey(endNode.ID))
            {
                throw new ArgumentException($"End node {endNode.ID} does not exist in the station network.", nameof(endNode));
            }

            var routes = new List<StationRoute>();

            foreach (var directiveGraph in directiveGraphs) 
            {
                routes.AddRange(SearchDirectiveGraph(directiveGraph, startNode.ID, endNode.ID));
            }

            return routes;
        }

        private static Dictionary<int, StationNodeRow> BuildStationNodeLookup(IEnumerable<StationNodeRow> stationNodes)
        {
            var nodeByID = new Dictionary<int, StationNodeRow>();

            foreach (var node in stationNodes)
            {
                if (!nodeByID.TryAdd(node.ID, node))
                {
                    throw new ArgumentException($"Duplicate StationNode ID {node.ID}.", nameof(stationNodes));
                }
            }

            return nodeByID;
        }

        private static Dictionary<int, StationLinkRow> BuildStationLinkLookup(IEnumerable<StationLinkRow> stationLinks)
        {
            var linkByID = new Dictionary<int, StationLinkRow>();

            foreach (var link in stationLinks)
            {
                if (!linkByID.TryAdd(link.ID, link))
                {
                    throw new ArgumentException($"Duplicate StationLink ID {link.ID}.", nameof(stationLinks));
                }
            }

            return linkByID;
        }

        private List<StationRoute> SearchDirectiveGraph(DirectiveGraph directiveGraph, int startNodeID, int endNodeID)
        {
            var routes = new List<StationRoute>();
            var outgoingLinksByNodeID = directiveGraph.Nodes.ToDictionary(node => node.ID, _ => new List<DirectiveGraphLink>());

            foreach (var link in directiveGraph.Links.OrderBy(link => link.ID))
            {
                if (!outgoingLinksByNodeID.TryGetValue(link.FromNodeID, out var outgoingLinks))
                {
                    throw new InvalidOperationException(
                        $"Directive graph {directiveGraph.Direction} contains link {link.ID} with missing from-node {link.FromNodeID}.");
                }

                outgoingLinks.Add(link);
            }

            var currentLinks = new List<DirectiveGraphLink>();
            var visitedNodeIDs = new HashSet<int> { startNodeID };

            void SearchFrom(int currentNodeID)
            {
                if (currentNodeID == endNodeID)
                {
                    routes.Add(BuildStationRoute(directiveGraph.Direction, startNodeID, endNodeID, currentLinks));
                    return;
                }

                if (!outgoingLinksByNodeID.TryGetValue(currentNodeID, out var outgoingLinks))
                {
                    return;
                }

                foreach (var link in outgoingLinks)
                {
                    if (!visitedNodeIDs.Add(link.ToNodeID))
                    {
                        continue;
                    }

                    currentLinks.Add(link);
                    SearchFrom(link.ToNodeID);
                    currentLinks.RemoveAt(currentLinks.Count - 1);
                    visitedNodeIDs.Remove(link.ToNodeID);
                }
            }

            SearchFrom(startNodeID);
            return routes;
        }

        private StationRoute BuildStationRoute(
            DirectiveGraphDirections direction,
            int startNodeID,
            int endNodeID,
            IReadOnlyCollection<DirectiveGraphLink> directiveLinks)
        {
            var routeNodeIDs = new List<int> { startNodeID };
            var stationLinks = new List<StationLinkRow>();

            foreach (var directiveLink in directiveLinks)
            {
                if (!_stationLinkByID.TryGetValue(directiveLink.ID, out var stationLink))
                {
                    throw new InvalidOperationException($"Directive graph link {directiveLink.ID} cannot be mapped to a station link.");
                }

                stationLinks.Add(stationLink);
                routeNodeIDs.Add(directiveLink.ToNodeID);
            }

            var stationNodes = routeNodeIDs
                .Select(nodeID =>
                {
                    if (!_stationNodeByID.TryGetValue(nodeID, out var stationNode))
                    {
                        throw new InvalidOperationException($"Directive graph node {nodeID} cannot be mapped to a station node.");
                    }

                    return stationNode;
                })
                .ToList();

            return new StationRoute
            {
                Direction = direction,
                StartNode = _stationNodeByID[startNodeID],
                EndNode = _stationNodeByID[endNodeID],
                Links = stationLinks,
                Nodes = stationNodes
            };
        }
    }
}
