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
    public class SegmentNode : GraphViewNode
    {
        protected GraphicalAssetPort _inputPort, _outputPort;
        protected GAPortType _inputPortType, _outputPortType;
        protected IntegerField _nLoopsField, _subMeshIndexField;
        protected FloatField _roundingField;
        protected Slider _relativeForwardTaperField, _relativeBackwardTaperField;
        protected FloatField _relativeForwardTaperFloatField, _relativeBackwardTaperFloatField;
        protected FloatField _sizeWidthField, _sizeHeightField, _sizeDepthField;
        protected Foldout _deformFoldout;
        protected Vector3Field[] _deformFields;
        protected FloatField _midThicknessField, _edgeBevelXField, _edgeBevelZField, _edgeField;
        protected FloatField _tipOffsetXField, _tipOffsetZField;
        protected FloatField _curveOffsetXField, _curveOffsetZField;
        protected bool _curveNotAffectNormal;

        public override void Initialise(Vector2 position)
        {
            NodeType = NodeType.Segment;
            base.Initialise(position);

            //Initialise fields
            _nLoopsField = new IntegerField() { label = "Loop count:" };
            _subMeshIndexField = new IntegerField() { label = "Submesh Index:" };
            _roundingField = new FloatField() { label = "Rounding:" };
            _relativeForwardTaperField = new Slider(0f, 1f);
            _relativeForwardTaperFloatField = new FloatField() { label = "End Taper:" };
            _relativeBackwardTaperField = new Slider(0f, 1f);
            _relativeBackwardTaperFloatField = new FloatField() { label = "Start Taper:" };
            _sizeWidthField = new FloatField()  { label = "Width:" }; 
            _sizeHeightField = new FloatField() { label = "Height:" };
            _sizeDepthField = new FloatField()  { label = "Depth:" };

            _midThicknessField = new FloatField(){ label = "Mid-thickness:", value = 1 };
            _edgeBevelXField = new FloatField()  { label = "Edge-bevel X:", value = 1 };
            _edgeBevelZField = new FloatField()  { label = "Edge-bevel Z:", value = 1 };
            _edgeField = new FloatField()        { label = "Edge:", value = 1 };

            //_deformFoldout = new Foldout() { text = "Deforms" };
            //_deformFields = new Vector3Field[0];
            _tipOffsetXField = new FloatField("Tip offset X:");
            _tipOffsetZField = new FloatField("Tip offset Z:");
            _curveOffsetXField = new FloatField("Curve X:");
            _curveOffsetZField = new FloatField("Curve Z:");

            //Add callbacks
            _nLoopsField.RegisterValueChangedCallback(x => { int v = Mathf.Clamp(x.newValue, 0, 30); _nLoopsField.SetValueWithoutNotify(v); CallSettingsEditEvent(); });
            _subMeshIndexField.RegisterValueChangedCallback(x => { int v = Mathf.Max(0, x.newValue); _subMeshIndexField.SetValueWithoutNotify(v); CallSettingsEditEvent(); });
            _roundingField.RegisterValueChangedCallback(x => { float v = Mathf.Max(0, x.newValue); _roundingField.SetValueWithoutNotify(v); CallSettingsEditEvent(); });

            _relativeForwardTaperField.RegisterValueChangedCallback(x => { _relativeForwardTaperFloatField.SetValueWithoutNotify(x.newValue); CallSettingsEditEvent(); });
            _relativeForwardTaperFloatField.RegisterValueChangedCallback(x => { float v = Mathf.Clamp(x.newValue, 0f, 1f); _relativeForwardTaperFloatField.SetValueWithoutNotify(v); _relativeForwardTaperField.SetValueWithoutNotify(v); CallSettingsEditEvent(); });
            _relativeBackwardTaperField.RegisterValueChangedCallback(x => { _relativeBackwardTaperFloatField.SetValueWithoutNotify(x.newValue); CallSettingsEditEvent(); });
            _relativeBackwardTaperFloatField.RegisterValueChangedCallback(x => { float v = Mathf.Clamp(x.newValue, 0f, 1f); _relativeBackwardTaperFloatField.SetValueWithoutNotify(v); _relativeBackwardTaperField.SetValueWithoutNotify(v); CallSettingsEditEvent(); });

            _sizeWidthField.   RegisterValueChangedCallback(x => { float v = Mathf.Max(0, x.newValue); _sizeWidthField.SetValueWithoutNotify(v); CallSettingsEditEvent(); });
            _sizeHeightField.  RegisterValueChangedCallback(x => { float v = Mathf.Max(0, x.newValue); _sizeHeightField.SetValueWithoutNotify(v); CallSettingsEditEvent(); });
            _sizeDepthField.   RegisterValueChangedCallback(x => { float v = Mathf.Max(0, x.newValue); _sizeDepthField.SetValueWithoutNotify(v); CallSettingsEditEvent(); });
            _midThicknessField.RegisterValueChangedCallback(x => { float v = Mathf.Clamp(x.newValue, 0f, 1f); _midThicknessField.SetValueWithoutNotify(v); CallSettingsEditEvent(); });
            _edgeBevelXField.  RegisterValueChangedCallback(x => { float v = Mathf.Clamp(x.newValue, 0f, 1f); _edgeBevelXField.SetValueWithoutNotify(v); CallSettingsEditEvent(); });
            _edgeBevelZField.  RegisterValueChangedCallback(x => { float v = Mathf.Clamp(x.newValue, 0f, 1f); _edgeBevelZField.SetValueWithoutNotify(v); CallSettingsEditEvent(); });
            _edgeField.        RegisterValueChangedCallback(x => { float v = Mathf.Clamp(x.newValue, 0f, 1f); _edgeField.SetValueWithoutNotify(v); CallSettingsEditEvent(); });
            _tipOffsetXField.  RegisterValueChangedCallback(x => { CallSettingsEditEvent(); });
            _tipOffsetZField.  RegisterValueChangedCallback(x => { CallSettingsEditEvent(); });
            _curveOffsetXField.RegisterValueChangedCallback(x => { CallSettingsEditEvent(); });
            _curveOffsetZField.RegisterValueChangedCallback(x => { CallSettingsEditEvent(); });

            //IO
            _ingoingPorts = new List<GraphicalAssetPort>();
            _outgoingPorts = new List<GraphicalAssetPort>();

            _inputPort = new GraphicalAssetPort(this, _inputPortType, UnityEditor.Experimental.GraphView.Orientation.Horizontal, Direction.Input, Port.Capacity.Single);
            _outputPort = new GraphicalAssetPort(this, _outputPortType, UnityEditor.Experimental.GraphView.Orientation.Horizontal, Direction.Output, Port.Capacity.Single);
            _ingoingPorts.Add(_inputPort);
            _outgoingPorts.Add(_outputPort);

        }

        public override NodeSetting GetSettings()
        {
            NodeSetting setting = base.GetSettings();
            setting.parameters.nLoops = _nLoopsField.value;
            setting.parameters.size = new Vector3(_sizeWidthField.value, _sizeHeightField.value, _sizeDepthField.value);
            setting.parameters.relativeForwardTaper = _relativeForwardTaperField.value;
            setting.parameters.relativeBackwardTaper = _relativeBackwardTaperField.value;
            setting.parameters.rounding = _roundingField.value;
            setting.parameters.subMeshIndex = _subMeshIndexField.value;
            setting.parameters.curveParams.tipOffset = new Vector2(_tipOffsetXField.value, _tipOffsetZField.value);
            setting.parameters.curveParams.controlPoint = new Vector2(_curveOffsetXField.value, _curveOffsetZField.value);
            setting.parameters.curveNotAffectNormal = _curveNotAffectNormal;
            //for (int i = 0; i < setting.parameters.deforms.Length; i++)
            //{
            //    setting.parameters.deforms[i] = _deformFields[i].value;
            //}

            float edgeBevelTrueX =   Mathf.Lerp(-setting.parameters.size.x/2, 0, _edgeBevelXField.value);
            float edgeBevelTrueZ =   Mathf.Lerp(-setting.parameters.size.z/2, 0, _edgeBevelZField.value);
            float edgeTrue =         Mathf.Lerp(-setting.parameters.size.x/2, 0, _edgeField.value);
            float midThicknessTrue = Mathf.Lerp(-setting.parameters.size.z/2, 0, _midThicknessField.value);

            setting.parameters.deforms[0] = new Vector3(-edgeBevelTrueX, 0f, -edgeBevelTrueZ);
            setting.parameters.deforms[1] = new Vector3(0f, 0f, -midThicknessTrue);
            setting.parameters.deforms[2] = new Vector3(edgeBevelTrueX, 0f, -edgeBevelTrueZ);
            setting.parameters.deforms[3] = new Vector3(edgeTrue, 0f, 0f);
            setting.parameters.deforms[4] = new Vector3(edgeBevelTrueX, 0f, edgeBevelTrueZ);
            setting.parameters.deforms[5] = new Vector3(0f, 0f, midThicknessTrue);
            setting.parameters.deforms[6] = new Vector3(-edgeBevelTrueX, 0f, edgeBevelTrueZ);
            setting.parameters.deforms[7] = new Vector3(-edgeTrue, 0f, 0f);

            return setting;
        }

        public override void LoadSettings(NodeSetting setting)
        {
            _nLoopsField.SetValueWithoutNotify(setting.parameters.nLoops);
            _sizeWidthField.SetValueWithoutNotify(setting.parameters.size.x);
            _sizeHeightField.SetValueWithoutNotify(setting.parameters.size.y);
            _sizeDepthField.SetValueWithoutNotify(setting.parameters.size.z);
            _relativeForwardTaperField.SetValueWithoutNotify(setting.parameters.relativeForwardTaper);
            _relativeForwardTaperFloatField.SetValueWithoutNotify(setting.parameters.relativeForwardTaper);
            _relativeBackwardTaperField.SetValueWithoutNotify(setting.parameters.relativeBackwardTaper);
            _relativeBackwardTaperFloatField.SetValueWithoutNotify(setting.parameters.relativeBackwardTaper);
            _roundingField.SetValueWithoutNotify(setting.parameters.rounding);
            _subMeshIndexField.SetValueWithoutNotify(setting.parameters.subMeshIndex);
            //_deformFields = new Vector3Field[setting.parameters.deforms.Length];
            _tipOffsetXField.SetValueWithoutNotify(setting.parameters.curveParams.tipOffset.x);
            _tipOffsetZField.SetValueWithoutNotify(setting.parameters.curveParams.tipOffset.y);
            _curveOffsetXField.SetValueWithoutNotify(setting.parameters.curveParams.controlPoint.x);
            _curveOffsetZField.SetValueWithoutNotify(setting.parameters.curveParams.controlPoint.y);

            _edgeBevelXField.SetValueWithoutNotify(Mathf.InverseLerp(setting.parameters.size.x / 2, 0f, Mathf.Abs(setting.parameters.deforms[0].x)));
            _edgeBevelZField.SetValueWithoutNotify(Mathf.InverseLerp(setting.parameters.size.z / 2, 0f, Mathf.Abs(setting.parameters.deforms[0].z)));
            _edgeField.SetValueWithoutNotify(Mathf.InverseLerp(setting.parameters.size.x / 2, 0f, Mathf.Abs(setting.parameters.deforms[3].x)));
            _midThicknessField.SetValueWithoutNotify(Mathf.InverseLerp(setting.parameters.size.z / 2, 0f, Mathf.Abs(setting.parameters.deforms[1].z)));

            //Debug.Log($"edgebevelx: {_edgeBevelXField.value}, deform0: {setting.parameters.deforms[0].x}, sizexdiv2: {setting.parameters.size.x/2}");

            //for (int i = 0; i < _deformFields.Length; i++)
            //{
            //    _deformFields[i] = new Vector3Field();
            //    _deformFields[i].SetValueWithoutNotify(setting.parameters.deforms[i]);
            //    _deformFields[i].RegisterValueChangedCallback(x => CallSettingsEditEvent());
            //    _deformFoldout.Add(_deformFields[i]);
            //}

            _curveNotAffectNormal = setting.parameters.curveNotAffectNormal;
        }

        public override void Draw()
        {
            base.Draw();

            ConfigPort(_inputPort, 5);
            ConfigPort(_outputPort, 0);

            inputContainer.Add(_inputPort);
            outputContainer.Add(_outputPort);

            extensionContainer.Add(CreateOptions());

            RefreshExpandedState();
        }

        //public void SetRandomValue(int valueGroupIndex)
        //{
        //    switch (valueGroupIndex)
        //    {
        //        default:
        //            _sizeWidthField.value = Random.value * 15f;
        //            _sizeHeightField.value = Random.value * 15f;
        //            break;
        //        case 1:
        //            _edgeBevelXField.value = Random.value;
        //            _edgeBevelZField.value = Random.value;
        //            //_edgeField.value = Random.value;
        //            _midThicknessField.value = Random.value;
        //            break;
        //        case 2:
        //            _curveOffsetXField.value = (Random.value - .5f) * 5f;
        //            _tipOffsetXField.value = (Random.value - .5f) * 5f;
        //            break;
        //        case 3:
        //            break;
        //    }
        //}

        public override void SetSize(float width, float heigth)
        {
            _sizeWidthField.value = width;
            _sizeHeightField.value = heigth;
        }

        public override void SetEdgeGeom(float bevelX, float bevelZ, float midThickness)
        {
            _edgeBevelXField.value = bevelX;
            _edgeBevelZField.value = bevelZ;
            _midThicknessField.value = midThickness;
        }

        public override void SetCurves(float curveX, float tipX)
        {
            _curveOffsetXField.value = curveX;
            _tipOffsetXField.value = tipX;
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

            VisualElement leftBlock = new VisualElement();
            leftBlock.AddToClassList("label-field-block");
            leftBlock.Add(_sizeWidthField);
            leftBlock.Add(_sizeHeightField);
            leftBlock.Add(_sizeDepthField);
            leftBlock.Add(_tipOffsetXField);
            leftBlock.Add(_curveOffsetXField);



            VisualElement roundingBlock = new VisualElement();
            roundingBlock.AddToClassList("label-field-block");
            roundingBlock.Add(_roundingField);

            VisualElement forwardTaperBlock = new VisualElement();
            forwardTaperBlock.AddToClassList("slider-group");
            forwardTaperBlock.Add(_relativeForwardTaperFloatField);
            forwardTaperBlock.Add(_relativeForwardTaperField);

            VisualElement backwardTaperBlock = new VisualElement();
            backwardTaperBlock.AddToClassList("slider-group");
            backwardTaperBlock.Add(_relativeBackwardTaperFloatField);
            backwardTaperBlock.Add(_relativeBackwardTaperField);
            
            VisualElement rightBlock = new VisualElement();
            rightBlock.Add(roundingBlock);
            rightBlock.Add(forwardTaperBlock);
            rightBlock.Add(backwardTaperBlock);
            rightBlock.Add(_tipOffsetZField);
            rightBlock.Add(_curveOffsetZField);

            VisualElement shapeContentContainer = new VisualElement();
            shapeContentContainer.AddToClassList("twoColBlock");
            shapeContentContainer.Add(leftBlock);
            shapeContentContainer.Add(rightBlock);

            shapeGroup.Add(shapeHeader);
            shapeGroup.Add(shapeContentContainer);

            VisualElement meshDetailsGroup = new VisualElement();
            meshDetailsGroup.AddToClassList("group");
            TextElement meshDetailsHeader = new TextElement()
            {
                text = "Mesh Details"
            };
            meshDetailsHeader.AddToClassList("header");

            VisualElement nLoopsBlock = new VisualElement();
            nLoopsBlock.AddToClassList("label-field-block");
            nLoopsBlock.Add(_nLoopsField);

            VisualElement sMeshIndexBlock = new VisualElement();
            sMeshIndexBlock.AddToClassList("label-field-block");
            sMeshIndexBlock.Add(_subMeshIndexField);

            meshDetailsGroup.Add(meshDetailsHeader);
            meshDetailsGroup.Add(nLoopsBlock);
            meshDetailsGroup.Add(sMeshIndexBlock);

            VisualElement refinementGroup = new VisualElement();
            refinementGroup.AddToClassList("group");
            TextElement refinementHeader = new TextElement()
            {
                text = "Mesh Refinement"
            };
            refinementHeader.AddToClassList("header");

            VisualElement refinementContentContainer = new VisualElement();
            refinementContentContainer.AddToClassList("twoColBlock");


            VisualElement refinementLeft = new VisualElement();
            refinementLeft.AddToClassList("label-field-block");

            VisualElement refinementRight = new VisualElement();
            refinementRight.AddToClassList("label-field-block");

            refinementLeft.Add(_midThicknessField);
            refinementLeft.Add(_edgeBevelXField);

            refinementRight.Add(_edgeField);
            refinementRight.Add(_edgeBevelZField);

            refinementGroup.Add(refinementHeader);
            refinementContentContainer.Add(refinementLeft);
            refinementContentContainer.Add(refinementRight);
            refinementGroup.Add(refinementContentContainer);


            optionsBlock.Add(shapeGroup);
            optionsBlock.Add(meshDetailsGroup);
            optionsBlock.Add(refinementGroup);
            //optionsBlock.Add(_deformFoldout);
            //_deformFoldout.value = false;
            return optionsBlock;
        }

    }
}