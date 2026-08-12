using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Merchandise", menuName = "Data/Merchandise")]
public class Merchandise : ScriptableObject
{
    public Item item;
    public int buyPrice = -1;       // 구매 불가능한 품목의 경우 -1
    public int sellPrice = -1;      // 판매 불가능한 품목의 경우 -1
    public int scrapValue = -1;     // 분해 불가능한 품목의 경우 -1
}
