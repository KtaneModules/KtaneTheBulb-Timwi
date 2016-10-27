using System;
using System.Linq;
using TheLamp;
using UnityEngine;

using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of The Lamp
/// Created by Timwi
/// </summary>
public class TheLampModule : MonoBehaviour
{
    public KMBombModule Module;
    public KMAudio Audio;

    public Light Light1;
    public Light Light2;
    //public Material Material;
    public MeshRenderer Bulb;

    private Color[] _lampColors = newArray(
        "3A9DFF",   // blue
        "FF1E00",   // red
        "2EFD2F",   // green
        "EAE11F",   // yellow
        "D2D2D2",   // white
        "F21DFF"    // purple
    )
        .Where(s => s != null)
        .Select(c => new Color(Convert.ToInt32(c.Substring(0, 2), 16) / 255f, Convert.ToInt32(c.Substring(2, 2), 16) / 255f, Convert.ToInt32(c.Substring(4, 2), 16) / 255f))
        .ToArray();

    void Start()
    {
        Module.OnActivate += ActivateModule;
    }

    void ActivateModule()
    {
        Debug.Log("[TheLamp] Activated");

        var colorIndex = Rnd.Range(0, _lampColors.Length);
        var transparent = Rnd.Range(0, 2) == 0;
        var color = _lampColors[colorIndex].WithAlpha(transparent ? .625f : 1f);
        Light1.color = color;
        Light2.color = color;
        Bulb.material.color = color;
    }

    private static T[] newArray<T>(params T[] array) { return array; }
}
