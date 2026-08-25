import { createHash } from "node:crypto";
import {
  cp,
  mkdir,
  readFile,
  readdir,
  rename,
  rm,
  stat,
  writeFile,
} from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const serverDirectory = path.resolve(scriptDirectory, "..");
const repositoryDirectory = path.resolve(serverDirectory, "..");
const unityDirectory = path.join(repositoryDirectory, "BungeoppangTycoon");
const unityBuildDirectory = path.join(unityDirectory, "Builds", "WebGL");
const gameDistDirectory = path.join(serverDirectory, "game-dist");
const manifestFileName = "build-manifest.json";
const sourceRoots = ["Assets", "Packages", "ProjectSettings"];
const artifactExclusions = new Set([manifestFileName, "README.md"]);

function sha256(value) {
  return createHash("sha256").update(value).digest("hex");
}

function normalizePath(value) {
  return value.split(path.sep).join("/");
}

function comparePaths(left, right) {
  return left < right ? -1 : left > right ? 1 : 0;
}

function normalizeHashContents(contents) {
  if (contents.includes(0)) return contents;
  return Buffer.from(contents.toString("utf8").replace(/\r\n/g, "\n"), "utf8");
}

async function listFiles(rootDirectory, relativeDirectory = "") {
  const directory = path.join(rootDirectory, relativeDirectory);
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];

  for (const entry of entries.sort((left, right) => comparePaths(left.name, right.name))) {
    const relativePath = path.join(relativeDirectory, entry.name);
    if (entry.isDirectory()) {
      files.push(...(await listFiles(rootDirectory, relativePath)));
      continue;
    }
    if (!entry.isFile()) {
      throw new Error(`지원하지 않는 파일 형식입니다: ${normalizePath(relativePath)}`);
    }
    files.push(normalizePath(relativePath));
  }

  return files;
}

async function describeFiles(rootDirectory, files) {
  const descriptions = [];
  for (const relativePath of files) {
    const absolutePath = path.join(rootDirectory, ...relativePath.split("/"));
    const contents = normalizeHashContents(await readFile(absolutePath));
    descriptions.push({
      path: relativePath,
      size: contents.byteLength,
      sha256: sha256(contents),
    });
  }
  return descriptions;
}

async function calculateSourceTreeHash() {
  const sourceFiles = [];
  for (const sourceRoot of sourceRoots) {
    const rootFiles = await listFiles(path.join(unityDirectory, sourceRoot));
    sourceFiles.push(...rootFiles.map((file) => `${sourceRoot}/${file}`));
  }

  sourceFiles.sort(comparePaths);
  const digest = createHash("sha256");
  for (const relativePath of sourceFiles) {
    const absolutePath = path.join(unityDirectory, ...relativePath.split("/"));
    const contents = normalizeHashContents(await readFile(absolutePath));
    digest.update(relativePath);
    digest.update("\0");
    digest.update(String(contents.byteLength));
    digest.update("\0");
    digest.update(sha256(contents));
    digest.update("\n");
  }
  return digest.digest("hex");
}

async function readUnityVersion() {
  const versionFile = path.join(unityDirectory, "ProjectSettings", "ProjectVersion.txt");
  const versionContents = await readFile(versionFile, "utf8");
  const match = /^m_EditorVersion:\s*(.+)$/m.exec(versionContents);
  if (!match) {
    throw new Error("ProjectVersion.txt에서 Unity 버전을 찾지 못했습니다.");
  }
  return match[1].trim();
}

async function describeArtifacts(directory) {
  const files = (await listFiles(directory)).filter((file) => !artifactExclusions.has(file));
  return describeFiles(directory, files);
}

async function validateWebGlOutput(directory) {
  const indexPath = path.join(directory, "index.html");
  const index = await readFile(indexPath, "utf8");
  if (!index.includes("/game-bridge.js")) {
    throw new Error("Unity index.html에 /game-bridge.js가 없습니다.");
  }
  if (!index.includes("createUnityInstance")) {
    throw new Error("Unity index.html에 createUnityInstance가 없습니다.");
  }
}

async function createManifest(directory) {
  await validateWebGlOutput(directory);
  return {
    schemaVersion: 1,
    unityVersion: await readUnityVersion(),
    sourceTreeSha256: await calculateSourceTreeHash(),
    artifacts: await describeArtifacts(directory),
  };
}

async function stageBuild() {
  await validateWebGlOutput(unityBuildDirectory);

  const stagingDirectory = path.join(serverDirectory, `.game-dist-stage-${process.pid}`);
  const backupDirectory = path.join(serverDirectory, `.game-dist-backup-${process.pid}`);
  await rm(stagingDirectory, { force: true, recursive: true });
  await rm(backupDirectory, { force: true, recursive: true });
  await mkdir(stagingDirectory, { recursive: true });

  try {
    await cp(unityBuildDirectory, stagingDirectory, { recursive: true });
    try {
      await cp(path.join(gameDistDirectory, "README.md"), path.join(stagingDirectory, "README.md"));
    } catch (error) {
      if (error.code !== "ENOENT") throw error;
    }

    const manifest = await createManifest(stagingDirectory);
    await writeFile(
      path.join(stagingDirectory, manifestFileName),
      `${JSON.stringify(manifest, null, 2)}\n`,
      "utf8",
    );

    let previousDistExists = false;
    try {
      await stat(gameDistDirectory);
      previousDistExists = true;
      await rename(gameDistDirectory, backupDirectory);
    } catch (error) {
      if (error.code !== "ENOENT") throw error;
    }

    try {
      await rename(stagingDirectory, gameDistDirectory);
    } catch (error) {
      if (previousDistExists) await rename(backupDirectory, gameDistDirectory);
      throw error;
    }
    await rm(backupDirectory, { force: true, recursive: true });

    console.log(
      `UNITY_BUILD_STAGED version=${manifest.unityVersion} source=${manifest.sourceTreeSha256} artifacts=${manifest.artifacts.length}`,
    );
  } finally {
    await rm(stagingDirectory, { force: true, recursive: true });
    await rm(backupDirectory, { force: true, recursive: true });
  }
}

async function verifyBuild() {
  await validateWebGlOutput(gameDistDirectory);
  const manifestPath = path.join(gameDistDirectory, manifestFileName);
  const expected = JSON.parse(await readFile(manifestPath, "utf8"));
  const actual = await createManifest(gameDistDirectory);

  if (JSON.stringify(expected) !== JSON.stringify(actual)) {
    const differences = [];
    if (expected.unityVersion !== actual.unityVersion) differences.push("Unity 버전");
    if (expected.sourceTreeSha256 !== actual.sourceTreeSha256) differences.push("Unity 소스 트리");
    if (JSON.stringify(expected.artifacts) !== JSON.stringify(actual.artifacts)) differences.push("WebGL 산출물");
    throw new Error(`Unity 빌드 매니페스트 불일치: ${differences.join(", ") || "스키마"}`);
  }

  console.log(
    `UNITY_BUILD_VERIFIED version=${actual.unityVersion} source=${actual.sourceTreeSha256} artifacts=${actual.artifacts.length}`,
  );
}

async function verifyArtifactsOnly() {
  await validateWebGlOutput(gameDistDirectory);
  const manifestPath = path.join(gameDistDirectory, manifestFileName);
  const expected = JSON.parse(await readFile(manifestPath, "utf8"));
  if (
    expected.schemaVersion !== 1 ||
    typeof expected.unityVersion !== "string" ||
    !/^[a-f0-9]{64}$/.test(expected.sourceTreeSha256 ?? "") ||
    !Array.isArray(expected.artifacts)
  ) {
    throw new Error("Unity 빌드 매니페스트 스키마가 올바르지 않습니다.");
  }

  const actualArtifacts = await describeArtifacts(gameDistDirectory);
  if (JSON.stringify(expected.artifacts) !== JSON.stringify(actualArtifacts)) {
    throw new Error("Unity 빌드 매니페스트 불일치: WebGL 산출물");
  }

  console.log(
    `UNITY_ARTIFACTS_VERIFIED version=${expected.unityVersion} source=${expected.sourceTreeSha256} artifacts=${actualArtifacts.length}`,
  );
}

const command = process.argv[2];
if (command === "stage") {
  await stageBuild();
} else if (command === "verify") {
  await verifyBuild();
} else if (command === "verify-artifacts") {
  await verifyArtifactsOnly();
} else {
  throw new Error("사용법: node scripts/unity-build-manifest.mjs <stage|verify|verify-artifacts>");
}
