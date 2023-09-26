using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SSL;

[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class SwordCreator : MonoBehaviour
{
    [Range(0, 5)]
    [SerializeField] int _subdiv;
    [SerializeField] float _spacing;
    [SerializeField] SSegment[] _nodes;
    [SerializeField] STerminal _testTerminalNode;
    [SerializeField] List<NestedList> _edges;


    SwordGraph sg;
    MeshFilter mf;

    void Start()
    {
        Gen();
    }

    void Gen()
    {
        if (sg == null)
            sg = new SwordGraph();
        if (mf == null)
            mf = GetComponent<MeshFilter>();

        BuildNodes();

        Dictionary<int, int[]> edgesConverted = new Dictionary<int, int[]>();
        for (int i = 0; i < _edges.Count; i++)
        {
            edgesConverted.Add(i, _edges[i].val.ToArray());
        }

        SElement[] newSet = new SElement[_nodes.Length + 1];
        for (int i = 0; i < _nodes.Length; i++)
        {
            newSet[i] = _nodes[i];
        }
        newSet[newSet.Length-1] = _testTerminalNode;

        sg.Load(_subdiv, _spacing, newSet, edgesConverted, true);
        Mesh m = sg.Generate();
        mf.mesh = m;
    }

    void BuildNodes()
    {
        for (int i = 0; i < _nodes.Length; i++)
        {
            _nodes[i].Build(_subdiv);
        }
        _testTerminalNode.Build(_subdiv);
    }

    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall -= Gen;
        UnityEditor.EditorApplication.delayCall += Gen;
    }

    [System.Serializable]
    public class NestedList
    {
        public List<int> val;
    }
}