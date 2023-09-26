using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEditor;
using SSL.Data.Utils;
using SSL.Data;

namespace SSL.Graph
{
    public class GAPreview : GraphElement
    {
        Editor _previewEditor;
        SwordGraph _swordGraph;
        GameObject _previewObject;
        MeshRenderer _previewMR;
        MeshFilter _previewMF;

        public void Initialise(GraphView graphView, GAGenData data)
        {
            styleSheets.Add((StyleSheet)AssetDatabase.LoadAssetAtPath($"{GAGenDataUtils.BasePath}Editor/Assets/UIStyles/GraphicalAssetPreviewStyle.uss", typeof(StyleSheet)));

            _previewObject = new GameObject();
            _previewMR = _previewObject.AddComponent<MeshRenderer>();
            _previewMF = _previewObject.AddComponent<MeshFilter>();

            VisualElement topContainer = new VisualElement()
            {
                name = "topContainer"
            };
            VisualElement mainContainer = new VisualElement()
            {
                name = "mainContainer"
            };

            TextElement titleElement = new TextElement()
            {
                text = "Preview",
                name = "titleElement"
            };

            IMGUIContainer previewContainer = new IMGUIContainer(() =>
                {
                    var guiStyle = new GUIStyle();
                    guiStyle.normal.background = Texture2D.blackTexture;
                    _previewEditor.OnPreviewGUI(GUILayoutUtility.GetRect(0, 500, 0, 500), guiStyle);
                }
            );

            topContainer.Add(titleElement);
            mainContainer.Add(previewContainer);

            contentContainer.Add(topContainer);
            contentContainer.Add(mainContainer);

            capabilities |= Capabilities.Movable | Capabilities.Resizable;
            var dragger = new Dragger() { clampToParentEdges = true };
            this.AddManipulator(dragger);
            hierarchy.Add(new Resizer());

            RegisterCallback<DragUpdatedEvent>(e =>
            {
                e.StopPropagation();
            });

            RegisterCallback<WheelEvent>(e =>
            {
                e.StopPropagation();
            });

            RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.button == (int)MouseButton.LeftMouse)
                    graphView.ClearSelection();
                // prevent ContentDragger manipulator
                e.StopPropagation();
            });

            
            focusable = true;

            _swordGraph = new SwordGraph();
            UpdateMesh(data);
        }

        public void UpdateMesh(GAGenData data)
        {
            GAGenData.NodesAndEdges nodesAndEdges = data.GetNodesAndEdges(2);
            _swordGraph.Load(2, 1f, nodesAndEdges.nodes, nodesAndEdges.edges, false);
            if(_previewEditor != null)
                Object.DestroyImmediate(_previewEditor);
            _previewMF.mesh = _swordGraph.Generate();
            //_previewMR.materials = new Material[] {Material. };
            _previewEditor = Editor.CreateEditor(_previewObject);
        }

        //void ReplaceMeshData(Mesh newMesh)
        //{
        //    _mesh.vertices = newMesh.vertices;
        //    _mesh.normals = newMesh.normals;
        //    _mesh.uv = newMesh.uv;
        //    _mesh.triangles = newMesh.triangles;
        //    _mesh.tangents = newMesh.tangents;
        //    _mesh.UploadMeshData(false);
        //}
    }
}