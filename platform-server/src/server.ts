import "dotenv/config";
import { createApp } from "./app.js";
import { loadConfig } from "./config.js";

const config = loadConfig();
const app = createApp({ config });

const server = app.listen(config.port, () => {
  console.log(`Game integration server listening at ${config.publicBaseUrl}`);
  console.log(`Hive mode=${config.hive.mode}, OpenAI mode=${config.openai.mode}`);
});

function shutdown(signal: string): void {
  console.log(`${signal} received. Shutting down...`);
  server.close((error) => {
    if (error) {
      console.error(error);
      process.exitCode = 1;
    }
  });
}

process.on("SIGINT", () => shutdown("SIGINT"));
process.on("SIGTERM", () => shutdown("SIGTERM"));
