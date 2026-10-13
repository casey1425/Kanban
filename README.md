# Kanban

Avalonia UI와 MVVM 패턴으로 만드는 크로스플랫폼 데스크톱 칸반 보드 앱입니다.
Avalonia를 학습하면서 단계별로 기능을 쌓아가는 개인 프로젝트입니다.

> 현재 개발 중입니다. 아래 [로드맵](#로드맵)에서 진행 상황을 확인할 수 있습니다.

## 기능

- [x] 보드, 리스트, 카드 표시
- [x] 카드 추가 / 삭제 / 제목 수정
- [x] 리스트 추가 / 삭제
- [x] 드래그 앤 드롭으로 카드 순서 변경 및 리스트 간 이동
- [x] SQLite 저장 (앱을 다시 켜도 보드가 유지됨)
- [x] 카드 상세: 설명, 마감일, 라벨, 체크리스트
- [ ] 다크 / 라이트 테마, 검색 / 필터

## 기술 스택

| 구분 | 사용 기술 |
|---|---|
| 언어 / 런타임 | C#, .NET 10 |
| UI 프레임워크 | [Avalonia](https://avaloniaui.net/) 12 |
| MVVM | [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) 8 |
| 데이터 | EF Core 10 + SQLite (마이그레이션 사용) |
| 구성 | Microsoft.Extensions.DependencyInjection |
| IDE | JetBrains Rider |

## 실행 방법

[.NET 10 SDK](https://dotnet.microsoft.com/download)가 필요합니다.

```bash
git clone https://github.com/casey1425/Kanban.git
cd Kanban
dotnet run --project Kanban.App
```

Windows, macOS, Linux에서 실행할 수 있습니다.

## 프로젝트 구조

```
Kanban/
├── Kanban.sln
├── Kanban.Core/          # 모델, IBoardRepository (UI·DB에 의존하지 않음)
├── Kanban.Data/          # EF Core DbContext, 저장소 구현, 마이그레이션
└── Kanban.App/           # Avalonia UI
    ├── ViewModels/       # MainWindow, BoardList, Card, CardDetail ...
    ├── Views/            # *.axaml
    ├── Messages/         # ViewModel 간 메시지
    └── ViewLocator.cs    # ViewModel → View 자동 연결
```

- **레이어 분리**: UI는 `IBoardRepository`만 알고, SQLite 구현은 DI로 주입됩니다.
- **MVVM**: View는 바인딩과 입력 이벤트만 다루고, 상태와 동작은 ViewModel이 가집니다.
- **ViewLocator**: ViewModel 이름 규칙으로 View를 자동 연결하므로 `ItemTemplate`을 따로 지정하지 않습니다.
- **Messenger**: 카드가 창을 직접 알지 못한 채 `OpenCardRequested` 메시지로 상세 패널을 요청합니다.
- **커맨드**: `[RelayCommand]`, `[ObservableProperty]` 소스 생성기로 보일러플레이트를 줄였습니다.

## 데이터 저장 위치

사용자 데이터 폴더의 `Kanban/kanban.db`에 저장됩니다.
(macOS/Linux: `~/.local/share/Kanban/kanban.db`, Windows: `%LOCALAPPDATA%\Kanban\kanban.db`)
앱 시작 시 마이그레이션이 자동으로 적용됩니다.

### 마이그레이션 추가 (개발자용)

```bash
dotnet tool restore
dotnet ef migrations add <이름> --project Kanban.Data --startup-project Kanban.Data
```

## 로드맵

| 단계 | 내용 | 상태 |
|---|---|---|
| 1 | 보드 / 리스트 / 카드 레이아웃 | 완료 |
| 2 | 카드·리스트 추가, 수정, 삭제 (MVVM, Command) | 완료 |
| 3 | 드래그 앤 드롭 | 완료 |
| 4 | SQLite(EF Core) 저장, 마감일 / 라벨 / 체크리스트 | 완료 |
| 5 | 테마, 검색 / 필터, 설정 | 예정 |
| 마무리 | 단위 테스트(xUnit), GitHub Actions 크로스플랫폼 빌드, 릴리스 | 예정 |
