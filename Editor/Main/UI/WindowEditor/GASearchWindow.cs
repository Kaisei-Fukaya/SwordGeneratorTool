using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using System;
using System.IO;
using System.Reflection;
using System.Linq;
using SSL.Data.Utils;

namespace SSL.Graph
{
    public class GASearchWindow : ScriptableObject, ISearchWindowProvider
    {
        GraphicalAssetGraphView _graphView;
        public void Initialise(GraphicalAssetGraphView graphView)
        {
            _graphView = graphView;
        }
        public virtual List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> searchTreeEntries = new List<SearchTreeEntry>()
            {
                new SearchTreeGroupEntry(new GUIContent("Create Element"))
            };
            string sourcePath = $"{GAGenDataUtils.BasePath}Editor/Main/UI/WindowEditor/Elements/";
            string[] allFolders = GAGenDataUtils.GetFolderPaths(sourcePath);

            foreach (string folder in allFolders)
            {
                string[] fileNames = GAGenDataUtils.GetFileNames(folder + "/");
                string folderName = folder.Replace(sourcePath, "");
                searchTreeEntries.Add(new SearchTreeGroupEntry(new GUIContent(folderName), 1));
                foreach (string fileName in fileNames)
                {
                    searchTreeEntries.Add(new SearchTreeEntry(new GUIContent(GAGenDataUtils.CleanFileName(fileName)))
                    {
                        level = 2,
                        userData = GAGenDataUtils.GetNodeTypeFromName(fileName)
                    });
                }
            }
            return searchTreeEntries;
        }

        public virtual bool OnSelectEntry(SearchTreeEntry SearchTreeEntry, SearchWindowContext context)
        {
            Vector2 localMousePosition = _graphView.GetLocalMousePosition(context.screenMousePosition, true);

            if (SearchTreeEntry.userData is NodeType)
            {
                GraphViewNode node = _graphView.CreateNode((NodeType)SearchTreeEntry.userData, localMousePosition);
                _graphView.AddElement(node);
            }
            return true;
        }
    }
}