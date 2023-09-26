using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SSL.Graph {
    public struct GAPortData
    {
        private string _name;
        public string Name
        {
            get
            {
                if(_name == string.Empty)
                {
                    return PortType.ToString();
                }
                return $"{_name} ({PortType})";
            }
            set
            {
                _name = value;
            }
        }
        public GAPortType PortType { get; set; }
    }
}