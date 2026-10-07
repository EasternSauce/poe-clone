using UnityEngine;

namespace PoeClone.Inventory
{
    /// <summary>Loads authored positions from a resource included in every game build.</summary>
    internal static class PassiveTreeLayout
    {
        internal static void Apply()
        {
            TextAsset asset = Resources.Load<TextAsset>(PassiveTreePositions.ResourceName);
            if (asset == null)
                throw new System.InvalidOperationException("Missing passive tree positions resource.");
            PassiveTreePositions layout = PassiveTreePositions.Parse(asset.text);
            layout.Validate(PassiveTree.Nodes);
            foreach (PassiveTreePositions.Entry entry in layout.nodes)
            {
                PassiveNode node = PassiveTree.Get(entry.id);
                node.X = entry.x;
                node.Y = entry.y;
            }
        }
    }
}
