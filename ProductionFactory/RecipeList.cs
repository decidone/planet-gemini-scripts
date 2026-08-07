using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class RecipeList : MonoBehaviour
{
    Dictionary<string, List<Recipe>> recipeDic;
    List<Recipe> recipes;
    // 레시피 양식 확정되면 json으로 만들어서 저장/관리하고 여기서 불러와서 사용

    #region Singleton
    public static RecipeList instance;

    void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);

        instance = this;
        recipeDic = new Dictionary<string, List<Recipe>>();

        string json = Resources.Load<TextAsset>("Recipe").ToString();
        recipeDic = JsonConvert.DeserializeObject<Dictionary<string, List<Recipe>>>(json);
    }
    #endregion

    public List<Recipe> GetRecipeInven(string str)
    {
        recipes = recipeDic[str];
        return recipes;
    }

    public Recipe GetRecipeIndex(string str, int index)
    {
        if (recipeDic.ContainsKey(str))
        {
            List<Recipe> recipes = recipeDic[str];
            return recipes[index];
        }
        return null;
    }
}
