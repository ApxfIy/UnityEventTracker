using UnityEngine;
using UnityEditor;
using UnityEventTracker.Utils;

namespace UnityEventTracker.DataClasses
{
    // Source - https://github.com/NCEEGEE/PrettyHierarchy/blob/main/Editor/HierarchyItem.cs
    internal class HierarchyItem
    {
        public int        InstanceId             { get; }
        public bool       IsSelected             { get; }
        public bool       IsHovered              { get; }
        public GameObject GameObject             { get; }
        public Rect       BackgroundRect         { get; private set; }
        public Rect       TextRect               { get; private set; }
        public Rect       CollapseToggleIconRect { get; private set; }
        public Rect       PrefabIconRect         { get; private set; }
        public Rect       EditPrefabIconRect     { get; private set; }

        public Color BackgroundColor { get; set; }

        public HierarchyItem(int instanceId, Rect selectionRect, GameObject gameObject)
        {
            InstanceId = instanceId;
            IsSelected = Selection.Contains(instanceId);
            GameObject = gameObject;

            CalculateRects(selectionRect);

            IsHovered = BackgroundRect.Contains(Event.current.mousePosition);

            BackgroundColor = EditorColors.Background;
        }

        public void UpdateRect(Rect rect)
        {
            CalculateRects(rect);
        }

        public void Draw()
        {
            EditorGUI.DrawRect(PrefabIconRect, BackgroundColor);
        }

        private void CalculateRects(Rect selectionRect)
        {
            var xPos  = selectionRect.position.x + 60f - 28f - selectionRect.xMin;
            var yPos  = selectionRect.position.y;
            var xSize = selectionRect.size.x + selectionRect.xMin + 28f - 60 + 16f;
            var ySize = selectionRect.size.y;
            BackgroundRect = new Rect(xPos, yPos, xSize, ySize);

            xPos     = selectionRect.position.x + 18f;
            yPos     = selectionRect.position.y;
            xSize    = selectionRect.size.x - 18f;
            ySize    = selectionRect.size.y;
            TextRect = new Rect(xPos, yPos, xSize, ySize);

            xPos                   = selectionRect.position.x - 14f;
            yPos                   = selectionRect.position.y + 1f;
            xSize                  = 13f;
            ySize                  = 13f;
            CollapseToggleIconRect = new Rect(xPos, yPos, xSize, ySize);

            xPos           = selectionRect.position.x;
            yPos           = selectionRect.position.y;
            xSize          = 16f;
            ySize          = 16f;
            PrefabIconRect = new Rect(xPos, yPos, xSize, ySize);

            xPos               = BackgroundRect.xMax - 16f;
            yPos               = selectionRect.yMin;
            xSize              = 16f;
            ySize              = 16f;
            EditPrefabIconRect = new Rect(xPos, yPos, xSize, ySize);
        }
    }
}