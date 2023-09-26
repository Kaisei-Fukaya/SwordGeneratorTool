using SSL.Data;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SSL.Graph.Elements
{
    public class BranchNode : GraphViewNode
    {
        protected GraphicalAssetPort _inputPortBottom, _outputPortTop, _outputPortLeft, _outputPortForward, _outputPortRight, _outputPortBackward;
        protected GAPortType _inputPortType, _outputPortType;
        protected IntegerField _subMeshIndexField;
        protected FloatField _sizeWidthField, _sizeHeightField, _sizeDepthField;

        public override void Initialise(Vector2 position)
        {
            NodeType = NodeType.Branch;
            base.Initialise(position);
            _subMeshIndexField = new IntegerField();

            //Add callbacks
            _subMeshIndexField.RegisterValueChangedCallback(x => CallSettingsEditEvent());
            _sizeWidthField = new FloatField() { label = "Width:" };
            _sizeHeightField = new FloatField() { label = "Height:" };
            _sizeDepthField = new FloatField() { label = "Depth:" };

            _sizeWidthField.RegisterValueChangedCallback(x => { float v = Mathf.Max(0, x.newValue); _sizeWidthField.SetValueWithoutNotify(v); CallSettingsEditEvent(); });
            _sizeHeightField.RegisterValueChangedCallback(x => { float v = Mathf.Max(0, x.newValue); _sizeHeightField.SetValueWithoutNotify(v); CallSettingsEditEvent(); });
            _sizeDepthField.RegisterValueChangedCallback(x => { float v = Mathf.Max(0, x.newValue); _sizeDepthField.SetValueWithoutNotify(v); CallSettingsEditEvent(); });

            //IO
            _ingoingPorts = new List<GraphicalAssetPort>();
            _outgoingPorts = new List<GraphicalAssetPort>();

            _inputPortBottom = new GraphicalAssetPort(this, _inputPortType, UnityEditor.Experimental.GraphView.Orientation.Horizontal, Direction.Input, Port.Capacity.Single, "Bottom");
            _outputPortTop = new GraphicalAssetPort(this, _outputPortType, UnityEditor.Experimental.GraphView.Orientation.Horizontal, Direction.Output, Port.Capacity.Single, "Top");
            _outputPortLeft = new GraphicalAssetPort(this, _outputPortType, UnityEditor.Experimental.GraphView.Orientation.Horizontal, Direction.Output, Port.Capacity.Single, "Left");
            _outputPortForward = new GraphicalAssetPort(this, _outputPortType, UnityEditor.Experimental.GraphView.Orientation.Horizontal, Direction.Output, Port.Capacity.Single, "Forward");
            _outputPortRight = new GraphicalAssetPort(this, _outputPortType, UnityEditor.Experimental.GraphView.Orientation.Horizontal, Direction.Output, Port.Capacity.Single, "Right");
            _outputPortBackward = new GraphicalAssetPort(this, _outputPortType, UnityEditor.Experimental.GraphView.Orientation.Horizontal, Direction.Output, Port.Capacity.Single, "Back");
            _ingoingPorts.Add(_inputPortBottom);
            _outgoingPorts.Add(_outputPortTop);
            _outgoingPorts.Add(_outputPortLeft);
            _outgoingPorts.Add(_outputPortForward);
            _outgoingPorts.Add(_outputPortRight);
            _outgoingPorts.Add(_outputPortBackward);
        }

        public override NodeSetting GetSettings()
        {
            NodeSetting setting = base.GetSettings();
            setting.parameters.size = new Vector3(_sizeWidthField.value, _sizeHeightField.value, _sizeDepthField.value);
            setting.parameters.subMeshIndex = _subMeshIndexField.value;
            return setting;
        }

        public override void LoadSettings(NodeSetting setting)
        {
            _sizeWidthField.SetValueWithoutNotify(setting.parameters.size.x);
            _sizeHeightField.SetValueWithoutNotify(setting.parameters.size.y);
            _sizeDepthField.SetValueWithoutNotify(setting.parameters.size.z);
            _subMeshIndexField.SetValueWithoutNotify(setting.parameters.subMeshIndex);
        }

        public override void Draw()
        {
            base.Draw();

            ConfigPort(_inputPortBottom, 5);
            ConfigPort(_outputPortTop, 0);
            ConfigPort(_outputPortLeft, 1);
            ConfigPort(_outputPortForward, 2);
            ConfigPort(_outputPortRight, 3);
            ConfigPort(_outputPortBackward, 4);

            inputContainer.Add(_inputPortBottom);
            outputContainer.Add(_outputPortTop);
            outputContainer.Add(_outputPortLeft);
            outputContainer.Add(_outputPortForward);
            outputContainer.Add(_outputPortRight);
            outputContainer.Add(_outputPortBackward);

            extensionContainer.Add(CreateOptions());

            RefreshExpandedState();
        }
        public override void SetSize(float width, float heigth)
        {
            _sizeWidthField.value = width;
            _sizeHeightField.value = heigth;
        }

        public override void SetEdgeGeom(float bevelX, float bevelZ, float midThickness)
        {

        }

        public override void SetCurves(float curveX, float tipX)
        {

        }

        protected VisualElement CreateOptions()
        {
            VisualElement optionsBlock = new VisualElement();

            VisualElement shapeGroup = new VisualElement();
            shapeGroup.AddToClassList("group");
            TextElement shapeHeader = new TextElement()
            {
                text = "Shape"
            };
            shapeHeader.AddToClassList("header");

            VisualElement sizeBlock = new VisualElement();
            sizeBlock.AddToClassList("label-field-block");
            sizeBlock.Add(_sizeWidthField);
            sizeBlock.Add(_sizeHeightField);
            sizeBlock.Add(_sizeDepthField);

            shapeGroup.Add(shapeHeader);
            shapeGroup.Add(sizeBlock);

            VisualElement meshDetailsGroup = new VisualElement();
            meshDetailsGroup.AddToClassList("group");
            TextElement meshDetailsHeader = new TextElement()
            {
                text = "Mesh Details"
            };
            meshDetailsHeader.AddToClassList("header");

            VisualElement sMeshIndexBlock = new VisualElement();
            sMeshIndexBlock.AddToClassList("label-field-block");
            sMeshIndexBlock.Add(new Label("Submesh Index:"));
            sMeshIndexBlock.Add(_subMeshIndexField);

            meshDetailsGroup.Add(meshDetailsHeader);
            meshDetailsGroup.Add(sMeshIndexBlock);

            optionsBlock.Add(shapeGroup);
            optionsBlock.Add(meshDetailsGroup);
            return optionsBlock;
        }

    }
}