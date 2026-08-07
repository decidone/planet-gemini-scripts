# Perlin Noise를 통한 절차적 맵 생성

시드(Seed) 기반의 결정론적 알고리즘으로 전체 지형을 생성합니다. 시드 오프셋을 Perlin Noise에 적용해 바이옴과 자원 광맥 분포를 결정하고, 보정 작업을 거쳐 최종 타일맵에 배치합니다.

호스트와 클라이언트는 서로 다른 바이옴 구조를 가진 전용 행성을 각각 소유합니다. 특정 행성에서만 산출되는 독점 자원이 존재하며, 이는 포탈을 활용한 플레이어 간 교역과 협동을 자연스럽게 유도합니다.

관련 원본: [`MapGenerator.cs`](./MapGenerator.cs) · [`Biome.cs`](./Biome.cs) · [`Cell.cs`](./Cell.cs) · [`Map.cs`](./Map.cs)

> 🎞️ _데모 GIF 예정_


## 구성 요소와 역할

| 구성 요소                 | 역할                                                              |
| --------------------- | --------------------------------------------------------------- |
| `System.Random(seed)` | 시드 기반 난수 생성을 담당합니다. 동일한 시드를 입력하면 매번 동일한 난수 열을 생성하여 맵 상태를 재현합니다. |
| `Mathf.PerlinNoise`   | 시드로 구한 오프셋을 사용해 노이즈 필드를 형성하고, 이를 기반으로 바이옴 및 자원 광맥의 분포를 계산합니다.   |
| `MapGenerator`        | 시드 설정부터 바이옴 배치, 경계 스무딩, 자원 배치, 타일맵 반영까지 전체 맵 생성 파이프라인을 제어합니다.   |
| `Biome`               | 7종 바이옴(평야, 숲, 사막, 눈, 빙결, 호수, 절벽)의 데이터 구조 및 경계 보정 규칙을 정의합니다.     |


## 동작 흐름

```
seed → System.Random(seed)
↓
GenerateMap — 행성마다
SetBiomeTable(isHost)  — 행성별 바이옴 분포 테이블
SetBiome → SmoothBiome → SmoothCliff
↓
CreateResource — 광맥 배치 후 인접 자원을 청크로 묶어 최소 크기 미만 파편은 제거
↓
타일맵에 그리기
```

### 1. 시드 기반 난수 생성

입력받은 시드로 난수 생성기를 초기화하며, 이 난수로 Perlin Noise 오프셋과 자원 위치를 산출하여 매번 동일한 결과물이 나오도록 보장합니다.

```csharp
random = new System.Random(seed);
```

### 2. 행성별 바이옴 차별화

호스트 및 클라이언트 맵을 각각 생성할 때 서로 다른 바이옴 테이블(`SetBiomeTable`)을 할당합니다. 이로 인해 행성마다 고유 자원이 생성되며 포탈을 통한 교역 시스템의 기반이 마련됩니다.

```csharp
// MapGenerator.GenerateMap
SetBiomeTable(true);  SetBiome(hostMap);   … CreateResource(hostMap, true);    // 호스트 행성
SetBiomeTable(false); SetBiome(clientMap); … CreateResource(clientMap, false); // 클라이언트 행성 (다른 분포)
```

### 3. 바이옴 배치

온도 및 고도 값을 나타내는 2개의 Perlin Noise 수치를 2차원 인덱스(열·행)로 환산하여 각 셀의 바이옴을 결정합니다. 이때 샘플링 오프셋(`tempX` 등)을 시드 난수로 생성하여 일관된 지형 패턴을 유지합니다.

```csharp
// MapGenerator.SetBiome — 시드 오프셋으로 Perlin 필드 샘플 (0~1 클램프)
float tempNoise = Mathf.Clamp01(Mathf.PerlinNoise((x - tempX) / magnification, (y - tempY) / magnification));
float scaledTemp = tempNoise * biomes.Count;
if (scaledTemp == biomes.Count) scaledTemp = biomes.Count - 1;   // 경계값(1.0) 인덱스 초과 방지

float heightNoise = Mathf.Clamp01(Mathf.PerlinNoise((x - heightX) / magnification, (y - heightY) / magnification));
float scaledHeight = heightNoise * biomes.Count;
if (scaledHeight == biomes.Count) scaledHeight = biomes.Count - 1;

// 온도·고도 노이즈를 바이옴 테이블의 열·행 인덱스로 → 셀 바이옴 결정
cell.biome = biomes[Mathf.FloorToInt(scaledHeight)][Mathf.FloorToInt(scaledTemp)];
```

### 4. 경계 스무딩 및 자원 광맥 생성

바이옴·절벽의 부자연스러운 경계를 8-이웃 규칙 기반 셀룰러 오토마타 스무딩으로 정돈하며, 더 이상 변경점이 없는 안정 상태에 도달하면 조기 종료합니다. 자원은 종류별 Perlin 필드를 활용해 자연스러운 덩어리 형태(광맥)로 배치합니다.

```csharp
// SmoothBiome / SmoothCliff — 경계 반복 보정 (안정되면 조기 종료)
for (int i = 0; i < MaxSmoothIterations; i++) {
    int exception = /* 바뀐 셀 수 */;
    if (exception == 0) return;
}
// CreateResource — 자원별 Perlin 필드로 광맥 분포 결정
float oreNoise = Mathf.PerlinNoise((x - oreX) / resource.distribution, (y - oreY) / resource.distribution);
if (oreNoise < resource.scale && resource.biome.Contains(biome.biome))
    // 해당 셀에 자원 광맥 배치
```


### 5. 파편 광맥 정리

자원 배치 시 인접한 8방향 타일을 탐색해 동일한 자원이 존재하면 동일한 청크 번호를 할당하고, 없으면 신규 청크를 생성합니다. 모든 배치가 완료된 후 `minimumChunkSize` 미만의 청크는 파편 광맥으로 판단해 삭제합니다.

```csharp
// CreateResource 라벨링 — 근처 8칸에 같은 자원이 있으면 그 청크 번호를 상속
for (int n = 0; n < 9; n++) {
    if (n == 4) continue;
    int nx = x + (n % 3) - 1, ny = y - (n / 3 - 1);
    if (map.IsOnMapData(nx, ny)
        && map.mapData[nx][ny].resource == resource
        && map.mapData[nx][ny].resourceChunkNum >= 0) {
        cell.resourceChunkNum = map.mapData[nx][ny].resourceChunkNum;
        chunkDic[cell.resourceChunkNum]++;
        break;
    }
}
if (cell.resourceChunkNum < 0) {
    // 이웃에 같은 자원 없는 경우 새 청크 생성
    cell.resourceChunkNum = resourceCount;
    chunkDic.Add(resourceCount, 1);
    resourceCount++;
}

// CreateResource 정리 — 배치 완료 후, 크기가 minimumChunkSize 미만인 광물 청크 제거
if (cell.resource.type == "ore" && chunkDic[cell.resourceChunkNum] < minimumChunkSize)
    resourcesTilemap.SetTile(cellPos, null);
```


## 설계 포인트

- **시드 기반 생성:** 단일 시드값을 활용해 결정론적으로 지형을 생성함으로써 언제나 동일한 맵을 재현할 수 있도록 구현했습니다.
- **Perlin Noise 기반 분포 생성:** 시드 오프셋을 반영한 노이즈 필드를 생성해 바이옴 경계가 연속적인 형태를 띠도록 지형 및 자원 광맥 분포를 구현했습니다.
- **행성 간 바이옴 차별화:** 호스트와 클라이언트 맵에 서로 다른 바이옴 테이블을 적용하여 행성별 독점 자원을 배치하고, 포탈을 통한 교역과 협동 플레이를 유도했습니다.
- **광맥 청크 정제:** 인접한 자원 셀을 청크 단위로 라벨링하고 최소 크기 미만의 소규모 파편을 제거하여 채굴 가치가 있는 광맥 위주로 배치되도록 구현했습니다.
- **조기 종료를 통한 연산 최적화:** 경계 보정 과정에서 데이터 변경이 더 이상 발생하지 않으면 루프를 즉시 종료하여 불필요한 연산을 방지했습니다.


## 동작 확인

- 동일 시드 입력 시 항상 동일한 지형 및 자원이 생성되는 것을 확인했습니다.
- 호스트 및 클라이언트 행성에 서로 다른 바이옴과 고유 자원이 정상 생성되는 것을 확인했습니다.
- 경계 보정 알고리즘 적용 후 바이옴 및 절벽 경계선이 자연스럽게 연결되는 것을 확인했습니다.
- 바이옴 및 자원 요소들이 무작위 파편화되지 않고 군집 형태(광맥)로 생성되는 것을 확인했습니다.
