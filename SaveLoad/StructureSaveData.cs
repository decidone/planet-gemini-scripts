using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StructureSaveData
{
    /*
     * 건물 인덱스 번호, 좌표값, 방향, 체력, 건물 레벨, 배터리 상태, 액체 저장량, 행성
     * 
     * 건물 인벤토리, 레시피 식별 값 json에 추가, 스플리터 언로더 등 필터 값(아이템 인덱 + bool), 라인 연결하는 건물인 경우 연결된 건물의 좌표값
     * 
     * 레시피 없는 채굴기같은 경우는 아이템 인덱스
     */

    public int index;               // 건물 인덱스 번호
    public bool sideObj;            // 지하벨트 처럼 외형이 변경되야 하는 경우
    public SerializedVector3 pos = new SerializedVector3(); // 좌표값
    public float hp;                // 현재 hp
    public bool planet;             // 건물이 지어진 행성 true: 호스트, false: 클라이언트

    public int level = 0;           // 건물 레벨
    public int direction = 0;       // 방향 - 0: 위, 1: 오른쪽, 2: 아래, 3: 왼쪽, 방향 상관 없는 건물: 0
    public InventorySaveData inven = new InventorySaveData();   // 건물 인벤토리
    public List<int> itemIndex = new List<int>();   // 물류 건물 아이템 저장

    public float storedEnergy = 0;  // 배터리 저장량
    public int energyBulletAmount = 0;  // 에너지 타워 탄약 수

    public int fluidType = -1;      // -1: 기본, 0: 물, 1: 석유
    public float storedFluid = 0;   // 액체 저장량

    public int recipeId = -1;       // 기본 -1, 레시피 식별자
    public float prodTimer = 0f;    // 생산 게이지
    public int fuel = 0;

    public int prodItemIndex = -1;  // 채굴기 - 생산 가능한 아이템 인덱스

    public int maxBuyAmount;        // 자동 구매기 최대 구매 수량
    public int sendingOption;        // 자동 구매기 구매 간격, 트랜스포터 전송량

    public List<SerializedVector3> connectedStrPos = new List<SerializedVector3>(); // 트랜스포터, 장거리 커넥터 등 라인 연결해야 하는 건물 좌표값

    public List<SerializedVector3> trUnitPosData = new List<SerializedVector3>(); // 트랜스포터 유닛 위치 정보
    public Dictionary<int, Dictionary<int, int>> trUnitItemData = new Dictionary<int, Dictionary<int, int>>(); // 트랜스포터 유닛 인덱스, 아이템+개수 정보

    // 필터 - 스플리터 같은 경우 3개의 필터 조건이 들어갈 수 있음
    public List<FilterSaveData> filters = new List<FilterSaveData>();

    public bool isAuto = false;     // 분쇄기 자동화 체크, 트랜스포터 최소 전송량 체크

    public string portalName;

    public bool isPreBuilding;
    public bool destroyStart;
    public float repairGauge;
    public float destroyTimer;

    public List<(int, float)> sendUnderBeltItems = new List<(int, float)>(); // 언더벨트로 보내는 아이템 인덱스와 남은 시간
}
