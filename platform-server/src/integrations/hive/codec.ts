export function encodeHivePayload(payload: object): string {
  const urlEncoded = encodeURIComponent(JSON.stringify(payload));
  return Buffer.from(urlEncoded, "utf8").toString("base64");
}

export function decodeHivePayload<T>(encodedPayload: string): T {
  try {
    const urlEncoded = Buffer.from(encodedPayload, "base64").toString("utf8");
    return JSON.parse(decodeURIComponent(urlEncoded)) as T;
  } catch {
    throw new Error("Hive 응답을 디코딩할 수 없습니다.");
  }
}
