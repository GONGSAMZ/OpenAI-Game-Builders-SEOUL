import {
  DynamoDBClient,
  type DynamoDBClientConfig
} from "@aws-sdk/client-dynamodb";
import {
  DynamoDBDocumentClient,
  QueryCommand,
  UpdateCommand
} from "@aws-sdk/lib-dynamodb";
import type { AppConfig } from "../config.js";

export const playerProgressCustomerIds = [
  "jeonghyeon",
  "hajin",
  "miju",
  "sunja",
  "geonwoo",
  "taesu",
  "nari",
  "junho"
] as const;

export type PlayerProgressCustomerId = (typeof playerProgressCustomerIds)[number];

export interface CustomerProgress {
  customerId: PlayerProgressCustomerId;
  met: boolean;
  completedTopicIndexes: number[];
  storyCompleted: boolean;
}

export interface PlayerProgress {
  schemaVersion: 1;
  customers: CustomerProgress[];
}

export interface StoryProgressInput {
  completedTopicIndexes: number[];
  storyCompleted: boolean;
}

export interface PlayerProgressStore {
  getPlayerProgress(subject: string): Promise<PlayerProgress>;
  markCustomerMet(
    subject: string,
    customerId: PlayerProgressCustomerId
  ): Promise<PlayerProgress>;
  mergeStoryProgress(
    subject: string,
    customerId: PlayerProgressCustomerId,
    input: StoryProgressInput
  ): Promise<PlayerProgress>;
}

const customerOrder = new Map(
  playerProgressCustomerIds.map((customerId, index) => [customerId, index])
);

function normalizeTopicIndexes(indexes: Iterable<number>): number[] {
  return [...new Set(indexes)]
    .filter((index) => Number.isInteger(index) && index >= 0 && index <= 63)
    .sort((left, right) => left - right);
}

function sortCustomers(customers: Iterable<CustomerProgress>): CustomerProgress[] {
  return [...customers]
    .map((customer) => ({
      ...customer,
      completedTopicIndexes: normalizeTopicIndexes(customer.completedTopicIndexes)
    }))
    .sort(
      (left, right) =>
        (customerOrder.get(left.customerId) ?? Number.MAX_SAFE_INTEGER) -
        (customerOrder.get(right.customerId) ?? Number.MAX_SAFE_INTEGER)
    );
}

function emptyProgress(): PlayerProgress {
  return { schemaVersion: 1, customers: [] };
}

function isCustomerId(value: unknown): value is PlayerProgressCustomerId {
  return playerProgressCustomerIds.includes(value as PlayerProgressCustomerId);
}

export class InMemoryPlayerProgressStore implements PlayerProgressStore {
  private readonly players = new Map<
    string,
    Map<PlayerProgressCustomerId, CustomerProgress>
  >();

  public async getPlayerProgress(subject: string): Promise<PlayerProgress> {
    const customers = this.players.get(subject);
    if (!customers) return emptyProgress();
    return { schemaVersion: 1, customers: sortCustomers(customers.values()) };
  }

  public async markCustomerMet(
    subject: string,
    customerId: PlayerProgressCustomerId
  ): Promise<PlayerProgress> {
    const customers = this.getOrCreatePlayer(subject);
    const existing = customers.get(customerId);
    customers.set(customerId, {
      customerId,
      met: true,
      completedTopicIndexes: existing?.completedTopicIndexes ?? [],
      storyCompleted: existing?.storyCompleted ?? false
    });
    return this.getPlayerProgress(subject);
  }

  public async mergeStoryProgress(
    subject: string,
    customerId: PlayerProgressCustomerId,
    input: StoryProgressInput
  ): Promise<PlayerProgress> {
    const customers = this.getOrCreatePlayer(subject);
    const existing = customers.get(customerId);
    customers.set(customerId, {
      customerId,
      met: true,
      completedTopicIndexes: normalizeTopicIndexes([
        ...(existing?.completedTopicIndexes ?? []),
        ...input.completedTopicIndexes
      ]),
      storyCompleted: (existing?.storyCompleted ?? false) || input.storyCompleted
    });
    return this.getPlayerProgress(subject);
  }

  private getOrCreatePlayer(
    subject: string
  ): Map<PlayerProgressCustomerId, CustomerProgress> {
    const existing = this.players.get(subject);
    if (existing) return existing;
    const created = new Map<PlayerProgressCustomerId, CustomerProgress>();
    this.players.set(subject, created);
    return created;
  }
}

interface DynamoCustomerProgressItem {
  PK: string;
  SK: string;
  customerId: PlayerProgressCustomerId;
  met?: boolean;
  completedTopicIndexes?: Set<number> | number[];
  storyCompleted?: boolean;
}

export class DynamoDbPlayerProgressStore implements PlayerProgressStore {
  private readonly client: DynamoDBDocumentClient;

  public constructor(
    private readonly tableName: string,
    clientConfig: DynamoDBClientConfig = {}
  ) {
    this.client = DynamoDBDocumentClient.from(new DynamoDBClient(clientConfig), {
      marshallOptions: { removeUndefinedValues: true }
    });
  }

  public async getPlayerProgress(subject: string): Promise<PlayerProgress> {
    const result = await this.client.send(
      new QueryCommand({
        TableName: this.tableName,
        KeyConditionExpression: "PK = :pk AND begins_with(SK, :progressPrefix)",
        ExpressionAttributeValues: {
          ":pk": `PLAYER#${subject}`,
          ":progressPrefix": "PROGRESS#CUSTOMER#"
        },
        ConsistentRead: true
      })
    );

    const customers = (result.Items ?? [])
      .map((item) => item as DynamoCustomerProgressItem)
      .filter((item) => isCustomerId(item.customerId))
      .map((item): CustomerProgress => ({
        customerId: item.customerId,
        met: item.met === true,
        completedTopicIndexes: normalizeTopicIndexes(
          item.completedTopicIndexes instanceof Set
            ? item.completedTopicIndexes
            : item.completedTopicIndexes ?? []
        ),
        storyCompleted: item.storyCompleted === true
      }));

    return { schemaVersion: 1, customers: sortCustomers(customers) };
  }

  public async markCustomerMet(
    subject: string,
    customerId: PlayerProgressCustomerId
  ): Promise<PlayerProgress> {
    await this.client.send(
      new UpdateCommand({
        TableName: this.tableName,
        Key: this.key(subject, customerId),
        UpdateExpression:
          "SET customerId = :customerId, met = :true, storyCompleted = if_not_exists(storyCompleted, :false), updatedAt = :updatedAt",
        ExpressionAttributeValues: {
          ":customerId": customerId,
          ":true": true,
          ":false": false,
          ":updatedAt": new Date().toISOString()
        }
      })
    );
    return this.getPlayerProgress(subject);
  }

  public async mergeStoryProgress(
    subject: string,
    customerId: PlayerProgressCustomerId,
    input: StoryProgressInput
  ): Promise<PlayerProgress> {
    const topics = normalizeTopicIndexes(input.completedTopicIndexes);
    const values: Record<string, unknown> = {
      ":customerId": customerId,
      ":true": true,
      ":false": false,
      ":updatedAt": new Date().toISOString()
    };
    const setStoryCompleted = input.storyCompleted
      ? ":true"
      : "if_not_exists(storyCompleted, :false)";
    let updateExpression =
      `SET customerId = :customerId, met = :true, storyCompleted = ${setStoryCompleted}, updatedAt = :updatedAt`;

    if (topics.length > 0) {
      values[":topics"] = new Set(topics);
      updateExpression += " ADD completedTopicIndexes :topics";
    }

    await this.client.send(
      new UpdateCommand({
        TableName: this.tableName,
        Key: this.key(subject, customerId),
        UpdateExpression: updateExpression,
        ExpressionAttributeValues: values
      })
    );
    return this.getPlayerProgress(subject);
  }

  private key(subject: string, customerId: PlayerProgressCustomerId) {
    return {
      PK: `PLAYER#${subject}`,
      SK: `PROGRESS#CUSTOMER#${customerId}`
    };
  }
}

export function createPlayerProgressStore(config: AppConfig): PlayerProgressStore {
  if (config.store.dataStore === "dynamodb") {
    if (!config.store.dynamodbTable) throw new Error("DynamoDB 테이블 설정이 없습니다.");
    return new DynamoDbPlayerProgressStore(config.store.dynamodbTable);
  }
  return new InMemoryPlayerProgressStore();
}
