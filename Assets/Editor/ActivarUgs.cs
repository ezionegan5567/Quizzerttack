// LAB09: agrega el define UGS_ON automáticamente (se ejecuta al abrir/recompilar Unity).
using UnityEditor;
using UnityEngine;
using UnityEditor.Build;

[InitializeOnLoad]
public static class ActivarUgs
{
    static ActivarUgs()
    {
        var target = NamedBuildTarget.Standalone;
        string defines = PlayerSettings.GetScriptingDefineSymbols(target);
        if (!defines.Contains("UGS_ON"))
        {
            string nuevo = string.IsNullOrEmpty(defines) ? "UGS_ON" : defines + ";UGS_ON";
            PlayerSettings.SetScriptingDefineSymbols(target, nuevo);
            Debug.Log("[LAB09] Define UGS_ON añadido automáticamente. Recompilando...");
        }
    }
}
