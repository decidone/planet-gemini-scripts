# Planet Gemini

Planet Gemini 개발 과정에서 작성한 핵심 코드들을 기능별로 정리한 저장소입니다. 전체 프로젝트가 아닌 주요 기능 구현 샘플만 수록하였으며, 아트/오디오 에셋 및 서드파티 플러그인은 제외되어 있어 독립적인 빌드는 불가능합니다.

각 기능 폴더의 `README.md`에서는 입력 처리부터 상태 변경까지의 실행 흐름을 핵심 소스 코드와 함께 상세히 설명하며, 원본 스크립트 및 셰이더 파일도 함께 제공합니다.

**프로젝트의 실제 플레이 및 PV 영상은 아래 스토어 링크에서 확인하실 수 있습니다.**

<a href="https://store.steampowered.com/app/3183170/Planet_Gemini/" target="_blank" rel="noopener noreferrer"><img src="https://img.shields.io/badge/Planet_Gemini_on_Steam-1b2838?style=for-the-badge&logo=steam&logoColor=white" width="400" alt="Planet Gemini on Steam"></a>


## 플레이 영상

<img width="640" height="360" alt="Image" src="https://github.com/user-attachments/assets/6ef5fff2-c40f-4c01-a001-adf2ea6db191" />


## 사용 기술 및 역할

| 분류 | 기술 스택 | 주요 역할 및 담당 기능 |
| --- | --- | --- |
| 엔진 | Unity (2021.3.10f1 → 6000.0.59f2) | 2D 타일 기반 게임 시스템 구동 (렌더링, 물리, 입력 처리) |
| 멀티플레이 | Netcode for GameObjects(NGO), Steamworks, Facepunch Transport | 호스트-클라이언트 상태 동기화, Steam Lobby 연동, P2P 통신 구현 |
| 데이터 | Newtonsoft.Json + Brotli | 세이브/로드 데이터 직렬화, 압축 저장 및 네트워크 전송 |
| 렌더링 | 커스텀 셰이더, Render Texture | 셰이더 기반 애니메이션 처리, 시야(Fog of War) 연산 및 마스킹 |
| 최적화 | 이벤트 기반 아키텍처, 오브젝트 풀링 | Update 폴링 구조 제거를 통한 연산 최적화, 재사용을 통한 메모리 관리 |


## 주요 구현 샘플

| 샘플 | 주제 | 문서 |
| --- | --- | --- |
| 렌더링 최적화 | Animator 컴포넌트 제거 및 셰이더 기반 애니메이션 전환 | [RenderingOptimization](RenderingOptimization/README.md) |
| 시야 시스템 | 시야 밖 몬스터 가리기, 부드러운 시야 경계 처리, 타일맵 마스킹 | [FogOfWar](FogOfWar/README.md) |
| 연산 최적화 | Update() 폴링을 이벤트 기반 캐싱으로 리팩토링 | [EventDrivenOptimization](EventDrivenOptimization/README.md) |
| 절차적 맵 생성 | Perlin Noise 기반 지형·자원 생성 | [ProceduralMapGeneration](ProceduralMapGeneration/README.md) |
| 인벤토리 | ScriptableObject 기반 데이터, 슬롯 시스템, 오브젝트 풀링 | [InventoryItem](InventoryItem/README.md) |
| 생산 · 공장 | JSON 데이터(Recipe) 기반 생산 파이프라인 구축 | [ProductionFactory](ProductionFactory/README.md) |
| 에너지 | 에너지 그룹 단위 관리(생성·병합·분리·제거) 및 생산 효율 계산 | [EnergySystem](EnergySystem/README.md) |
| 상점 | NPC 거래 중심의 경제 시스템 구축 및 후반 아이템 수급 경로 제공 | [Shop](Shop/README.md) |
| 세이브 · 로드 | JSON 직렬화 및 Brotli 압축을 활용한 데이터 관리 | [SaveLoad](SaveLoad/README.md) |
| 멀티플레이 기반 | NGO, Steamworks, Facepunch Transport 기반 네트워크 구현 | [MultiplayerBase](MultiplayerBase/README.md) |
| 멀티플레이 동기화 | 슬라이딩 윈도우 알고리즘을 도입하여 상태 일괄 전송 폭주로 인한 접속 장애 해결 | [MultiplayerSync](MultiplayerSync/README.md) |
| 엔진 업그레이드 | 런타임 보안 취약점(CVE-2025-59489) 대응을 위한 엔진 버전 업그레이드 | [UnityVersionUpgrade](UnityVersionUpgrade/README.md) |
| 라이브 이슈 대응 | AMD GPU 환경의 애니메이션 렌더링 오류 원인 분석 및 해결 | [PostLaunch](PostLaunch/README.md) |


## 공개 범위

본인이 직접 작성한 스크립트 및 셰이더 코드만 공개합니다. 서드파티 에셋 및 외부 SDK(Alpha Masking, A* Pathfinding Project, Steamworks 등)는 포함되어 있지 않으며 참조 형태로만 존재합니다.

---

Copyright (c) 2026 Planet Gemini All rights reserved.

본 저장소의 코드는 열람 및 역량 평가 목적으로만 공개됩니다.<br>사전 서면 동의 없는 무단 복제, 수정, 배포 및 상업적 이용을 금합니다.
