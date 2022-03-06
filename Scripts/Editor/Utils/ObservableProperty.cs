using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[Serializable]
public class ObservableProperty<T> : ObservableProperty
{
    [SerializeField] private T _value;

    public T Value => _value;

    public event Action<T> OnChange;

    /// <summary>
    /// Called from <see cref="ObservablePropertyDrawer"/> trough reflection
    /// </summary>
    private void TriggerOnChange()
    {
        OnChange?.Invoke(Value);
    }

    public ObservableProperty(T value)
    {
        _value = value;
    }

    public static implicit operator ObservableProperty<T>(T value)
    {
        return new ObservableProperty<T>(value);
    }

    public static implicit operator T(ObservableProperty<T> observableProperty)
    {
        return observableProperty.Value;
    }
}

[Serializable]
public class ObservableProperty
{
}

[CustomPropertyDrawer(typeof(ObservableProperty), true)]
public class ObservablePropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var value = property.FindPropertyRelative("_value");
        EditorGUI.PropertyField(position, value, label);
        
        if (!property.serializedObject.ApplyModifiedProperties()) return;

        var instance = fieldInfo.GetValue(property.serializedObject.targetObject);
        fieldInfo.FieldType
            .GetMethod("TriggerOnChange", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(instance, null);
    }
}