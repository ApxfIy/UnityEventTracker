using System;
using UnityEngine;

namespace UnityEventTracker.Utils
{
    public enum ColorType
    {
        Background,
        Content,
        All
    }
    
    public class ColorScope : IDisposable
    {
        private readonly Color[] _oldColors = new Color[3];

        public ColorScope(Color color, ColorType colorType)
        {
            _oldColors[0] = GUI.backgroundColor;
            _oldColors[1] = GUI.contentColor;
            _oldColors[2] = GUI.color;

            switch (colorType)
            {
                case ColorType.Background:
                    GUI.backgroundColor = color;
                    break;
                case ColorType.Content:
                    GUI.contentColor = color;
                    break;
                case ColorType.All:
                    GUI.color = color;
                    break;
            }
        }
        
        public static Color ColorOrDefault(Func<bool> predicate, Color color, ColorType colorType)
        {
            if (predicate())
            {
                return color;
            }

            return colorType switch
            {
                ColorType.Background => GUI.backgroundColor,
                ColorType.Content    => GUI.contentColor,
                _                    => GUI.color
            };
        }

        public void Dispose()
        {
            GUI.backgroundColor = _oldColors[0];
            GUI.contentColor = _oldColors[1];
            GUI.color = _oldColors[2];
        }
    }
}
