using System.Xml.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.VisualScripting;

public class TitleManager : MonoBehaviour
{
    private CanvasGroup variableCG;
    private CanvasGroup staticCG;

    private enum State
    {
        Music,
        InGame,
    }
}