FROM mcr.microsoft.com/playwright:v1.59.1-noble

WORKDIR /app

# xvfb lets us run the browser "headful" (so Bing's bot detection sees
# real rendering, requestAnimationFrame, WebGL, etc.) without a physical display.
# microsoft-edge-stable is installed so desktop searches can run under the
# real Edge binary — Microsoft Rewards grants an extra bonus for Edge users.
RUN apt-get update && apt-get install -y --no-install-recommends \
        xvfb x11vnc ca-certificates curl gnupg adb \
    && install -d -m 0755 /etc/apt/keyrings \
    && curl -fsSL https://packages.microsoft.com/keys/microsoft.asc \
        | gpg --dearmor -o /etc/apt/keyrings/microsoft.gpg \
    && echo "deb [arch=amd64 signed-by=/etc/apt/keyrings/microsoft.gpg] https://packages.microsoft.com/repos/edge stable main" \
        > /etc/apt/sources.list.d/microsoft-edge.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends microsoft-edge-stable \
    && rm -rf /var/lib/apt/lists/*

COPY package.json ./
RUN npm install --production

COPY . .

# Persistent browser profile stored in /data
VOLUME /data

ENV PROFILE_DIR=/data/browser-profile
ENV PORT=3100

EXPOSE 3100 5900

RUN chmod +x /app/entrypoint.sh

ENTRYPOINT ["/app/entrypoint.sh"]
CMD ["server.js"]
