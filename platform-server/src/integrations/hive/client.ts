import type { AppConfig } from "../../config.js";
import { encodeHivePayload } from "./codec.js";

interface HiveCallbackPayload {
  code: string;
  state?: string;
}

interface HiveUserInfo {
  user_id?: number | string;
  is_blocked?: boolean;
  access_token?: string;
  refresh_token?: string;
}

export interface HiveVerificationResponse {
  code: number;
  appid?: string;
  idp_index?: number;
  idp_user_id?: string;
  enc_idp?: string;
  user_info?: HiveUserInfo;
}

function requireValue(value: string | undefined, name: string): string {
  if (!value) throw new Error(`${name} 설정이 없습니다.`);
  return value;
}

export class HiveWebLoginClient {
  public constructor(private readonly config: AppConfig["hive"]) {}

  public buildLoginUrl(): string {
    if (this.config.mode === "mock") {
      throw new Error("mock 모드에서는 실제 Hive 로그인 URL을 만들지 않습니다.");
    }

    const host =
      this.config.mode === "production"
        ? "https://weblogin.withhive.com"
        : "https://sandbox-weblogin.withhive.com";

    const param = encodeHivePayload({
      appid: requireValue(this.config.appId, "HIVE_APP_ID"),
      url: requireValue(this.config.redirectUri, "HIVE_REDIRECT_URI"),
      client_id: requireValue(this.config.clientId, "HIVE_CLIENT_ID"),
      response_type: "code",
      country: this.config.country,
      language: this.config.language
    });

    return `${host}/login?param=${encodeURIComponent(param)}`;
  }

  public async verifyCallback(payload: HiveCallbackPayload): Promise<HiveVerificationResponse> {
    if (payload.code !== "100" || !payload.state) {
      throw new Error(`Hive 로그인이 실패했습니다. code=${payload.code}`);
    }

    const host =
      this.config.mode === "production"
        ? "https://weblogin.withhive.com"
        : "https://sandbox-weblogin.withhive.com";

    const response = await fetch(`${host}/token`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        grant_type: "authorization_code",
        state: payload.state,
        client_id: requireValue(this.config.clientId, "HIVE_CLIENT_ID"),
        client_secret: requireValue(this.config.clientSecret, "HIVE_CLIENT_SECRET"),
        redirect_uri: requireValue(this.config.redirectUri, "HIVE_REDIRECT_URI")
      }),
      signal: AbortSignal.timeout(10_000)
    });

    if (!response.ok) {
      throw new Error(`Hive 토큰 검증 요청이 HTTP ${response.status}로 실패했습니다.`);
    }

    const result = (await response.json()) as HiveVerificationResponse;
    if (result.code !== 100 || !result.idp_index || !result.idp_user_id) {
      throw new Error(`Hive 토큰 검증에 실패했습니다. code=${result.code}`);
    }

    if (result.user_info?.is_blocked) {
      throw new Error("이용이 정지된 Hive 사용자입니다.");
    }

    return result;
  }
}
