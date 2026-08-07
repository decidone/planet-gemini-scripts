using System.Collections.Generic;
using UnityEngine;

public class ShaderAnimSelector : MonoBehaviour
{
    public List<ShaderAnimData> beltsLv1 = new();
    public List<ShaderAnimData> beltsLv2 = new();
    public List<ShaderAnimData> beltsLv3 = new();

    #region Singleton
    public static ShaderAnimSelector instance;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }
    #endregion

    public ShaderAnimData GetBeltAnimData(int level, int dirNum, int modelMotion)
    {
        int dir = 6 * dirNum + modelMotion;

        if (level == 0)
        {
            return beltsLv1[dir];
        }
        else if (level == 1)
        {
            return beltsLv2[dir];
        }
        else if (level == 2)
        {
            return beltsLv3[dir];
        }

        return beltsLv1[0];
    }
}
