# Animator 제거와 셰이더 애니메이션을 통한 렌더링 최적화

수천 개의 건물이 각각 Animator 컴포넌트로 스프라이트 애니메이션을 재생하면, 매 프레임 오브젝트 개수만큼 상태 머신 연산이 메인 스레드에 누적되어 심각한 CPU 병목을 유발합니다.

이를 해결하기 위해 Animator 컴포넌트를 제거하고, 애니메이션 연산을 셰이더의 `_Time` 기반으로 전환했습니다. 또한 MaterialPropertyBlock을 활용해 개별 머티리얼 인스턴스를 생성하지 않고 오브젝트별 파라미터를 전달함으로써, 오브젝트마다 다른 프레임이 재생되어도 GPU 배칭이 유지되도록 최적화했습니다.

관련 원본: [`ShaderAnimController.cs`](./ShaderAnimController.cs) · [`ShaderAnimated.shader`](./ShaderAnimated.shader) · [`ShaderAnimData.cs`](./ShaderAnimData.cs) · [`ShaderAnimSelector.cs`](./ShaderAnimSelector.cs)


## 구성 요소와 역할

| 구성 요소 | 역할 |
| --- | --- |
| `ShaderAnimated.shader` | 프래그먼트 셰이더가 전역 시간 `_Time`으로 현재 프레임 인덱스를 계산해 아틀라스에서 해당 셀만 샘플링합니다. CPU 개입 없이 GPU에서 애니메이션을 처리하며, GPU Instancing을 지원해 다수의 오브젝트를 한 번의 드로우콜로 묶어 그립니다. |
| `ShaderAnimController` | Animator를 대체하는 컨트롤러입니다. 애니메이션이 변경될 때만 MaterialPropertyBlock으로 아틀라스, 프레임 수, 프레임레이트 등 파라미터를 주입합니다. 머티리얼을 복제하지 않으므로 배칭이 유지됩니다. |
| `ShaderAnimData` (ScriptableObject) | 아틀라스 텍스처, 프레임 수, 기본 프레임레이트, 동기화 여부 등의 데이터를 관리합니다. 애니메이션 설정값을 코드 수정 없이 에셋 형태로 관리할 수 있습니다. |
| `ShaderAnimSelector` | 건물 상태(작동, 정지 등)에 맞는 `ShaderAnimData`를 선택해 컨트롤러에 전달합니다. |


## 동작 흐름

```
상태 변경 (건물 작동/정지 등)
↓
ShaderAnimSelector가 상황에 맞는 ShaderAnimData 선택
↓
ShaderAnimController.SetAnimation(data)
MaterialPropertyBlock에 아틀라스, 프레임 크기, 프레임 수, 프레임레이트 설정
↓
GPU 셰이더가 _Time으로 프레임 계산 (CPU·Animator 사용 안 함)
frame = floor(_Time * frameRate) % totalFrames → 아틀라스 셀 UV
↓
정지/재생/속도는 _FrameRate 값 변경으로 제어 (Pause = 0)
```

### 1. 파라미터 주입 (CPU, 상태 변경 시 1회)

컨트롤러는 애니메이션 상태가 변경될 때만 MaterialPropertyBlock으로 아틀라스, 프레임 크기, 프레임 수, 프레임레이트를 전달합니다. 머티리얼을 직접 복제하지 않으므로 드로우콜 배칭이 깨지지 않습니다.

```csharp
// ShaderAnimController.SetAnimation
rend.GetPropertyBlock(mpb);
mpb.SetTexture("_MainTex", animData.atlas);
mpb.SetFloat("_TotalFrames", animData.totalFrames);
mpb.SetFloat("_FrameColumns", animData.columns);
mpb.SetFloat("_FrameRate", baseFrameRate * speedMultiplier);
mpb.SetFloat("_TimeOffset", animData.sync ? 0f : Random.Range(0f, 10f));
rend.SetPropertyBlock(mpb);
```

### 2. 프레임 계산 (GPU, 매 픽셀)

셰이더가 전역 시간을 기준으로 현재 프레임을 산출하고 아틀라스의 해당 셀을 샘플링합니다.

```hlsl
// ShaderAnimated.shader (frag)
float time  = _Time.y + timeOffset;
float frame = floor(time * frameRate);
frame = frame - floor(frame / totalFrames) * totalFrames;
float col = frame - floor(frame / frameColumns) * frameColumns;
float row = floor(frame / frameColumns);
uv = float2(IN.texcoord.x + col * frameWidth, IN.texcoord.y - row * frameHeight);
```

### 3. 정지·재생·속도 제어 (CPU, 저비용 연산)

상태 전환 시 `_FrameRate` 파라미터 하나만 변경합니다. 값을 0으로 설정하면 프레임 진행이 멈추어 정지 상태가 됩니다.

```csharp
// ShaderAnimController.Pause / Resume
mpb.SetFloat("_FrameRate", 0f); // Pause
mpb.SetFloat("_FrameRate", baseFrameRate * speedMultiplier); // Resume
```


## 설계 포인트

- **CPU 오버헤드 제거:** Animator 컴포넌트를 제거하여 개체 수만큼 쌓이던 상태 머신 연산 병목을 해소했습니다. 프레임 계산은 GPU에서 `_Time`을 기반으로 일괄 처리합니다.
- **드로우콜 최적화:** MaterialPropertyBlock과 GPU Instancing을 결합해 오브젝트마다 서로 다른 프레임을 재생하더라도 머티리얼 인스턴스가 생성되지 않아 배칭이 유지됩니다.
- **재생 시점 분산:** `_TimeOffset`을 무작위로 부여해 수많은 건물이 동일한 프레임으로 재생되는 어색한 연출을 방지했습니다 (동기화가 필요한 경우에는 0 지정 가능).
- **경량 상태 전환:** 정지, 재생, 속도 조절을 `_FrameRate` 파라미터 하나로 제어하므로 상태 변경에 드는 비용이 매우 적습니다.


## 동작 확인

- 대량의 건물이 배치되어도 Animator 없이 GPU 연산만으로 애니메이션이 원활히 동작하는 것을 확인했습니다.
- 정지 시 애니메이션이 멈추고, 재개 시 전역 시간 기준 프레임부터 다시 재생됩니다.
- 인스턴스별 재생 시점이 분산되어, 동일한 애니메이션을 사용하는 건물이 다수 배치되어도 자연스럽게 연출됩니다.
- 머티리얼 인스턴스가 중복 생성되지 않아 드로우콜 배칭 상태가 지속해서 유지됨을 확인했습니다.
