---
name: write-pr
description: >
  PR 본문을 작성한다. 현재 브랜치의 커밋과 diff를 분석해
  개요 → 해결 과제 → 해결 방안 → 기대 효과 → 영향 범위 구조로
  한국어 PR 본문을 생성하고 GitHub에 PR을 열거나 기존 PR을 업데이트한다.
  사용자가 "PR 써줘", "PR 본문 작성", "PR 올려줘", "write PR", "/write-pr"라고 말할 때 사용한다.
---

# write-pr

PR#14를 모범 예시로 삼아 PR 본문을 작성하고 GitHub PR을 생성 또는 업데이트한다.
모범 예시 전문은 `references/pr-example.md`를 읽어 톤·구조·상세도를 맞춘다.

## 워크플로우

### 1. 컨텍스트 수집

다음을 병렬로 실행한다:

```bash
git log main..HEAD --oneline           # 커밋 목록
git diff main...HEAD --stat            # 변경 파일 통계
git diff main...HEAD                   # 전체 diff (영향 범위 파악용)
```

`main` 브랜치가 없으면 `origin/main` 또는 base 브랜치로 대체한다.

### 2. PR 본문 작성

`references/pr-example.md`를 읽어 구조와 톤을 참고한 뒤
`.github/PULL_REQUEST_TEMPLATE.md` 템플릿을 채운다.

#### 섹션별 작성 기준

**개요**
- 무엇을 왜 바꿨는지 2~4문장으로 요약
- 독자가 diff를 보지 않아도 변경의 맥락을 이해할 수 있어야 함

**해결 과제**
- 문제가 여러 개면 번호 붙여 나열
- 각 문제는: 현상 → 왜 나쁜지 → (가능하면) 코드 스니펫 순서로 기술
- PR#14처럼 구체적인 라인 번호나 메서드 이름을 명시하면 좋음

**해결 방안**
- before/after 구조 다이어그램이 있으면 포함
- Phase로 나눌 만한 규모면 Phase A/B/C… 형식으로 분리
- 핵심 인터페이스/클래스 시그니처를 코드 블록으로 보여줌

**기대 효과**
- 테스트 가능성, 확장성, 성능, 유지보수성 등 구체적 개선 항목
- 가능하면 테스트 코드 예시 포함

**영향 범위**
- 변경된 파일/클래스를 표로 정리
- 신규 파일은 "신규:" 접두어 표시
- 삭제된 파일은 "삭제:" 접두어 표시

### 3. PR 생성 또는 업데이트

기존 PR이 없으면 생성:
```bash
gh pr create --title "<type>: <설명>" --body "$(cat <<'EOF'
<본문>
EOF
)"
```

이미 PR이 열려 있으면 본문만 업데이트:
```bash
gh pr edit <number> --body "$(cat <<'EOF'
<본문>
EOF
)"
```

**선행 PR 처리 (중요)**
- 현재 브랜치의 base가 `main`이 아닌 다른 브랜치이거나, 사용자가 선행 PR을 언급한 경우 → 반드시 Draft로 생성
- 이 경우 개요 마지막 줄에 `Blocked By #<PR번호>` 를 추가
- 선행 PR 번호를 모르면 사용자에게 확인한다

Draft로 생성:
```bash
gh pr create --draft --title "<type>: <설명>" --body "$(cat <<'EOF'
<본문>
EOF
)"
```

일반 PR (선행 PR 없음, 작업 완료):
```bash
gh pr create --title "<type>: <설명>" --body "$(cat <<'EOF'
<본문>
EOF
)"
```

이미 PR이 열려 있으면 본문만 업데이트:
```bash
gh pr edit <number> --body "$(cat <<'EOF'
<본문>
EOF
)"
```

## 품질 기준

REFERENCE PR와 비교했을 때:
- 코드 변경이 포함된 경우 문제점 섹션 코드 스니펫이 1개 이상 있는가
- 해결 방안에 before/after 구조가 표현되어 있는가
- 영향 범위 표에 주요 파일이 빠짐없이 포함되어 있는가

위 항목 중 하나라도 빠졌다면 사용자에게 보완이 필요한 이유를 설명하고 추가 정보를 요청한다.
