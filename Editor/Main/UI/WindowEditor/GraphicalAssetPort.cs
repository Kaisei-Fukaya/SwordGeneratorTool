using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using SSL.Graph.Utils;
using System;

namespace SSL.Graph
{
    public class GraphicalAssetPort : VisualElement
    {
        GraphViewNode _parentNode;
        public GAPortType PortType { get { return _portType; } }
        GAPortType _portType;
        UnityEditor.Experimental.GraphView.Orientation _orientation;
        Direction _direction;
        Port.Capacity _capacity;
        Port _genPort;
        Color _genPortColor, _genPortDisabledColor;
        string _customPortName;
        Label _portLabel;
        public string PortName 
        { 
            get
            {
                if (_customPortName == string.Empty)
                {
                    return PortType.ToString();
                }
                return $"{_customPortName} ({PortType})";
            }
            set
            {
                _customPortName = value;
                _portLabel.text = value;
            }
        }
        public GraphicalAssetPort(GraphViewNode parent, GAPortType portType, UnityEditor.Experimental.GraphView.Orientation orientation, Direction direction, Port.Capacity capacity, string customPortName = "")
        {
            _parentNode = parent;
            _portType = portType;
            _orientation = orientation;
            _direction = direction;
            _capacity = capacity;
            _genPortColor = new Color(51f / 255f, 141f / 255f, 78f / 255f);
            _genPortDisabledColor = new Color(51f / 255f, 107f / 255f, 68f / 255f);
            _customPortName = customPortName;
            Draw();
        }

        public void Draw()
        {
            if (_parentNode == null || _parentNode.GraphView == null)
                return;
            VisualElement portContainer = new VisualElement();
            portContainer.AddToClassList("port-container");

            GAPortData trainPortData = new GAPortData()
            {
                Name = _customPortName,
                PortType = _portType
            };

            GAPortData genPortData = new GAPortData()
            {
                Name = _customPortName,
                PortType = _portType
            };

            _genPort = _parentNode.InstantiatePort(_orientation, _direction, _capacity, typeof(bool));
            _genPort.userData = genPortData;
            _genPort.AddToClassList("gen-port");

            _genPort.style.paddingLeft = 0;
            _genPort.style.paddingRight = 0;

            _portLabel = new Label()
            {
                text = trainPortData.Name
            };
            _portLabel.AddToClassList("port-label");

            _genPort.portName = genPortData.Name;

            portContainer.Add(_genPort);

            VisualElement icon = new VisualElement();
            if(_direction == Direction.Input)
                portContainer.Add(icon);
            else
                portContainer.Insert(0, icon);


            //Different order depending on direction
            if (_direction == Direction.Input)
                contentContainer.Add(portContainer);

            contentContainer.Add(_portLabel);

            if (_direction == Direction.Output)
                contentContainer.Add(portContainer);

        }

        public Port GetPort()
        {
            return _genPort;
        }

        public void DisconnectAll()
        {
            if(_genPort != null)
                _genPort.DisconnectAll();
        }

        public IEnumerable<Edge> Connections()
        {
            if (_genPort == null)
                return default;
            return _genPort.connections;
        }

        public Edge GetConnectedEdge()
        {
            if (_genPort == null)
                return null;
            return _genPort.connections.SingleOrDefault();
        }

        public Edge ConnectTo(Port other)
        {
            return _genPort.ConnectTo(other);
        }
    }
}