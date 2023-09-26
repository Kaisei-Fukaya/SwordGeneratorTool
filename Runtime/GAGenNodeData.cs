using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using SSL.Graph;

namespace SSL.Data
{
    [Serializable]
    public class GAGenNodeData
    {
        [field: SerializeField] public string ID { get; set; }
        [field: SerializeField] public NodeType NodeType { get; set; }
        [field: SerializeField] public List<ConnectionData> OutGoingConnections { get; set; }
        [field: SerializeField] public List<ConnectionData> InGoingConnections { get; set; }
        [field: SerializeField] public NodeSetting Settings { get; set; }
        [field: SerializeField] public Vector2 Position { get; set; }
    }

    [Serializable]
    public struct ConnectionData
    {
        public string iD;
        public int indexInOwner, indexInOther;
        public string owner;
        public GAPortType type;
        public string ownerDisplayName;

        public ConnectionData(string iD, int indexInOther, int indexInOwner, string owner, GAPortType type, string displayName = "")
        {
            this.iD = iD;
            this.indexInOwner = indexInOwner;
            this.indexInOther = indexInOther;
            this.owner = owner;
            this.type = type;
            ownerDisplayName = displayName;
        }
    }

    [Serializable]
    public class NodeSetting
    {
        //Number of inputs
        public List<GAPortType> i_portTypes = new List<GAPortType>();
        //Output settings
        public List<GAPortType> o_portTypes = new List<GAPortType>();

        public NodeParams parameters = NodeParams.defaultParams;
    }
}

namespace SSL.Graph
{
    public enum NodeType
    {
        Segment,
        Branch
    }

    public enum GAPortType
    {
        Mesh
    }

    public enum Orientation
    {
        Up,
        Left,
        Forward,
        Right,
        Backward,
        Down
    }

    public struct OrientationMap
    {
        Orientation Up;
        Orientation Left;
        Orientation Forward;
        Orientation Right;
        Orientation Backward;
        Orientation Down;

        public static OrientationMap GetDefault()
        {
            return new OrientationMap()
            {
                Up = Orientation.Up,
                Left = Orientation.Left,
                Forward = Orientation.Forward,
                Right = Orientation.Right,
                Backward = Orientation.Backward,
                Down = Orientation.Down
            };
        }

        public Orientation this[int index]
        {
            get
            {
                switch (index)
                {
                    default:
                        return Up;
                    case 1:
                        return Left;
                    case 2:
                        return Forward;
                    case 3:
                        return Right;
                    case 4:
                        return Backward;
                    case 5:
                        return Down;
                }
            }
        }

        public OrientationMap TransformLeft()
        {
            OrientationMap result = new OrientationMap();
            result.Up = Left;
            result.Left = Backward;
            result.Forward = Down;
            result.Right = Forward;
            result.Backward = Up;
            result.Down = Right;
            return result;
        }

        public OrientationMap TransformForward()
        {
            OrientationMap result = new OrientationMap();
            result.Up = Forward;
            result.Left = Left;
            result.Forward = Down;
            result.Right = Right;
            result.Backward = Up;
            result.Down = Backward;
            return result;
        }

        public OrientationMap TransformRight()
        {
            OrientationMap result = new OrientationMap();
            result.Up = Right;
            result.Left = Forward;
            result.Forward = Down;
            result.Right = Backward;
            result.Backward = Up;
            result.Down = Left;
            return result;
        }

        public OrientationMap TransformBackward()
        {
            OrientationMap result = new OrientationMap();
            result.Up = Backward;
            result.Left = Right;
            result.Forward = Down;
            result.Right = Left;
            result.Backward = Up;
            result.Down = Forward;
            return result;
        }
    }
}