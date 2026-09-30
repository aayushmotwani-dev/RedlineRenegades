using UnityEngine;

namespace Redline
{
    [RequireComponent(typeof(Camera))]
    public sealed class PremiumPresentation : MonoBehaviour
    {
        Material grade;

        void Awake()
        {
            Shader shader = Resources.Load<Shader>("RedlinePremiumGrade");
            if (shader != null && shader.isSupported) grade = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        void OnDestroy()
        {
            if (grade != null) Destroy(grade);
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (grade == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            grade.SetFloat("_Contrast", 1.04f);
            grade.SetFloat("_Saturation", 0.98f);
            grade.SetFloat("_Vignette", 0.085f);
            grade.SetFloat("_Lift", 0.022f);
            grade.SetColor("_GradeTint", new Color(1.045f, 1.005f, 0.95f, 1f));
            Graphics.Blit(source, destination, grade);
        }
    }
}
