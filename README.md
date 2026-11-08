# LIFE OF MAFIA — 스테이지 1 데모

C# 기말과제 · 현민규 · 26031031

실행하면 컷씬 없이 스테이지 1 전투가 바로 시작됩니다. 적 4명을 모두 처치하면 클리어,
플레이어 HP가 0이 되면 게임 오버입니다. 처치한 적은 자동으로 부활하지 않습니다.
성공·실패 후 R로 처음부터 다시 플레이할 수 있습니다.

## 실행

Windows와 .NET 10 SDK가 설치된 환경에서 `ONESHOTONEKILL/ONESHOTONEKILL.slnx`를 열고
실행하거나, 저장소 루트에서 다음 명령을 실행하세요.

```powershell
dotnet run --project ONESHOTONEKILL/ONESHOTONEKILL.csproj
```

실행 파일로 배포하려면 다음과 같이 게시합니다. 생성된 폴더 전체를 함께 전달해야 하며,
받는 Windows PC에는 .NET 10 Desktop Runtime이 필요합니다.

```powershell
dotnet publish ONESHOTONEKILL/ONESHOTONEKILL.csproj -c Release --self-contained false -o artifacts/Stage1Demo
```

`artifacts/Stage1Demo/ONESHOTONEKILL.exe`를 실행하세요. 이미지와 음악은 빌드·게시 시 자동으로 복사됩니다.

## 조작

| 동작 | 키 |
| --- | --- |
| 이동 | 방향키 / WASD |
| 사격 | Z / Space (누르고 있으면 연사) |
| 사격 방향 | 마지막으로 이동한 좌우 방향 |
| 다시 시작 | R |
| 일시정지 / 계속 | Esc |
| 배경음악 볼륨 | F5 감소 / F6 증가 |
| TV 효과 전환 | F2 |
| 컬러 / 흑백 전환 | = |
| 전체 화면 전환 | Alt + Enter |

사격 중에는 이동할 수 없습니다. 적의 조준 동작을 보고 사격을 멈춰 위아래로 피한 뒤 반격하세요.
게임 창에서 벗어나면 전투와 기록 시간이 자동으로 멈춥니다.

## 브랜치

- `main`: 과제 제출용 스테이지 1 전투 데모.
- `feature/story-cutscenes`: 기존 오프닝, 25년 전 문구, 자동차 컷씬과 관련 리소스를 보존한 작업.

## 로직 확인

```powershell
dotnet run --project tests/Stage1.Checks/Stage1.Checks.csproj
```

처치 판정, 클리어·실패, 결과 고정, 자동 부활 방지, 재시작 초기화와 탄환 충돌을 확인합니다.
