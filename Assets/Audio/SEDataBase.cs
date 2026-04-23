using UnityEngine;
using System.Collections.Generic;
using System.Xml.Serialization;

[CreateAssetMenu(fileName = "SEDataBase", menuName = "Audio/SEDataBase")]
public class SEDataBase : ScriptableObject
{
    private List<SEData> SeDataList;

    public void Init()
    {

    }

    public int GetSEData(string seName)
    {
        for (int i = 0; i < SeDataList.Count; i++)
        {
            if (SeDataList[i].SeName == seName)
            {
                return i;
            }
        }
        return -1;
    }

    public SEData GetSEData(int index)
    {
        if (index < 0 || index >= SeDataList.Count)
        {
            Debug.LogError($"SEData index {index} is out of range.");
            return null;
        }
        return SeDataList[index];
    }
}