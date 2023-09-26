using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SSL.Graph.Utils
{
    public static class ExtensionMethods
    {
        public static void SetEnabledFull(this VisualElement ve, bool enabled)
        {
            ve.SetEnabled(enabled);
            ve.focusable = enabled;
        }
    }
}