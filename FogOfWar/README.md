# 렌더 텍스처 마스크를 통한 시야(Fog of War) 밖 몬스터 가시성 제어

시야 처리에 유니티 스프라이트 마스크를 쓰면 배칭이 깨져 드로우콜이 크게 늘어납니다. 이를 피하기 위해 건물과 유닛의 시야를 별도 카메라로 마스크 텍스처에 렌더링하고, 이 마스크를 셰이더 전역 변수로 공유합니다. 각 몬스터는 자신의 월드 좌표에 해당하는 마스크 값을 참조하여 시야 밖일 경우 알파를 0으로 만들어 화면에서 가립니다.

셰이더가 마스크 한 장으로 가시성을 처리하므로, 유닛 수가 많아도 적은 연산 비용으로 적 정보를 은닉할 수 있습니다. 또한 Alpha Masking으로 시야 마스크를 부드럽게 합성하여 각지기 쉬운 타일 경계를 그라데이션으로 연출했습니다. 이로 인해 연속적인 마스크 값이 생성되어, 몬스터가 시야 경계에서 갑자기 사라지지 않고 자연스럽게 페이드아웃됩니다. (Alpha Masking 에셋은 서드파티 플러그인으로, 저장소에 포함되어 있지 않습니다.)

관련 원본: [`MonsterFogVisible.shader`](./MonsterFogVisible.shader) · [`AlphaCameraController.cs`](./AlphaCameraController.cs)

<img width="640" height="360" alt="Image" src="https://github.com/user-attachments/assets/83c91f25-65a6-497d-a631-e709db2f547d" />


## 구성 요소와 역할

| 구성 요소 | 역할 |
| --- | --- |
| 시야 카메라 + 렌더 텍스처 | 건물·유닛의 시야 모양을 별도 카메라로 렌더 텍스처(마스크) 한 장에 그린다. |
| `AlphaCameraController` | 마스크 텍스처를 `_FogTex`, 카메라 위치·크기를 `_FogCameraParams`로 셰이더 전역에 올려, 이 셰이더를 쓰는 모든 오브젝트가 같은 시야를 참조하게 한다. |
| `MonsterFogVisible.shader` | 몬스터가 자신의 월드좌표를 마스크 UV로 변환해 시야 값을 읽고, 시야 밖이면 알파를 0으로 만들어 화면에서 가린다. |
| Alpha Masking (서드파티) | 타일맵 fog 마스크를 부드럽게 합성해 시야 경계가 타일 단위로 각지지 않게 한다. (에셋 미포함, 참조만) |


## 동작 흐름

```
건물·유닛의 시야를 시야 카메라가 마스크 텍스처로 렌더
↓
AlphaCameraController: 마스크를 _FogTex, 카메라 위치·크기를 _FogCameraParams 로 전역 세팅
↓
몬스터 셰이더(MonsterFogVisible): 몬스터 월드좌표 → _FogTex UV 샘플
visibility = 1 - fog.r
↓
시야 밖 몬스터는 알파 제거 → 화면에서 사라짐(투명 처리)
```

### 1. 시야 마스크 전역 공유

시야 카메라가 그린 마스크와 그 카메라의 위치·크기를 셰이더 전역 변수로 넘깁니다.

```csharp
// AlphaCameraController
Shader.SetGlobalTexture("_FogTex", alphaRenderCamera.targetTexture);
Shader.SetGlobalVector("_FogCameraParams", new Vector4(camX, camY, width, height));
```

### 2. 몬스터 가시성 (셰이더)

몬스터의 월드 좌표를 마스크 UV로 변환해 시야 값을 읽고, 시야 밖이면 알파를 0으로 만듭니다.

```hlsl
// MonsterFogVisible.shader (frag)
float2 fogUV = (IN.worldPos - _FogCameraParams.xy) / _FogCameraParams.zw + 0.5;
float visibility = 1.0 - tex2D(_FogTex, fogUV).r;   // 시야 밖(검정) → 0
fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
c.a *= visibility;                                   // 시야 밖 몬스터는 투명
c.rgb *= c.a;
```


## 설계 포인트

- **배칭 유지:** 시야 판정을 스프라이트 마스크(스텐실)가 아니라 프래그먼트 셰이더에서 마스크 텍스처를 샘플링해 처리하므로, 스프라이트들의 배칭이 깨지지 않고 유지됩니다.
- **적 정보 은닉:** 시야 밖 몬스터의 알파를 0으로 만들어 화면에서 감춰, 플레이어가 밝히지 않은 지역의 적 위치가 드러나지 않도록 했습니다.
- **데이터 전역 공유:** 마스크와 카메라 파라미터를 전역 셰이더 변수로 올려, 시야 데이터가 필요한 오브젝트가 개별 연결 없이 같은 값을 참조하도록 했습니다.
- **부드러운 경계 연출:** 연속적인 마스크 값을 활용하여 시야 경계선이 타일 단위로 각지지 않고 몬스터 및 지형이 부드럽게 나타나거나 사라지도록 구현했습니다.


## 동작 확인

- 시야 범위 내 몬스터만 노출되며 범위 밖의 개체는 알파 처리를 통해 가려지는 것을 확인했습니다.
- 시야를 제공하는 건물/유닛의 이동에 따라 마스크 텍스처가 갱신되며 가시성이 실시간 반영됩니다.
- 스프라이트 마스크 없이도 시야 밖 몬스터가 정상적으로 감춰지는 것을 확인했습니다.
