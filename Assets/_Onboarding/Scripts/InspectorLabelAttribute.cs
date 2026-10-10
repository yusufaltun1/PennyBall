using System;
using UnityEngine;

/// <summary>
/// SerializeField Inspector görünen adını değiştirir.
/// Not: Unity'nin InspectorName attribute'u yalnızca enum değerleri içindir.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class InspectorLabelAttribute : PropertyAttribute
{
    public string Label { get; }

    public InspectorLabelAttribute(string label)
    {
        Label = label;
    }
}
