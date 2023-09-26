using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System;
using UnityEditor.Experimental.GraphView;
using SSL.Data;
using SSL.Data.Utils;
using System.IO;

namespace SSL.Graph
{
    public class GraphicalAssetGeneratorWindow : EditorWindow
    {
        StyleSheet _variablesStyleSheet;
        StyleSheet _generateStyleVariables, _trainStyleVariables;
        StyleSheet _toolbarToggleStyles;
        GraphicalAssetGraphView _graphView;
        PreviewBox _previewWindow;
        ConfigBox _configBox;
        VisualElement _previewConfigBox;
        VisualElement _mainView;
        public bool inTrainingMode;

        public int subdiv { get; private set; } = 2;
        public bool facetedShading { get; private set; } = false;
        public List<(int, Material)> materials = new List<(int, Material)>() {};

        void SetSubdiv(int value)
        {
            subdiv = value;
            _graphView.NodeUpdateFlag();
        }

        public float spacing { get; private set; } = 0f;

        void SetSpacing(float value)
        {
            spacing = value;
            _graphView.NodeUpdateFlag();
        }

        void SetShading(bool value)
        {
            facetedShading = value;
            _graphView.NodeUpdateFlag();
        }

        void SetMaterials(List<(int, Material)> mats)
        {
            if (mats != materials)
            {
                materials = mats;
                _graphView.NodeUpdateFlag();
            }
        }

        Material[] GetMaterials()
        {
            materials.Sort((x, y) => x.Item1.CompareTo(y.Item1));
            Material[] materialsArray = new Material[materials.Count];
            for (int i = 0; i < materialsArray.Length; i++)
            {
                materialsArray[i] = materials[i].Item2;
            }
            return materialsArray;
        }

        GAGenData _saveData;
        Inference _inference;

        [MenuItem("Window/Graphical Asset Generator")]
        public static void ShowWindow()
        {
            GetWindow<GraphicalAssetGeneratorWindow>();
        }

        private void CreateGUI()
        {
            Material defaultMat = (Material)AssetDatabase.LoadAssetAtPath($"{GAGenDataUtils.BasePath}Editor/Assets/DefaultMat.mat", typeof(Material));
            materials.Add((0, defaultMat));

            _inference = new Inference();
            this.titleContent = new GUIContent("Graphical Asset Generator");
            _generateStyleVariables = (StyleSheet)AssetDatabase.LoadAssetAtPath($"{GAGenDataUtils.BasePath}Editor/Assets/UIStyles/GraphicalAssetGeneratorVariablesGenerate.uss", typeof(StyleSheet));
            _trainStyleVariables = (StyleSheet)AssetDatabase.LoadAssetAtPath($"{GAGenDataUtils.BasePath}Editor/Assets/UIStyles/GraphicalAssetGeneratorVariablesTrain.uss", typeof(StyleSheet));
            _toolbarToggleStyles = (StyleSheet)AssetDatabase.LoadAssetAtPath($"{GAGenDataUtils.BasePath}Editor/Assets/UIStyles/GraphicalAssetToolbarToggleStyle.uss", typeof(StyleSheet));
            AddToolbar();
            _mainView = new VisualElement()
            {
                name = "mainView"
            };
            _mainView.AddToClassList("root");
            //mainView.StretchToParentSize();
            _saveData = CreateInstance<GAGenData>();
            rootVisualElement.Add(_mainView);
            AddGraphView();
            AddStyles();
            AddPreviewConfigWindow();
            SetupDragAndDrop();
        }

        private void OnDestroy()
        {
            if(_configBox != null)
                _configBox.CloseMaterialsWindow();
        }

        private void SetupDragAndDrop()
        {
            Color origColour = _mainView.style.backgroundColor.value;
            //Drag enter
            _mainView.RegisterCallback<DragEnterEvent>(e =>
            {
                _mainView.style.backgroundColor = new StyleColor(new Color(0f, 0f, 100f, 0.3f));
            });
            //Drag leave
            _mainView.RegisterCallback<DragLeaveEvent>(e =>
            {
                _mainView.style.backgroundColor = new StyleColor(origColour);
            });
            //Drag updated
            _mainView.RegisterCallback<DragUpdatedEvent>(e =>
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            });
            //Drag perform
            _mainView.RegisterCallback<DragPerformEvent>(e =>
            {
                var draggedObjects = DragAndDrop.objectReferences;

                for (int i = 0; i < draggedObjects.Length; i++)
                {
                    if (draggedObjects[i] is GAGenData)
                    {
                        Load((GAGenData)draggedObjects[i]);
                        break;
                    }
                }

                _mainView.style.backgroundColor = new StyleColor(origColour);
            });
            //Drag exited
            _mainView.RegisterCallback<DragExitedEvent>(e =>
            {
                _mainView.style.backgroundColor = new StyleColor(origColour);
            });
        }

        void AddGraphView()
        {
            _graphView = new GraphicalAssetGraphView(this);
            _graphView.StretchToParentSize();
            _mainView.Add(_graphView);
        }

        private void AddToolbar()
        {
            Toolbar toolbar = new Toolbar();

            ToolbarButton saveButton = new ToolbarButton()
            {
                text = "Save Graph"
            };
            saveButton.clicked += Save;

            ToolbarButton saveAsButton = new ToolbarButton()
            {
                text = "Save Graph As"
            };
            saveAsButton.clicked += SaveAs;

            ToolbarButton loadButton = new ToolbarButton()
            {
                text = "Load Graph"
            };
            loadButton.clicked += Load;

            ToolbarButton savePrefabButton = new ToolbarButton()
            {
                text = "Export Prefab"
            };
            savePrefabButton.clicked += ExportPrefab;

            //ToolbarButton img2ModelButton = new ToolbarButton()
            //{
            //    text = "Generate from Image"
            //};
            //img2ModelButton.clicked += GenerateFromImage;

            //ToolbarButton rand2ModelButton = new ToolbarButton()
            //{
            //    text = "Generate Random"
            //};
            //rand2ModelButton.clicked += GenerateFromRandom;

            //ToolbarButton interpButton = new ToolbarButton()
            //{
            //    text = "Interpolate"
            //};
            //interpButton.clicked += Interpolate;


            ToolbarSpacer spacer1 = new ToolbarSpacer();
            ToolbarSpacer spacer2 = new ToolbarSpacer();
            ToolbarSpacer spacer3 = new ToolbarSpacer();
            toolbar.Add(saveButton);
            toolbar.Add(spacer1);
            toolbar.Add(saveAsButton);
            toolbar.Add(loadButton);
            toolbar.Add(spacer2);
            //toolbar.Add(img2ModelButton);
            //toolbar.Add(rand2ModelButton);
            //toolbar.Add(interpButton);
            toolbar.Add(spacer3);
            toolbar.Add(savePrefabButton);

            rootVisualElement.Add(toolbar);
        }

        public Material[] GetMatList()
        {
            //if(_configBox != null)
            //    return _configBox.GetMaterialList();
            //return new Material[0];
            return GetMaterials();
        }

        private void AddPreviewConfigWindow()
        {
            _previewConfigBox = new VisualElement()
            {
                name = "previewConfigWindow"
            };

            _previewWindow = new PreviewBox();
            _previewWindow.Initialise(_graphView, GAGenDataUtils.NodesToData(_graphView.Nodes), GetMatList());
            _previewWindow.name = "previewWindow";
            _graphView.PreviewWindow = _previewWindow;
            _previewConfigBox.Add(_previewWindow);

            _configBox = new ConfigBox()
            {
                name = "configWindow",
                inference = _inference
            };
            _configBox.Initialise(this, Load, SetSubdiv, SetSpacing, SetShading, SetMaterials);

            _previewConfigBox.Add(_configBox);

            rootVisualElement.Add(_previewConfigBox);
        }

        public void RandomiseSelectedNodes(int valueGroupIndex)
        {
            _graphView.RandomiseSelectedNodes(valueGroupIndex);
        }

        public void ExportPrefab()
        {
            string path = EditorUtility.SaveFilePanelInProject("Please select a location to save the prefab to.", "Generated Prefab", "", "");
            if (path == "")
                return;
            GameObject currentObject = _previewWindow.GetCurrentObject(path);
            //PrefabUtility.SaveAsPrefabAsset(currentObject, $"{path}.prefab");
            //DestroyImmediate(currentObject);
        }

        public void SaveProcessPack()
        {
            GAGenData dataToSave = GetGraphData();
            string name = AutoGenName(dataToSave.creator);
            //Get tmp images and original images from each image field
            //Export obj of result including materials (use the prefab instance?).
            if (!Directory.Exists("Assets/Generator Results"))
                AssetDatabase.CreateFolder("Assets", "Generator Results");
            AssetDatabase.CreateFolder("Assets/Generator Results", name);
            AssetDatabase.CreateAsset(dataToSave, $"Assets/Generator Results/{name}/{name}.asset");
            GameObject currentObject = _previewWindow.GetCurrentObject($"Assets/Generator Results/{name}/{name}");
            //PrefabUtility.SaveAsPrefabAsset(currentObject, $"Assets/Generator Results/{name}/{name}.prefab");

            var imagePaths = _configBox.GetImagePaths();
            if (imagePaths.sourceImagePaths[0] != "")
                File.Copy(imagePaths.sourceImagePaths[0], $"Assets/Generator Results/{name}/{name}_source-recon{Path.GetExtension(imagePaths.sourceImagePaths[0])}");
            if (imagePaths.sourceImagePaths[1] != "")
                File.Copy(imagePaths.sourceImagePaths[1], $"Assets/Generator Results/{name}/{name}_source-interpA{Path.GetExtension(imagePaths.sourceImagePaths[1])}");
            if (imagePaths.sourceImagePaths[2] != "")
                File.Copy(imagePaths.sourceImagePaths[2], $"Assets/Generator Results/{name}/{name}_source-interpB{Path.GetExtension(imagePaths.sourceImagePaths[2])}");

            if (imagePaths.processedImagePaths[0] != "")
                File.Copy(imagePaths.processedImagePaths[0], $"Assets/Generator Results/{name}/{name}_processed-recon{Path.GetExtension(imagePaths.processedImagePaths[0])}");
            if (imagePaths.processedImagePaths[1] != "")
                File.Copy(imagePaths.processedImagePaths[1], $"Assets/Generator Results/{name}/{name}_processed-interpA{Path.GetExtension(imagePaths.processedImagePaths[1])}");
            if (imagePaths.processedImagePaths[2] != "")
                File.Copy(imagePaths.processedImagePaths[2], $"Assets/Generator Results/{name}/{name}_processed-interpB{Path.GetExtension(imagePaths.processedImagePaths[2])}");

            //Create zip file
            System.IO.Compression.ZipFile.CreateFromDirectory($"Assets/Generator Results/{name}", $"Assets/Generator Results/{name}.zip");

            //DestroyImmediate(currentObject);
        }

        string AutoGenName(int creatorID)
        {
            DateTime currentDateTime = DateTime.Now;
            string creatorType = "";
            switch (creatorID) 
            {
                default:
                    creatorType = "manual";
                    break;
                case 0:
                    creatorType = "random";
                    break;
                case 1:
                    creatorType = "reconstruction";
                    break;
                case 2:
                    creatorType = "interpolation";
                    break;
            }
            return creatorType + "_" + currentDateTime.ToString("yyyy-MM-dd-HH-mm-ss");
        }

        void Save()
        {
            //If save data doesn't exist, call save as
            if (_saveData == null)
            {
                SaveAs();
                return;
            }

            //Otherwise overwrite the data
            SaveData(_graphView, _saveData);
            //AssetDatabase.SaveAsset(_saveData);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();

            _saveData.UpdateRequiredFlag = true;

            //void RefreshSelection()
            //{
            //    //if (Selection.activeObject == null)
            //    //{
            //    //    Selection.activeObject = _saveData;
            //    //    _saveData.UpdateRequiredFlag = true;
            //    //    GAGenDataUtils.RepaintInspector(typeof(GraphReaderEditor));
            //    //}
            //    //else
            //    //{
            //    //    Selection.activeObject = null;
            //    //    EditorApplication.delayCall += RefreshSelection;
            //    //}
            //}

            //EditorApplication.delayCall += RefreshSelection;
        }

        void SaveData(GraphicalAssetGraphView graphView, GAGenData saveData)
        {
            List<GraphViewNode> nodes = graphView.Nodes;
            saveData.Nodes = new List<GAGenNodeData>();
            foreach (GraphViewNode node in nodes)
            {
                saveData.Nodes.Add(GAGenDataUtils.GraphNodeToNodeData(node));
            }
            saveData.materials = materials;
            EditorUtility.SetDirty(saveData);
        }

        void SaveAs()
        {
            string savePath = EditorUtility.SaveFilePanelInProject("Save As", "New Graphical Asset Generator", "asset", "");
            if (savePath == string.Empty)
                return;
            _saveData = CreateInstance<GAGenData>();
            SaveData(_graphView, _saveData);
            AssetDatabase.CreateAsset(_saveData, savePath);
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = _saveData;
        }

        public GAGenData GetGraphData()
        {
            List<GraphViewNode> nodes = _graphView.Nodes;
            GAGenData graph = CreateInstance<GAGenData>();
            graph.Nodes = new List<GAGenNodeData>();
            foreach (GraphViewNode node in nodes)
            {
                graph.Nodes.Add(GAGenDataUtils.GraphNodeToNodeData(node));
            }
            graph.materials = materials;
            graph.creator = _saveData.creator;
            return graph;
        }

        public void Load(GAGenData data)
        {
            if (data == null)
            {
                Debug.LogWarning("Selected file was not compatible");
                return;
            }

            _saveData = data;

            ClearGraph();

            Dictionary<string, NodeAndData> iDToNode = new Dictionary<string, NodeAndData>();
            List<string> allNodeIDs = new List<string>();
            foreach (GAGenNodeData nodeData in _saveData.Nodes)
            {
                GraphViewNode newNode = _graphView.CreateNode(nodeData.NodeType, nodeData.Position);
                newNode.ID = nodeData.ID;
                newNode.LoadSettings(nodeData.Settings);
                newNode.Draw();
                iDToNode.Add(newNode.ID, new NodeAndData(newNode, nodeData));
                allNodeIDs.Add(newNode.ID);
                _graphView.AddElement(newNode);
            }

            //Make sure this is done last so that all ports are drawn
            foreach (string id in allNodeIDs)
            {
                NodeAndData nodeAndData = iDToNode[id];
                GraphViewNode node = nodeAndData.node;
                GAGenNodeData nodeData = nodeAndData.data;
                List<GraphicalAssetPort> ports = node.GetPorts(true);
                if (nodeData.InGoingConnections == null)
                    continue;
                //Gen connections
                if (ports.Count != nodeData.InGoingConnections.Count)
                {
                    //Debug.Log("dddd");
                    continue;
                }
                for (int i = 0; i < nodeData.InGoingConnections.Count; i++)
                {
                    if (nodeData.InGoingConnections[i].iD == "EMPTY")
                        continue;

                    GraphViewNode otherNode = iDToNode[nodeData.InGoingConnections[i].iD].node;
                    List<GraphicalAssetPort> otherPorts = otherNode.GetPorts(false);
                    Edge edge = otherPorts[nodeData.InGoingConnections[i].indexInOther].ConnectTo(ports[i].GetPort());
                    if (edge == null)
                        continue;
                    _graphView.AddElement(edge);
                }
            }

            if(_saveData.materials.Count > 0 && _saveData.materials[0].Item2 != null)
                materials = _saveData.materials;
            EditorApplication.delayCall += _graphView.CentreGraphOnNodes;
            _graphView.NodeUpdateFlag();
            titleContent = new GUIContent($"{_saveData.name} (Graphical Asset Generator)");
        }

        void Load()
        {
            string path = EditorUtility.OpenFilePanel("Load", "Assets", "asset");
            path = path.Replace(Application.dataPath, "Assets");
            GAGenData data = AssetDatabase.LoadAssetAtPath<GAGenData>(path);
            Load(data);
        }

        void GenerateFromImage()
        {
            Generate(0);
        }

        void GenerateFromRandom()
        {
            Generate(1);
        }

        void Interpolate()
        {
            Generate(2);
        }

        void Generate(int mode = 0)
        {
            //Lazy load inference instance
            if (_inference == null)
                _inference = new Inference();

            switch (mode)
            {
                default:
                    string filePath = EditorUtility.OpenFilePanel("Please provide an image", "", "");
                    var im2result = _inference.Img2Model(filePath);
                    var model = im2result;
                    if (model != null)
                    {
                        Load(model);
                    }
                    break;
                case 1:
                    var r2result = _inference.Rand2Model();
                    if (r2result != null)
                        Load(r2result);
                    break;
                case 2:
                    string filePath1 = EditorUtility.OpenFilePanel("Please provide an image", "", "");
                    string filePath2 = EditorUtility.OpenFilePanel("Please provide an image", "", "");

                    var interpResult = _inference.Interp(filePath1, filePath2, 0.5f);

                    if (interpResult != null)
                        Load(interpResult);
                    break;
            }
        }

        struct NodeAndData
        {
            public GraphViewNode node;
            public GAGenNodeData data;
            public NodeAndData(GraphViewNode node, GAGenNodeData data)
            {
                this.node = node;
                this.data = data;
            }
        }

        void ClearGraph()
        {
            _graphView.ClearGraph();
        }

        void SetStyle()
        {
            rootVisualElement.styleSheets.Remove(_variablesStyleSheet);
            if (inTrainingMode)
                _variablesStyleSheet = _trainStyleVariables;
            else
                _variablesStyleSheet = _generateStyleVariables;
            rootVisualElement.styleSheets.Add(_variablesStyleSheet);
        }

        void AddStyles()
        {
            _variablesStyleSheet = _generateStyleVariables;
            StyleSheet windowStyleSheet = (StyleSheet)AssetDatabase.LoadAssetAtPath($"{GAGenDataUtils.BasePath}Editor/Assets/UIStyles/GraphicalAssetGeneratorWindowStyle.uss", typeof(StyleSheet));
            rootVisualElement.styleSheets.Add(_variablesStyleSheet);
            rootVisualElement.styleSheets.Add(windowStyleSheet);
        }
    }

}