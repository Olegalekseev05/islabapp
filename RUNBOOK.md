# RUNBOOK

Все команды ниже предполагают запуск из корня репозитория.

## Подготовка

1. Скопировать пример переменных окружения:

```bash
cp deploy/.env.example deploy/.env
```

2. Проверить значения в `deploy/.env`: `MSSQL_SA_PASSWORD`, `DB_NAME`, `APP_IMAGE_TAG`, `APP_VERSION`.

Рекомендация: `APP_IMAGE_TAG` и `APP_VERSION` держать одинаковыми для релиза, чтобы `/version` показывал фактически развернутую версию.

## Проверка статуса сервисов

```bash
docker compose -f deploy/docker-compose.yml --env-file deploy/.env ps
```

Ожидаемый результат: сервисы `app` и `mssql` находятся в статусе `running`.

## Просмотр логов

Все сервисы:

```bash
docker compose -f deploy/docker-compose.yml --env-file deploy/.env logs
```

Следить в реальном времени:

```bash
docker compose -f deploy/docker-compose.yml --env-file deploy/.env logs -f
```

Только приложение:

```bash
docker compose -f deploy/docker-compose.yml --env-file deploy/.env logs -f app
```

Только SQL Server:

```bash
docker compose -f deploy/docker-compose.yml --env-file deploy/.env logs -f mssql
```

## Проверка доступности приложения

`/health`:

```bash
curl -fsS http://127.0.0.1:5000/health
```

`/version`:

```bash
curl -fsS http://127.0.0.1:5000/version
```

`/db/ping`:

```bash
curl -fsS http://127.0.0.1:5000/db/ping
```

Ожидаемо: `/health` возвращает `status=ok`, `/version` возвращает актуальную версию, `/db/ping` подтверждает доступность MSSQL.

## Обновление приложения

1. Открыть `deploy/.env`.
2. Поменять:
   `APP_IMAGE_TAG=<новый тег>`
   `APP_VERSION=<новая версия>`
3. Скачать новый образ:

```bash
docker compose -f deploy/docker-compose.yml --env-file deploy/.env pull app
```

4. Перезапустить приложение с новым тегом:

```bash
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d app
```

5. Проверить `ps`, логи и endpoints `/health`, `/version`, `/db/ping`.

Пример тега: `latest` или короткий SHA из CI, например `a1b2c3d`.

Полное имя образа: `ghcr.io/olegalekseev05/islabapp:<tag>`.

## Откат приложения

1. Открыть `deploy/.env`.
2. Вернуть предыдущие значения:
   `APP_IMAGE_TAG=<предыдущий тег>`
   `APP_VERSION=<предыдущая версия>`
3. Скачать предыдущий образ:

```bash
docker compose -f deploy/docker-compose.yml --env-file deploy/.env pull app
```

4. Перезапустить приложение:

```bash
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d app
```

5. Проверить `ps`, логи и endpoints `/health`, `/version`, `/db/ping`.

## Создание бэкапа

Текущее постоянное хранилище в этом стенде: именованный Docker volume для MSSQL. Для текущего compose-проекта по умолчанию это `deploy_mssql_data`.

Бэкапы хранить на хосте в каталоге `deploy/backups/`.

1. Найти фактическое имя volume:

```bash
docker volume ls --format '{{.Name}}' | grep '_mssql_data$'
```

2. Создать архив volume в `deploy/backups/`:

```bash
mkdir -p deploy/backups
docker run --rm \
  -v <volume_name>:/volume \
  -v "$PWD/deploy/backups:/backup" \
  alpine \
  sh -c 'tar -czf /backup/mssql_data_$(date -u +%Y%m%dT%H%M%SZ).tar.gz -C /volume .'
```

3. Проверить, что архив появился:

```bash
ls -lh deploy/backups
```

## Восстановление из бэкапа

1. Остановить сервисы:

```bash
docker compose -f deploy/docker-compose.yml --env-file deploy/.env down
```

2. Очистить целевой volume и развернуть в него архив:

```bash
docker run --rm \
  -v <volume_name>:/volume \
  -v "$PWD/deploy/backups:/backup" \
  alpine \
  sh -c 'rm -rf /volume/* && tar -xzf /backup/<backup_file>.tar.gz -C /volume'
```

3. Поднять сервисы:

```bash
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d
```

## Проверка восстановления

1. Проверить статус:

```bash
docker compose -f deploy/docker-compose.yml --env-file deploy/.env ps
```

2. Проверить логи `mssql` и `app` на предмет ошибок старта.

3. Проверить endpoints:

```bash
curl -fsS http://127.0.0.1:5000/health
curl -fsS http://127.0.0.1:5000/version
curl -fsS http://127.0.0.1:5000/db/ping
```

4. Убедиться, что `/db/ping` возвращает успешный ответ.

Ограничение текущего стенда: приложение пока не хранит бизнес-данные в MSSQL, поэтому практическая проверка восстановления сейчас сводится к успешному запуску MSSQL и положительному ответу `/db/ping`.
