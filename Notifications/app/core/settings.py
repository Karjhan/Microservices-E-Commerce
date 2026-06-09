from functools import lru_cache

from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    service_name: str = "notifications-service"
    log_level: str = "INFO"

    database_url: str = (
        "postgresql+asyncpg://postgres:postgres@localhost:5433/notifications"
    )

    rabbitmq_url: str = "amqp://guest:guest@localhost:5672/"
    rabbitmq_queue: str = "notifications.events"
    rabbitmq_dlx: str = "notifications-dlx"
    rabbitmq_dlq: str = "notifications.dlq"

    smtp_host: str = "localhost"
    smtp_port: int = 1025
    smtp_from: str = "noreply@ecommerce.local"
    smtp_use_tls: bool = False
    smtp_username: str | None = None
    smtp_password: str | None = None

    notifications_operator_emails: str = "ops@example.com"

    otel_endpoint: str = "http://localhost:4317"
    otel_enabled: bool = True

    max_delivery_attempts: int = 4

    model_config = {"env_file": ".env", "extra": "ignore"}

    @property
    def operator_email_list(self) -> list[str]:
        return [e.strip() for e in self.notifications_operator_emails.split(",") if e.strip()]


@lru_cache
def get_settings() -> Settings:
    return Settings()
