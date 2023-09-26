using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System;
using UnityEditor.UIElements;
using SSL.Data.Utils;

public class MaterialsPopupWindow : EditorWindow
{
    VisualElement _listBlock;
    List<MaterialElement> _materialElements = new List<MaterialElement>();
    List<(int, Material)> _materials;
    Action<List<(int, Material)>> _updateAction;
    public static void ShowWindow()
    {
        GetWindow<MaterialsPopupWindow>("Material Editor");
    }

    private void OnChange()
    {
        _materials = new List<(int, Material)>();
        for (int i = 0; i < _materialElements.Count; i++)
        {
            _materials.Add((_materialElements[i].ToTuple()));
        }
        _updateAction(_materials);
    }

    public void CreateGUI()
    {
        rootVisualElement.styleSheets.Add((StyleSheet)AssetDatabase.LoadAssetAtPath($"{GAGenDataUtils.BasePath}Editor/Assets/UIStyles/MaterialsPopupStyle.uss", typeof(StyleSheet)));
        _listBlock = new VisualElement();
        _listBlock.name = "listBlock";
        Button addButton = new Button();
        addButton.text = "Add New Material";
        addButton.RegisterValueChangedCallback(x => OnChange());
        addButton.clicked += () => AddNewMaterialElement();
        rootVisualElement.Add(_listBlock);
        rootVisualElement.Add(addButton);
    }

    public void Initialise(List<(int, Material)> materials, Action<List<(int, Material)>> updateAction)
    {
        _materials = materials;
        _updateAction = updateAction;
        for (int i = 0; i < _materials.Count; i++)
        {
            AddNewMaterialElement(_materials[i].Item1, _materials[i].Item2, true);
        }
    }

    void RemoveMaterialElement(MaterialElement element)
    {
        _listBlock.Remove(element);
        _materialElements.Remove(element);
        _materials.Remove(element.ToTuple());
    }

    void AddNewMaterialElement(int submeshindex = 0, Material mat = null, bool suppressOnChange = false)
    {
        MaterialElement newElement = new MaterialElement(OnChange, RemoveMaterialElement, _listBlock, submeshindex, mat);

        _materialElements.Add(newElement);
        _listBlock.Add(newElement);
        if(!suppressOnChange)
            OnChange();
    }

    class MaterialElement : VisualElement
    {
        public IntegerField subindexField;
        public ObjectField materialField;
        public MaterialElement(Action OnChange, Action<MaterialElement> Remove, VisualElement listBlock, int submeshindex, Material mat)
        {
            subindexField = new IntegerField();
            subindexField.label = "Submesh Index";
            subindexField.SetValueWithoutNotify(submeshindex);
            subindexField.RegisterValueChangedCallback(x => { int v = Mathf.Max(x.newValue, 0); subindexField.SetValueWithoutNotify(v); OnChange(); });
            materialField = new ObjectField();
            materialField.objectType = typeof(Material);
            materialField.SetValueWithoutNotify(mat);
            materialField.RegisterValueChangedCallback(x => OnChange());
            Button deleteElementButton = new Button();
            deleteElementButton.text = "X";
            deleteElementButton.clicked += OnChange;
            deleteElementButton.clicked += () => Remove(this);
            contentContainer.Add(subindexField);
            contentContainer.Add(materialField);
            contentContainer.Add(deleteElementButton);
        }

        public (int, Material) ToTuple()
        {
            return (subindexField.value, (Material)materialField.value);
        }
    }
}
