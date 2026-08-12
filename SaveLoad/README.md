# JSON 직렬화와 Brotli 압축을 통한 세이브·로드

게임 상태를 JSON 형식으로 직렬화하여 저장하고, 로드 시에는 저장된 데이터를 바탕으로 객체 인스턴스를 동적으로 재구성합니다. 이때 용량이 큰 맵과 청사진 데이터는 Brotli 알고리즘으로 압축해 별도 파일로 분리 저장함으로써 파일 용량과 로드 시의 메모리 부담을 최적화했습니다.

관련 스크립트: [`DataManager.cs`](./DataManager.cs) · [`Compression.cs`](./Compression.cs) · [`LoadManager.cs`](./LoadManager.cs) · [`SaveData.cs`](./SaveData.cs) · [`StructureSaveData.cs`](./StructureSaveData.cs)

<img width="640" height="360" alt="Image" src="https://github.com/user-attachments/assets/089b3ce7-bfed-4453-a59e-3510ad2dc2ef" />


## 구성 요소와 역할

| 구성 요소 | 역할 |
| --- | --- |
| `Newtonsoft.Json` | 게임 상태 객체와 JSON 데이터 간의 직렬화 및 역직렬화를 처리합니다. |
| `Compression` | 대용량 맵 및 청사진 데이터를 Brotli 알고리즘으로 압축하고, 로드 시 다시 원본으로 복원합니다. |
| `DataManager` | 각 서브시스템의 세이브 데이터를 수집하여 통합 저장하고, 로드 시 데이터를 각 시스템에 재분배합니다. |
| `*SaveData` 구조체 | 건물, 인벤토리, 맵, 유닛 등 각 서브시스템이 저장할 데이터 구조를 정의합니다. 로드 시 해당 데이터를 기반으로 상태를 복원합니다. |


## 동작 흐름

```
각 서브시스템.SaveData() 취합 → SaveData
↓
JsonConvert.SerializeObject
게임 데이터 → slot.json
맵/청사진 → Brotli 압축 → slot.maps / slot.blueprints
↓
로드: 파일 → Deserialize
LoadData(서브시스템 분배) + SpawnStructure(건물 재생성)
```

### 1. 서브시스템 상태 취합 및 직렬화

각 시스템의 상태 구조체를 `DataManager`가 하나로 통합하여 직렬화합니다. 기본 게임 데이터는 JSON 파일로 저장하고, 대용량 데이터는 압축 과정을 거칩니다.

```csharp
// DataManager.SaveCoroutine
string json = JsonConvert.SerializeObject(saveData);
File.WriteAllText (path + slot + ".json", json);
string mapJson = JsonConvert.SerializeObject(mapsSaveData);
File.WriteAllBytes(path + slot + ".maps", Compression.Compress(mapJson));   // 맵은 압축
```

### 2. Brotli 스트림을 통한 데이터 압축

용량이 큰 맵 및 청사진 JSON 데이터는 BrotliStream을 활용해 바이트 배열로 압축 후 별도 파일(`.maps`, `.blueprints`)로 디스크에 저장합니다.

```csharp
// Compression.Compress
using var brotliStream = new BrotliStream(output, CompressionLevel.Fastest);
input.CopyTo(brotliStream);
return output.ToArray();
```

### 3. 데이터 역직렬화 및 씬 개체 동적 생성

저장된 파일에서 데이터를 읽어 각 서브시스템에 재분배하고, 건물 및 유닛 등의 개체는 해당 세이브 데이터를 기반으로 생성하여 씬 상태를 복원합니다.

```csharp
// DataManager.Load
saveData = LoadManager.instance.GetSaveData();
LoadData(saveData);                        // 인벤·과학·통계 등 분배
foreach (var structureSave in ...) SpawnStructure(structureSave);   // 건물 재생성
```


## 설계 포인트

- **모듈화된 세이브/로드 책임 분리:** 각 서브시스템이 자체적인 `SaveData()` 및 `LoadData()` 로직을 전담하고, `DataManager`는 수집과 분배 역할만 수행하도록 구조화했습니다.
- **대용량 데이터 분리 압축:** 용량이 큰 맵 및 청사진 데이터는 Brotli 알고리즘으로 별도 압축 보관하여 기본 `.json` 세이브 파일의 가독성과 처리 속도를 확보했습니다.
- **세이브 데이터 구조 최적화:** 씬 전체 상태를 저장하는 대신 복원에 필요한 최소 정보(`StructureSaveData`)만 추출해 저장하고, 로드 시 이를 기반으로 객체를 인스턴스화하여 파일 용량을 최적화했습니다.


## 동작 확인

- 저장 후 로드 시 건물 인스턴스, 인벤토리, 맵 데이터, 과학 및 통계 수치가 원상태로 정상 복원됨을 확인했습니다.
- 맵 및 청사진 데이터가 정상적으로 압축되어 텍스트 세이브 파일 대비 용량이 대폭 감소함을 확인했습니다.
- 저장 슬롯별로 `.json`, `.maps`, `.blueprints` 파일이 분리되어 디스크에 저장되는 것을 확인했습니다.
