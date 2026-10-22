FROM mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-22.04@sha256:9f07099847fb885aba664fc1f1c52f7a2eea317a1ae8fd4519f7a716823f7c0e

# The upstream image copies SQL binaries without dpkg metadata. Install BOTH
# packages at the same exact version so apt satisfies FTS's engine dependency
# without silently selecting a newer engine. Runtime verification checks version.
USER root
RUN printf '%s\n' 'deb [arch=amd64] https://packages.microsoft.com/ubuntu/22.04/mssql-server-2025 jammy main' > /etc/apt/sources.list.d/mssql-server-2025.list \
    && apt-get update \
    && ACCEPT_EULA=Y DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
       mssql-server=17.0.5015.4-1 mssql-server-fts=17.0.5015.4-1 \
    && test "$(dpkg-query -W -f='${Version}' mssql-server)" = '17.0.5015.4-1' \
    && test "$(dpkg-query -W -f='${Version}' mssql-server-fts)" = '17.0.5015.4-1' \
    && rm -rf /var/lib/apt/lists/*
USER mssql
