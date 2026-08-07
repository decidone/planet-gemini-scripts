using UnityEngine;
using UnityEngine.Pool;

public class ItemProps : MonoBehaviour
{
    public IObjectPool<GameObject> itemPool { get; set; }

    public Item item;
    public int amount;
    public bool waitingForDestroy = false;
    [HideInInspector]
    public bool isOnBelt = false;
    public BeltCtrl setOnBelt;
    public int beltGroupIndex;
    public SpriteRenderer spriteRenderer;

    public double beltEnterTime;   // ServerTime 기준 벨트 진입 시각
    public Vector3 beltStartPos;   // 진입 시점의 시작 위치
    public Vector3 beltEndPos;     // 목표 위치 (nextPos[0])
    public double beltTravelDuration; // 이동에 걸리는 시간(초)
    public Interactable interactable;

    int canNotFoundCount = 0; // 아이템을 찾을 수 없는 경우의 누적 횟수

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        TryGetComponent(out interactable);
    }

    public void ResetItemProps(bool isInHostmap, int destroyRequestedBy)
    {
        if (GameManager.instance.isHost)
        {
            Inventory planetInven;

            if (isInHostmap)
                planetInven = GameManager.instance.hostMapInven;
            else
                planetInven = GameManager.instance.clientMapInven;

            if (planetInven.SpaceCheck(item) >= amount)
            {
                int itemIndex = GeminiNetworkManager.instance.GetItemSOIndex(item);
                planetInven.DisplayDestroyLootInfoClientRpc(itemIndex, amount, destroyRequestedBy);
                planetInven.Add(item, amount);
            }
            else
            {
                int itemIndex = GeminiNetworkManager.instance.GetItemSOIndex(item);
                GeminiNetworkManager.instance.ItemSpawnServerRpc(itemIndex, amount, transform.position);
            }
        }

        waitingForDestroy = false;
        if (itemPool != null && gameObject) itemPool.Release(gameObject);
    }

    public void ClientResetItemProps()
    {
        if(itemPool != null && gameObject)
        {
            item = null;
            amount = 0;
            waitingForDestroy = false;
            isOnBelt = false;
            setOnBelt = null;
            beltGroupIndex = -1;
            spriteRenderer.sprite = null;
            beltEnterTime = 0;
            beltStartPos = Vector2.zero;
            beltEndPos = Vector2.zero;
            beltTravelDuration = 0;
            canNotFoundCount = 0;
            itemPool.Release(gameObject);
        }
    }

    public void SetBeltData(double enterTime, Vector3 startPos, Vector3 endPos, float speed)
    {
        beltEnterTime = enterTime;
        beltStartPos = startPos;
        beltEndPos = endPos;
        float dist = Vector3.Distance(startPos, endPos);

        if (float.IsNaN(dist) || float.IsNaN(speed) || dist < 0.0001f || speed <= 0f)
            beltTravelDuration = 0;
        else
            beltTravelDuration = dist / speed;
    }

    public bool FindObj()
    {
        bool zombieObj = false;

        canNotFoundCount++;
        if (canNotFoundCount > 3)
        {
            zombieObj = true;
        }

        return zombieObj;
    }
}