using UnityEngine;

namespace TheBulb
{
    static class Ext
    {
        public static Color WithAlpha(this Color color, float alpha) { return new Color(color.r, color.g, color.b, alpha); }

        public static T[] NewArray<T>(params T[] array) { return array; }
    }
}
