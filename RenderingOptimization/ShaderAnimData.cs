using UnityEngine;

[CreateAssetMenu(fileName = "AnimData", menuName = "Animation/ShaderAnimData")]
public class ShaderAnimData : ScriptableObject
{
    public Sprite[] frames;

    public float defaultFrameRate = 8f;
    public bool sync = true;

    [Header("자동 계산값")]
    public Texture2D atlas;
    public int frameWidth = 16;
    public int frameHeight = 16;
    public int startFrameX = 0;
    public int startFrameY = 0;
    public int totalFrames = 4;
    public int columns = 4;

    public void AutoCalculateFromSprites()
    {
        if (frames == null || frames.Length == 0) return;

        var first = frames[0];
        atlas = first.texture;
        frameWidth = (int)first.rect.width;
        frameHeight = (int)first.rect.height;
        totalFrames = frames.Length;
        columns = Mathf.RoundToInt(atlas.width / (float)frameWidth);

        startFrameX = Mathf.RoundToInt(first.rect.x / frameWidth);

        int totalRows = Mathf.RoundToInt(atlas.height / (float)frameHeight);
        int rowFromBottom = Mathf.RoundToInt(first.rect.y / frameHeight);
        startFrameY = totalRows - 1 - rowFromBottom;
    }

    public void GetUVData(out float uvWidth, out float uvHeight, out float startU, out float startV)
    {
        uvWidth = (float)frameWidth / atlas.width;
        uvHeight = (float)frameHeight / atlas.height;
        startU = startFrameX * uvWidth;
        startV = startFrameY * uvHeight;
    }
}