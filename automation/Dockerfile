FROM mcr.microsoft.com/playwright:v1.49.0-noble

WORKDIR /app

COPY package.json ./
RUN npm install --production

COPY . .

# Persistent browser profile stored in /data
VOLUME /data

ENV PROFILE_DIR=/data/browser-profile
ENV PORT=3100

EXPOSE 3100

CMD ["node", "server.js"]
