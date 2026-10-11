# Kanban

Avalonia UI와 MVVM 패턴으로 만드는 크로스플랫폼 데스크톱 칸반 보드 앱입니다.
Avalonia를 학습하면서 단계별로 기능을 쌓아가는 개인 프로젝트입니다.

> 현재 개발 중입니다. 아래 [로드맵](#로드맵)에서 진행 상황을 확인할 수 있습니다.

## 기능

- [x] 보드, 리스트, 카드 표시
- [x] 카드 추가 / 삭제 / 제목 수정
- [x] 리스트 추가 / 삭제
- [ ] 드래그 앤 드롭으로 카드 이동
- [ ] SQLite 저장
- [ ] 라벨, 마감일, 체크리스트
- [ ] 다크 / 라이트 테마, 검색 / 필터

## 기술 스택

| 구분 | 사용 기술 |
|---|---|
| 언어 / 런타임 | C#, .NET 10 |
| UI 프레임워크 | [Avalonia](https://avaloniaui.net/) 12 |
| MVVM | [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) 8 |
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
└── Kanban.App/
    ├── ViewModels/   # MainWindow, BoardList, Card ViewModel
    ├── Views/        # MainWindow, BoardListView, CardView (.axaml)
    ├── ViewLocator.cs  # ViewModel → View 자동 연결
    └── App.axaml     # 앱 진입점, 테마
```

- **MVVM**: View는 바인딩만 하고, 상태와 동작은 ViewModel이 가집니다.
- **ViewLocator**: `CardViewModel`을 `CardView`로 이름 규칙에 따라 자동 연결하므로 `ItemTemplate`을 따로 지정하지 않습니다.
- **커맨드**: `[RelayCommand]`, `[ObservableProperty]` 소스 생성기로 보일러플레이트를 줄였습니다.

## 로드맵

| 단계 | 내용 | 상태 |
|---|---|---|
| 1 | 보드 / 리스트 / 카드 레이아웃 | 완료 |
| 2 | 카드·리스트 추가, 수정, 삭제 (MVVM, Command) | 완료 |
| 3 | 드래그 앤 드롭 | 예정 |
| 4 | SQLite(EF Core) 저장, 라벨 / 마감일 / 체크리스트 | 예정 |
| 5 | 테마, 검색 / 필터, 설정 | 예정 |
| 마무리 | 단위 테스트(xUnit), GitHub Actions 크로스플랫폼 빌드, 릴리스 | 예정 |
