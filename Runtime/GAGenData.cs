using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using System;

namespace SSL.Data
{
    [CreateAssetMenu(menuName = "Graphical Asset Generator")]
    public class GAGenData : ScriptableObject
    {
        [field: SerializeField] public List<GAGenNodeData> Nodes { get; set; } = new List<GAGenNodeData>();
        [field: SerializeField] public bool UpdateRequiredFlag { get; set; } = false;

        [field: SerializeField] public int creator = -1; //-1: manual, 0: random, 1: reconstruction, 2: interpolation
        [field: SerializeField] public bool hasBeenManuallyEdited = false;
        [field: SerializeField] public List<(int, Material)> materials = new List<(int, Material)>() { (0, null) };

        public NodesAndEdges GetNodesAndEdges(int subdiv)
        {
            NodesAndEdges nodesAndEdges = new NodesAndEdges();
            List<SElement> newNodes = new List<SElement>();
            List<SwordCreator.NestedList> newEdges = new List<SwordCreator.NestedList>();
            Dictionary<string, int> indexLookup = new Dictionary<string, int>();

            SortNodes();


            //Nodes
            for (int i = 0; i < Nodes.Count; i++)
            {
                NodeParams newParams = Nodes[i].Settings.parameters;
                if (Nodes[i].InGoingConnections[0].iD == "EMPTY")
                {
                    newParams.visibleFaces.bottom = true;
                }
                if (Nodes[i].OutGoingConnections[0].iD == "EMPTY")
                {
                    newParams.visibleFaces.top = true;
                }
                if(Nodes[i].OutGoingConnections.Count == 5)
                {
                    if(Nodes[i].OutGoingConnections[1].iD == "EMPTY")
                    {
                        newParams.visibleFaces.left = true;
                    }
                    if (Nodes[i].OutGoingConnections[2].iD == "EMPTY")
                    {
                        newParams.visibleFaces.front = true;
                    }
                    if (Nodes[i].OutGoingConnections[3].iD == "EMPTY")
                    {
                        newParams.visibleFaces.right = true;
                    }
                    if (Nodes[i].OutGoingConnections[4].iD == "EMPTY")
                    {
                        newParams.visibleFaces.back = true;
                    }
                }

                SElement newNode;
                switch (Nodes[i].NodeType)
                {
                    case Graph.NodeType.Segment:
                        newNode = new SSegment();
                        break;
                    case Graph.NodeType.Branch:
                        newNode = new SBranch();
                        break;
                    default:
                        newNode = new SSegment();
                        break;
                }
                newNode.Build(subdiv, newParams);
                //Debug.Log(newParams.subMeshIndex);
                newNodes.Add(newNode);
                indexLookup.Add(Nodes[i].ID, i);
            }

            nodesAndEdges.nodes = newNodes.ToArray();

            //Edges
            for (int i = 0; i < Nodes.Count; i++)
            {
                SwordCreator.NestedList newEdgeSet = new SwordCreator.NestedList();
                newEdgeSet.val = new List<int>();
                for (int j = 0; j < Nodes[i].OutGoingConnections.Count; j++)
                {
                    if (Nodes[i].OutGoingConnections[j].iD == "EMPTY")
                    {
                        newEdgeSet.val.Add(-1);
                        continue;
                    }
                    newEdgeSet.val.Add(indexLookup[Nodes[i].OutGoingConnections[j].iD]);
                    //Debug.Log($"node {i}, connections {j}, connects to node index: {newEdgeSet.val[0]}");
                }
                newEdges.Add(newEdgeSet);
            }

            //Convert Edges to Dict
            Dictionary<int, int[]> edgesConverted = new Dictionary<int, int[]>();
            for (int i = 0; i < newEdges.Count; i++)
            {
                edgesConverted.Add(i, newEdges[i].val.ToArray());
                //Debug.Log(edgesConverted[i].Length);
            }


            nodesAndEdges.edges = edgesConverted;

            return nodesAndEdges;
        }

        //Depth first sorting
        private void SortNodes()
        {
            var sortedList = new List<GAGenNodeData>();
            Dictionary<GAGenNodeData, bool> permMarkLookup = new Dictionary<GAGenNodeData, bool>();
            Dictionary<GAGenNodeData, bool> tempMarkLookup = new Dictionary<GAGenNodeData, bool>();
            for (int i = 0; i < Nodes.Count; i++)
            {
                permMarkLookup.Add(Nodes[i], false);
                tempMarkLookup.Add(Nodes[i], false);
            }

            while (permMarkLookup.ContainsValue(false))
            {
                foreach (KeyValuePair<GAGenNodeData, bool> entry in permMarkLookup)
                {
                    if (entry.Value == false)
                    {
                        Visit(entry.Key);
                        break;
                    }
                }
            }

            void Visit(GAGenNodeData n)
            {
                if (permMarkLookup[n])
                    return;
                if (tempMarkLookup[n])
                    throw new System.Exception("There is a loop in the graph!!");

                tempMarkLookup[n] = true;

                for (int i = 0; i < n.OutGoingConnections.Count; i++)
                {
                    if (n.OutGoingConnections[i].iD == "EMPTY")
                        continue;
                    Visit(NodeDataFromID(Nodes, n.OutGoingConnections[i].iD));
                }

                tempMarkLookup[n] = false;
                permMarkLookup[n] = true;
                sortedList.Insert(0, n);
            }

            Nodes = sortedList;

        }


        GAGenNodeData NodeDataFromID(List<GAGenNodeData> nodeList, string iD)
        {
            for (int i = 0; i < nodeList.Count; i++)
            {
                if (nodeList[i].ID == iD)
                    return nodeList[i];
            }
            //Debug.Log($"{iD} was null?");
            return null;
        }

        public struct NodesAndEdges
        {
            public SElement[] nodes;
            public Dictionary<int, int[]> edges;
        }
    }
}