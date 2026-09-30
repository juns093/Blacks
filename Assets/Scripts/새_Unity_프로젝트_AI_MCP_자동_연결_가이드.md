# 새 Unity 프로젝트와 AI를 위한 MCP 자동 연결 가이드

## 1. 문서 목적

이 문서는 새 Unity 프로젝트에서 사용자가 Codex, Claude Code 또는 다른 MCP 지원 AI에게 다음과 같이 요청했을 때 활용하는 표준 작업 지침이다.

> 이 Unity 프로젝트에 MCP for Unity를 설치하고 너와 연결한 뒤, 실제 Unity 상태를 조회해 연결을 검증해 줘.

목표는 **AI가 가능한 설정을 직접 처리하고, 사용자는 운영체제 보안 승인이나 Unity 재시작처럼 자동화할 수 없는 최소한의 작업만 수행하게 하는 것**이다.

이 문서는 특정 프로젝트 경로, 사용자 이름, 포트 또는 AI 클라이언트에 종속되지 않는다. 프로젝트별 실제 버전과 경로는 연결할 때마다 다시 감지한다.

## 2. 결론: 완전 자동화가 가능한가?

대부분 자동화할 수 있지만 모든 컴퓨터에서 완전한 무인 설치를 보장할 수는 없다.

### AI가 자동으로 처리할 수 있는 작업

- Unity 프로젝트와 Editor 버전 확인
- `Packages/manifest.json` 검사
- MCP for Unity 패키지 추가
- 패키지 설치와 Unity 컴파일 상태 확인
- `uv` 또는 `uvx` 설치 여부와 실제 실행 경로 확인
- Codex MCP 설정 등록
- Claude Code MCP 등록
- 다른 MCP 클라이언트용 설정 생성
- Unity 인스턴스, 활성 씬, Editor 상태와 Console 조회
- 테스트용 오브젝트 생성·삭제로 실제 쓰기 연결 검증
- 설치 결과와 복구 방법을 프로젝트 문서에 기록

### 사용자 승인이 남을 수 있는 작업

- 프로그램 설치 또는 인터넷 다운로드 승인
- 사용자 홈 폴더의 AI 설정 파일 수정 승인
- 방화벽 또는 네트워크 포트 승인
- Unity나 AI 클라이언트 재시작
- Claude Code의 프로젝트 범위 `.mcp.json` 신뢰 승인
- 회사 관리 정책이 막은 MCP 서버 허용

AI는 승인 없이 우회하지 않는다. 필요한 작업과 이유를 한 번에 설명하고 최소 범위의 승인을 요청한다.

## 3. 연결 구조

```text
Codex / Claude Code / Cursor / VS Code / 기타 MCP 클라이언트
                            │
                  MCP 서버 프로세스
                   stdio 또는 HTTP
                            │
                 Unity MCP Bridge
                            │
                     Unity Editor
                            │
                     Unity 프로젝트
```

MCP for Unity는 Unity Editor 안에서 실행되는 브리지와 AI 클라이언트가 실행하는 MCP 서버로 구성된다. 공식 프로젝트는 Claude, Codex, VS Code 및 다른 MCP 클라이언트를 지원한다고 설명한다. [MCP for Unity 공식 저장소](https://github.com/CoplayDev/unity-mcp)

## 4. 기본 원칙

1. **자동 설정을 우선한다.** Unity의 `Window > MCP for Unity`에서 제공하는 `Configure All Detected Clients` 또는 `Auto-Setup`을 우선 사용한다.
2. **버전을 추측하지 않는다.** Unity 패키지 버전과 MCP 서버 버전을 실제 UI·manifest·생성된 설정에서 확인한다.
3. **검증된 버전은 고정한다.** 장기 프로젝트에서는 `#main`보다 검증한 릴리스 태그 또는 커밋을 사용한다.
4. **포트를 고정값으로 가정하지 않는다.** Unity 브리지 포트는 실행 시 달라질 수 있으므로 인스턴스 조회 결과를 따른다.
5. **클라이언트 이름은 `unityMCP`로 통일한다.** 도구 이름과 인수인계가 일관돼진다.
6. **연결 표시만 믿지 않는다.** 읽기 검증과 안전한 쓰기 검증을 모두 통과해야 완료다.
7. **기존 설정을 보존한다.** 설정 파일을 덮어쓰지 말고 Unity MCP 항목만 병합한다.
8. **토큰이나 비밀번호를 문서에 기록하지 않는다.** Unity MCP의 로컬 stdio 연결에는 보통 인증 토큰이 필요하지 않다.

## 5. 사전 확인

AI는 설치 전에 다음 항목을 읽기 전용으로 확인한다.

- Unity 프로젝트 루트: `Assets`, `Packages`, `ProjectSettings`가 함께 있는 폴더
- Unity 버전: `ProjectSettings/ProjectVersion.txt`
- 현재 프로젝트가 Unity Editor에서 열려 있는지
- Git 작업 상태와 기존 사용자 변경 사항
- `Packages/manifest.json`에 `com.coplaydev.unity-mcp`가 이미 있는지
- MCP for Unity의 기존 버전 또는 Git URL
- `uv --version`과 `uvx --version`
- 사용할 AI 클라이언트의 설치 여부
  - Codex: `codex --version`
  - Claude Code: `claude --version`
- 기존 MCP 설정에 `unityMCP`가 있는지

이미 정상 설치된 구성은 다시 설치하지 않는다.

## 6. Unity 프로젝트에 MCP for Unity 설치

### 권장 방법: Unity Package Manager

1. Unity에서 프로젝트를 연다.
2. `Window > Package Manager`를 연다.
3. `+ > Add package from git URL...`을 선택한다.
4. 다음 공식 URL을 입력한다.

```text
https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main
```

5. 설치와 컴파일이 끝날 때까지 기다린다.

공식 Quickstart도 Package Manager 설치 후 `Configure All Detected Clients`를 실행하는 흐름을 안내한다. [MCP for Unity Quickstart](https://github.com/CoplayDev/unity-mcp#quickstart)

### AI 자동 설치 방법

Unity UI를 직접 조작할 수 없지만 프로젝트 파일을 수정할 수 있는 AI는 `Packages/manifest.json`의 `dependencies`에 다음 항목을 병합할 수 있다.

```json
"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main"
```

주의사항:

- 기존 JSON을 깨뜨리지 않는다.
- 이미 설치된 버전을 임의로 변경하지 않는다.
- 설치 후 Unity가 패키지를 내려받고 컴파일할 시간을 준다.
- Unity Console의 패키지·C# 오류를 확인한다.
- 팀 프로젝트는 정상 검증 후 URL 끝을 `#<검증한 릴리스 태그>` 또는 커밋으로 고정한다.

## 7. 필수 실행 도구 확인

MCP for Unity의 로컬 서버는 Python 실행 환경을 위해 `uv/uvx`를 사용한다. 공식 Quickstart는 Python 3.10 이상과 `uv`를 요구한다. [MCP for Unity 요구 사항](https://github.com/CoplayDev/unity-mcp#quickstart)

AI는 먼저 다음 명령의 성공 여부를 확인한다.

```powershell
uv --version
uvx --version
```

명령이 없으면 운영체제에 맞는 `uv` 공식 설치법을 제시하고 설치 승인을 요청한다. PATH에서 발견되지 않으면 무작정 재설치하지 말고 실제 실행 파일을 찾은 뒤 절대 경로를 사용한다.

Windows에서는 여러 `uv.exe`가 있을 수 있다. Unity MCP 창의 `Choose UV Install Location`에서 실제 경로를 지정할 수 있다.

## 8. Unity에서 자동 구성

패키지 설치 후 다음 순서가 가장 안전하다.

1. `Window > MCP for Unity`를 연다.
2. Server Status가 설치됨인지 확인한다.
3. `Auto-Setup` 또는 `Configure All Detected Clients`를 실행한다.
4. Unity Bridge가 중지 상태라면 `Start Bridge`를 누른다.
5. 연결할 클라이언트별 상태가 `Configured`인지 확인한다.
6. Codex나 Claude Code 실행 파일을 찾지 못하면 해당 `Choose ... Location` 버튼으로 실제 실행 파일을 지정한다.

MCP for Unity의 Editor 가이드는 Auto-Setup이 감지된 클라이언트 설정을 작성하고 브리지 연결을 확인하며, Claude Code에는 별도의 등록 동작을 제공한다고 설명한다. [MCP for Unity Editor Plugin Guide](https://github.com/CoplayDev/unity-mcp/blob/beta/MCPForUnity/README.md)

이 Unity 화면 조작은 최초 1회 사용자가 직접 할 수 있다. 컴퓨터 제어 권한이 있는 AI라면 자동으로 수행할 수 있지만, 사용자가 화면 조작을 원하지 않으면 다음 클라이언트별 CLI 등록을 사용한다.

## 9. Codex 자동 연결

### 자동 등록 우선순위

1. Unity MCP 창의 `Configure All Detected Clients`
2. `codex mcp add` 명령
3. 마지막 수단으로 `config.toml` 병합

Codex는 기본적으로 `~/.codex/config.toml`을 사용하며, 신뢰된 프로젝트에서는 프로젝트의 `.codex/config.toml`도 사용할 수 있다. Codex 앱, CLI와 IDE 확장은 이 설정을 공유한다. [OpenAI Codex MCP 문서](https://developers.openai.com/codex/mcp)

### CLI 등록 형식

Unity MCP 창의 Manual Configuration 또는 HTTP Server Command에 표시된 **현재 버전의 정확한 명령**을 복사한 뒤 다음 형식으로 등록한다.

```powershell
codex mcp add unityMCP -- <Unity MCP 창에 표시된 서버 실행 명령과 인수>
```

예시 구조만 보면 다음과 같다. 아래 플레이스홀더를 그대로 실행하지 않는다.

```powershell
codex mcp add unityMCP -- "<uvx 절대 경로>" --from "<Unity가 표시한 서버 패키지와 버전>" mcp-for-unity --transport stdio
```

검증:

```powershell
codex mcp list
```

Codex 안에서는 `/mcp`로 활성 서버를 확인한다. 등록 후 실행 중인 Codex 앱 또는 확장이 새 설정을 읽지 못하면 재시작한다. OpenAI 공식 문서는 `codex mcp add`, `codex mcp list`와 `/mcp` 확인 방법을 제공한다. [OpenAI Codex MCP CLI 설정](https://developers.openai.com/codex/mcp)

### TOML 수동 구성

자동 등록이 실패할 때만 Unity MCP가 생성한 값을 그대로 사용해 기존 파일에 다음 구조를 병합한다.

```toml
[mcp_servers.unityMCP]
command = "<uvx 또는 서버 실행 파일의 절대 경로>"
args = ["<Unity MCP가 표시한 실제 인수>"]
startup_timeout_sec = 60
```

Windows 경로의 역슬래시 이스케이프와 기존 TOML 문법을 보존한다. 전체 설정 파일을 새 내용으로 덮어쓰지 않는다.

## 10. Claude Code 자동 연결

### 자동 등록 우선순위

1. Unity MCP 창의 `Register with Claude Code`
2. `claude mcp add` 명령
3. 프로젝트 공유가 필요한 경우 `.mcp.json`

Unity가 `claude`를 찾지 못하면 Unity MCP 창의 `Choose Claude Location`에서 실행 파일의 절대 경로를 지정한다.

### CLI 등록 형식

Unity MCP 창에 표시된 정확한 서버 명령을 사용한다.

```powershell
claude mcp add --transport stdio --scope user unityMCP -- <Unity MCP 창에 표시된 서버 실행 명령과 인수>
```

Claude Code에서는 옵션을 서버 이름 앞에 두고, `--` 뒤에 MCP 서버 명령과 인수를 넣어야 한다. [Claude Code MCP 공식 문서](https://code.claude.com/docs/en/mcp)

검증:

```powershell
claude mcp list
claude mcp get unityMCP
```

Claude Code 세션 안에서는 `/mcp`를 실행한다.

### 범위 선택

- `--scope user`: 이 컴퓨터 사용자의 모든 프로젝트에서 사용
- `--scope project`: 프로젝트의 `.mcp.json`에 저장하여 팀과 공유 가능
- 기본 local 범위: 현재 프로젝트에서 현재 사용자만 사용

프로젝트 범위 설정은 처음 로드할 때 Claude Code가 신뢰 승인을 요청할 수 있다. 이는 정상적인 보안 절차이므로 자동으로 우회하지 않는다.

## 11. Cursor, VS Code, Windsurf와 다른 MCP 클라이언트

1. Unity MCP 창에서 해당 클라이언트를 선택한다.
2. `Auto Configure`를 사용한다.
3. 자동 감지가 안 되면 `Manual Setup`이 생성한 JSON을 복사한다.
4. 해당 클라이언트 공식 문서의 설정 위치에 `unityMCP` 항목만 병합한다.
5. 클라이언트를 재시작하고 MCP 서버 상태를 확인한다.

클라이언트마다 최상위 키가 `mcpServers`, `servers` 등으로 다를 수 있으므로 다른 클라이언트의 JSON을 그대로 복사하지 않는다. 서버 실행 명령과 인수는 같아도 설정 파일 형식은 해당 클라이언트 문서를 따른다.

## 12. 연결 검증: 반드시 네 단계 모두 수행

### 1단계: 서버 등록 확인

- 클라이언트 목록에 `unityMCP`가 존재한다.
- 실패나 인증 필요 표시가 없다.

### 2단계: Unity 인스턴스 확인

AI가 `mcpforunity://instances`를 읽어 다음을 확인한다.

- 인스턴스 수가 1개 이상
- 대상 프로젝트 이름과 경로가 일치
- 상태가 `running`
- Unity 버전이 실제 프로젝트와 일치

여러 Unity가 열려 있으면 `Name@hash`로 정확한 인스턴스를 활성화한다. 포트 번호만으로 프로젝트를 선택하지 않는다.

### 3단계: Editor 상태 확인

AI가 `mcpforunity://editor/state`를 읽어 확인한다.

- 활성 씬이 조회됨
- `is_compiling=false`
- `is_domain_reload_pending=false`
- `ready_for_tools=true`

상태가 stale이면 잠시 기다렸다가 다시 조회한다.

### 4단계: 안전한 읽기·쓰기 검증

1. 활성 씬과 Hierarchy를 읽는다.
2. 사용자의 허락을 받아 빈 테스트 씬 또는 임시 오브젝트를 만든다.
3. 이름, 위치 또는 컴포넌트를 변경한다.
4. 변경 결과를 다시 읽는다.
5. 임시 항목을 삭제하거나 테스트 씬을 저장할지 사용자 의도에 맞게 처리한다.
6. Unity Console의 error와 warning을 확인한다.

기존 씬이나 사용자 오브젝트를 테스트 대상으로 임의 변경하지 않는다.

## 13. AI가 따라야 할 자동 연결 절차

새 AI는 “연결했습니다”라고 바로 답하지 말고 다음 순서를 수행한다.

```text
1. 프로젝트 루트, Unity 버전, Git 상태 확인
2. 기존 MCP 패키지와 클라이언트 설정 검색
3. 설치가 없으면 공식 패키지를 manifest에 추가하거나 Package Manager로 설치
4. Unity 패키지 로드와 컴파일 완료 확인
5. uv/uvx 및 대상 AI CLI의 실제 경로 확인
6. Unity Auto-Setup을 우선 사용
7. 실패하면 대상 클라이언트 공식 CLI로 unityMCP 등록
8. AI 클라이언트 재시작이 필요하면 사용자에게 한 번만 요청
9. instances → editor/state → hierarchy 순서로 읽기 검증
10. 허가된 임시 대상에 쓰기 검증
11. Console 오류·경고 확인
12. 사용한 패키지 버전, 서버 버전, transport와 설정 위치를 문서에 기록
```

## 14. 사용자에게 최소한으로 요청할 수 있는 작업

자동 처리가 막혔을 때만 다음 중 필요한 항목을 구체적으로 요청한다.

- “Unity를 한 번 재시작해 주세요.”
- “Unity에서 `Window > MCP for Unity`를 열고 `Start Bridge`를 눌러 주세요.”
- “Claude Code가 표시한 프로젝트 MCP 신뢰 요청을 승인해 주세요.”
- “uv 설치를 위한 관리자 승인을 허용해 주세요.”
- “회사 정책에서 로컬 MCP 실행이 허용되는지 관리자에게 확인해 주세요.”

“알아서 설정해 주세요”처럼 모호한 요청을 사용자에게 되돌리지 않는다.

## 15. 자주 발생하는 문제

### Unity 인스턴스가 0개

- Unity 프로젝트가 열려 있는지 확인
- Unity MCP Bridge가 Running인지 확인
- Unity Console의 패키지 오류 확인
- Bridge 재시작 후 AI 클라이언트 재시작

### 서버는 등록됐지만 연결 실패

- 설정에 기록된 `uvx` 또는 CLI 경로가 실제로 존재하는지 확인
- Unity MCP가 표시한 명령과 설정의 인수가 같은지 비교
- 서버 패키지와 Unity 패키지 버전 불일치 확인
- 시작 제한 시간을 60초 정도로 늘려 재검증

### Unity가 `uv` 또는 `claude`를 찾지 못함

- 셸 PATH와 Unity Hub로 실행된 앱의 PATH가 다를 수 있다.
- Unity MCP 창에서 실행 파일의 절대 경로를 지정한다.
- 같은 도구를 중복 설치하기 전에 기존 경로를 검색한다.

### 여러 Unity 프로젝트 중 잘못된 프로젝트가 수정됨

- `mcpforunity://instances`에서 프로젝트 경로 확인
- 정확한 `Name@hash`를 활성 인스턴스로 지정
- 각 쓰기 작업 전에 활성 씬을 다시 확인

### 상태가 stale 또는 busy

- 컴파일, 도메인 리로드, Play Mode 전환이 끝날 때까지 기다린다.
- 동일한 변경 명령을 반복 전송하지 않는다.
- Editor state를 다시 읽고 `ready_for_tools`를 확인한다.

### Claude Code 연결 확인

Claude Code 공식 문서는 `claude mcp list`, `claude mcp get <name>`과 세션 내 `/mcp`를 제공하며, 프로젝트 범위 서버는 승인 대기 상태가 될 수 있다고 설명한다. [Claude Code MCP 서버 상태](https://code.claude.com/docs/en/mcp)

## 16. 보안과 데이터 보호

- 신뢰할 수 있는 공식 MCP 패키지와 서버만 설치한다.
- 설치 전에 Git URL과 패키지 ID를 확인한다.
- 외부 콘텐츠를 읽는 MCP 서버에는 프롬프트 인젝션 위험이 있을 수 있다.
- AI 설정 파일 전체를 출력하거나 공유하지 않는다.
- 환경 변수, API 키, 토큰을 Markdown과 Git에 저장하지 않는다.
- 기존 씬, Prefab, ProjectSettings와 PlayerPrefs를 테스트 과정에서 삭제하지 않는다.
- 네트워크 공개가 필요 없는 로컬 작업은 stdio를 우선한다.
- HTTP를 사용할 때는 `127.0.0.1`과 방화벽 범위를 우선하고 불필요하게 외부 인터페이스에 노출하지 않는다.

## 17. 설치 완료 보고 양식

AI는 완료 후 다음 형식으로 보고한다.

```text
Unity MCP 연결 완료

- 프로젝트: <절대 경로>
- Unity 버전: <버전>
- Unity MCP 패키지: <패키지 ID와 버전/커밋>
- MCP 서버: <실제 서버 버전>
- 클라이언트: <Codex / Claude Code / 기타>
- Transport: <stdio / HTTP>
- 설정 위치: <민감 정보 없이 경로만>
- Unity 인스턴스: <Name@hash>
- 활성 씬 조회: 성공
- 읽기 검증: 성공
- 쓰기 검증: 성공 또는 생략 이유
- Console 오류·경고: <건수>
- 재시작 필요 여부: <없음/필요>
```

## 18. 새 AI에게 전달할 자동 연결 명령문

아래 문구를 새 AI에게 그대로 전달한다.

```text
현재 Unity 프로젝트에 MCP for Unity를 설치하고 너 자신과 연결해 줘. 사용자가 직접 설정하게 하지 말고, 네가 접근 가능한 파일·터미널·Unity 도구로 가능한 부분은 모두 자동 처리해라.

먼저 프로젝트 루트, Unity 버전, Git 상태, Packages/manifest.json, 기존 MCP 설정, uv/uvx와 네 AI 클라이언트 CLI 설치 여부를 읽기 전용으로 확인해라. 기존 설치와 사용자 설정은 보존하고 중복 설치하거나 설정 파일 전체를 덮어쓰지 마라.

MCP for Unity가 없으면 공식 CoplayDev 패키지를 설치하고 Unity 컴파일 완료와 Console을 확인해라. 설치 후 Unity의 Auto-Setup/Configure All Detected Clients 방식을 우선하고, 사용할 수 없으면 네 클라이언트의 공식 MCP 등록 명령으로 이름 `unityMCP`의 로컬 stdio 서버를 등록해라. 서버 실행 명령과 버전은 추측하지 말고 Unity MCP 창, 설치 패키지 또는 생성된 Manual Configuration에서 실제 값을 가져와라.

Codex라면 공식 `codex mcp add/list` 또는 기존 config.toml 병합 방식을 사용하고, Claude Code라면 Unity의 Register with Claude Code 또는 공식 `claude mcp add/list/get` 방식을 사용해라. 다른 클라이언트라면 그 클라이언트의 공식 설정 형식을 확인해라.

등록 후 AI 클라이언트 재시작이 꼭 필요할 때만 나에게 한 번 요청해라. 연결 검증은 반드시 `mcpforunity://instances`, `mcpforunity://editor/state`, 활성 씬과 Hierarchy 읽기 순으로 수행해라. 여러 Unity 인스턴스가 있으면 프로젝트 절대 경로가 일치하는 인스턴스를 선택해라. 사용자에게 허락된 임시 씬이나 오브젝트로 쓰기 검증을 하고 Unity Console 오류·경고까지 확인해라.

완료 후 프로젝트 경로, Unity 버전, Unity MCP 패키지와 서버 버전, 클라이언트, transport, 설정 위치, 인스턴스 ID, 읽기·쓰기 검증 결과와 Console 상태를 보고하고 프로젝트 Documentation 폴더에 연결 정보를 기록해라. 토큰·비밀번호·전체 사용자 설정은 문서에 남기지 마라.
```

## 19. 공식 참고 자료

- [MCP for Unity 공식 저장소와 Quickstart](https://github.com/CoplayDev/unity-mcp)
- [MCP for Unity Editor Plugin Guide](https://github.com/CoplayDev/unity-mcp/blob/beta/MCPForUnity/README.md)
- [OpenAI Codex MCP 공식 문서](https://developers.openai.com/codex/mcp)
- [Claude Code MCP 공식 문서](https://code.claude.com/docs/en/mcp)

문서의 명령과 UI는 도구 업데이트로 바뀔 수 있다. 새 프로젝트에 적용할 때는 위 공식 문서와 Unity MCP 창이 표시하는 현재 명령을 우선한다.
