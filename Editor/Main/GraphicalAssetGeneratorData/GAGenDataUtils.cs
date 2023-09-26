using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using SSL.Graph;
using System.Linq;
using System.IO;
using UnityEditor;

namespace SSL.Data.Utils
{
    public static class GAGenDataUtils
    {
        public static Dictionary<NodeType, string> DisplayNameLookup = new Dictionary<NodeType, string>
        {
            { NodeType.Segment,  "Segment" },
            { NodeType.Branch,   "Branch" }
        };
        public static GAGenNodeData GraphNodeToNodeData(GraphViewNode node)
        {
            return new GAGenNodeData()
            {
                ID = node.ID,
                OutGoingConnections = node.GetOutgoingConnectionIDs(),
                InGoingConnections = node.GetIngoingConnectionIDs(),
                Position = node.GetPosition().position,
                NodeType = node.NodeType,
                Settings = node.GetSettings()
            };
        }

        public static string BasePath
        {
            get
            {
                //"Packages/com.gagen.core/"
                return "Packages/com.kf.gagen/";//"Assets/Scripts/SwordShapeLanguage/";
            }
        }

        public static NodeType GetNodeTypeFromName(string name)
        {
            if (Enum.GetNames(typeof(NodeType)).Contains(name))
            {
                return (NodeType)Enum.Parse(typeof(NodeType), name);
            }
            throw new System.Exception($"GANodeType of {name} does not exist!");
        }

        public static string[] GetFileNames(string folderPath)
        {
            string[] paths = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
            string[] names = new string[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                //Remove rest of path to get name
                names[i] = paths[i].Replace(folderPath, "").Replace("Node", "").Split('.')[0];
            }
            names = names.Distinct().ToArray();
            return names;
        }

        public static string[] GetFolderPaths(string path)
        {
            return Directory.GetDirectories(path, "*", SearchOption.AllDirectories);
        }
        public static string CleanFileName(string fileName)
        {
            string cleanName;
            bool containsException = false;

            if (fileName.Contains("2D") || fileName.Contains("3D"))
                containsException = true;

            //Replace 2 with To
            cleanName = string.Concat(fileName.Select(x => x == '2' ? "To" : x.ToString()));
            //Add spaces before caps
            cleanName = string.Concat(cleanName.Select(x => Char.IsUpper(x) ? " " + x : x.ToString())).TrimStart(' ');

            if (containsException)
            {
                if (cleanName.Contains("To D"))
                    cleanName = cleanName.Replace("To D", "2D");
                if (cleanName.Contains("3 D"))
                    cleanName = cleanName.Replace("3 D", " 3D");
            }

            return cleanName;
        }

        public static GraphViewNode IDToGraphViewNode(string iD, IEnumerable<GraphViewNode> nodes)
        {
            foreach (var item in nodes)
            {
                if (item.ID == iD)
                    return item;
            }
            return null;
        }

        public static GAGenData NodesToData(List<GraphViewNode> nodes)
        {
            var output = ScriptableObject.CreateInstance<GAGenData>();
            output.Nodes = new List<GAGenNodeData>();
            foreach (GraphViewNode node in nodes)
            {
                output.Nodes.Add(GAGenDataUtils.GraphNodeToNodeData(node));
            }
            return output;
        }

        public static void RepaintInspector(System.Type t)
        {
            Editor[] ed = (Editor[])Resources.FindObjectsOfTypeAll<Editor>();
            for (int i = 0; i < ed.Length; i++)
            {
                if (ed[i].GetType() == t)
                {
                    Debug.Log("Repaint");
                    ed[i].Repaint();
                    return;
                }
            }
        }
    }
}