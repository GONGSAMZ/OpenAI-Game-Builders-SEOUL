# 하진 캐릭터 기준 이미지

## 현재 기준

- 최종 후보: `Hajin-uniform-v3-front-cutout.png`
- 크기: `1535 × 1024`
- 형식: RGBA PNG, 투명 배경
- 표정 순서: 기본 / 기쁨 / 실망
- Figma 이미지 노드: `4:8`
- Figma 캐릭터 시트 프레임: `4:2`
- 생성일: 2026-08-17
- 생성 도구: Codex 내장 OpenAI 이미지 생성 도구

## 입력 이미지 역할

- `assets/figma-8-customers/02_HaYoung.png`: Figma에 있던 정면 카메라, 세 칸 배치, 얼굴과 포니테일, 수채화 화풍 기준
- `resources/character-art/hajin/Hajin-uniform-v2-cutout.png`: 카메라, 스토리보드, 학원 가방과 새 하진 설정 기준

## 최종 생성 프롬프트

```text
Use case: identity-preserve
Asset type: replacement three-expression character sheet for the existing Figma character sheet
Input images:
- Image 1 is the authoritative Figma reference for camera, exact front-facing orientation, three-column spacing, body scale, baseline, watercolor rendering, face identity, and ponytail.
- Image 2 is the new Hajin concept reference for the handheld camera, storyboard notebook, heavy academy backpack, updated navy middle-school uniform, and emotional tone.
Primary request: create Hajin, a 15-year-old Korean middle-school third-year student and aspiring film director, in exactly three full-body expressions: neutral and observant, genuinely happy, discouraged under study pressure.
Orientation lock: every figure must face straight toward the viewer. Head, eyes, nose, shoulders, torso, hips, knees, and shoes all face front symmetrically like Image 1. No three-quarter view, no side view, no turned body, no angled feet.
Character: preserve Image 1's recognizable round face, brown high ponytail and simple facial style. Use Image 2's plain navy middle-school blazer, cream shirt, modest charcoal pleated skirt, dark tights, practical black shoes, small camera on neck strap, compact storyboard notebook, and dark academy backpack.
Pose layout: exactly three separate figures in one horizontal sheet, evenly spaced, identical eye line, height, body proportions, costume, props, lighting, and ground baseline. Neutral pose holds storyboard calmly; happy pose makes a small confident fist while camera remains visible; discouraged pose hugs the storyboard while camera and backpack remain visible.
Style/medium: warm hand-painted watercolor with restrained gouache cleanup, soft brown linework, mild paper texture, matching the other Figma customer sheets.
Technical output: isolated figures on a genuinely transparent RGBA background, generous padding, complete hands and feet.
Remove completely: orange safety armband, safety badge, crest, student safety-officer identity.
Constraints: no text, no letters, no speech bubbles, no scenery, no extra characters, no watermark, no signature, no cropped limbs, no duplicated props.
Avoid: three-quarter angle, profile, side-facing pose, adult proportions, professional director costume, makeup, photorealism, anime cel shading, thick black outlines, checkerboard baked into the image.
```

이미지 생성 결과에는 체크무늬가 실제 배경으로 포함되어 있어 `tools/remove_checkerboard_background.py`로 배경과 연결된 밝은 중성색 영역만 알파로 변환했다. `asset_report.py --require-alpha` 검사 결과를 통과했다.
