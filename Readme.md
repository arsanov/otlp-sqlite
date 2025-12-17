# OTLP Sqlite Receiver

This app receives traces and logs using opentelemetry protocol and saves them into sqlite database.

It is supposed to be used in integration tests. E.g. start infrastructure, with this app in docker(mount /var/Data folder to the local folder, sqlite database will appear there), run tests, when tests are done stop the container, open database.db and check traces and logs.

## Run locally

```bash
docker build -t otlp-sqlite .
```


```bash
mkdir -p data
docker run -v "$(pwd)/data:/app/data" -p 24317:4317 otlp-sqlite -e TraceContextSettings:DbPath="/app/data/database.db"
```


## OTLP protobuf
Proto files for otlp can be downloaded [here](https://github.com/open-telemetry/opentelemetry-proto/)
