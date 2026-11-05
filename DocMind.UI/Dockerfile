# Build context: the repository root (docker build -f DocMind.UI/Dockerfile .), so the root
# .dockerignore keeps node_modules, dist and .angular out of the build.

# ---- build: compile the Angular app ----
FROM node:24-alpine AS build
WORKDIR /src
COPY DocMind.UI/package.json DocMind.UI/package-lock.json ./
RUN npm ci
COPY DocMind.UI/ ./
RUN npm run build

# ---- web: nginx serves the app and proxies /api/ to the API ----
FROM nginx:alpine
COPY DocMind.UI/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/docmind-ui/browser /usr/share/nginx/html
