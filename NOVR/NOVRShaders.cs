using UnityEngine;

namespace NOVR;

public class NOVRShaders
{
    public static Material UiMaterial { get; private set; }
    
    
    public static void Setup()
    {
        Shader baseShader = Shader.Find("Sprites/Default");

        
        var uiMat = new Material(baseShader)
        {
            name = "Runtime_UI_Material"
        };
        uiMat.SetFloat("_ZWrite", 0);
        uiMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        uiMat.renderQueue = 5000;
        UiMaterial = uiMat;
    }
}