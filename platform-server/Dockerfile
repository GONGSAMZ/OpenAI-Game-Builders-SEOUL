FROM node:24-alpine AS build
WORKDIR /app
RUN corepack enable
COPY platform-server/package.json platform-server/pnpm-lock.yaml platform-server/pnpm-workspace.yaml platform-server/tsconfig.json platform-server/tsconfig.build.json ./
RUN pnpm install --frozen-lockfile
COPY platform-server/src ./src
RUN pnpm build

FROM node:24-alpine AS runtime
ARG APP_REVISION=development
ENV NODE_ENV=production
ENV APP_REVISION=$APP_REVISION
WORKDIR /app
RUN corepack enable
COPY platform-server/package.json platform-server/pnpm-lock.yaml platform-server/pnpm-workspace.yaml ./
RUN pnpm install --prod --frozen-lockfile
COPY --from=build /app/dist ./dist
COPY platform-server/public ./public
COPY platform-server/game-dist ./game-dist
EXPOSE 3000
CMD ["node", "dist/src/server.js"]
